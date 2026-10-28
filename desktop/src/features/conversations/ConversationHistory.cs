using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Negotchi
{
    internal sealed class HistoryConversation
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Folder { get; set; }
        public string Preview { get; set; }
        public string Model { get; set; }
        public string Effort { get; set; }
        public long UpdatedAt { get; set; }
        public long CreatedAt { get; set; }
        public bool Archived { get; set; }
        public ConversationState Local { get; set; }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(Title) ? (string.IsNullOrWhiteSpace(Preview) ? "Conversa sem título" : Preview.Replace('\n', ' ').Replace('\r', ' ')) : Title; } }
        public string Updated { get { return DateLabel(UpdatedAt); } }
        public string Created { get { return DateLabel(CreatedAt); } }
        private static string DateLabel(long timestamp)
        {
            if (timestamp <= 0) return "—";
            try { return new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime().ToString("dd/MM HH:mm"); }
            catch (ArgumentOutOfRangeException) { return "—"; }
        }
        public bool Matches(string search)
        {
            search = (search ?? "").Trim();
            return search.Length == 0 || (DisplayName + "\n" + Folder + "\n" + Preview + "\n" + Id).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (Local != null && Local.Matches(search));
        }
        public static HistoryConversation FromJson(Dictionary<string, object> thread, bool archived)
        {
            return new HistoryConversation { Id = Json.Text(thread, "id"), Title = Json.Text(thread, "name"), Preview = Json.Text(thread, "preview"), Folder = Json.Text(thread, "cwd"), Model = Json.Text(thread, "model"), Effort = Json.Text(thread, "reasoningEffort"), UpdatedAt = Convert.ToInt64(Json.Get(thread, "updatedAt") ?? 0), CreatedAt = Convert.ToInt64(Json.Get(thread, "createdAt") ?? 0), Archived = archived };
        }
    }

    internal sealed class ConversationHistory
    {
        private readonly CodexClient client;
        internal ConversationHistory(CodexClient client) { this.client = client; }
        internal async Task<List<HistoryConversation>> List(bool archived, CancellationToken cancellation)
        {
            var result = new List<HistoryConversation>(); string cursor = null; var visited = new HashSet<string>();
            do
            {
                cancellation.ThrowIfCancellationRequested();
                // Like resume --all: no cwd restriction; empty providers means all providers.
                // Omitted sourceKinds retains the CLI's default interactive sessions.
                var page = await client.RequestAsync("thread/list", Json.Map("cursor", cursor, "limit", 100, "sortKey", "updated_at", "sortDirection", "desc", "archived", archived, "modelProviders", new string[0]));
                cancellation.ThrowIfCancellationRequested();
                result.AddRange(Json.Items(page, "data").OfType<Dictionary<string, object>>().Select(thread => HistoryConversation.FromJson(thread, archived)).Where(thread => !string.IsNullOrEmpty(thread.Id)));
                cursor = Json.Text(page, "nextCursor");
                if (cursor.Length > 0 && !visited.Add(cursor)) throw new InvalidOperationException("O Codex repetiu uma página do histórico.");
            } while (cursor.Length > 0);
            return result.GroupBy(thread => thread.Id).Select(group => group.First()).ToList();
        }
        internal async Task<List<ChatMessage>> Messages(string threadId, string speaker, CancellationToken cancellation = default(CancellationToken))
        {
            var result = new List<ChatMessage>(); string cursor = null; var visited = new HashSet<string>();
            do
            {
                cancellation.ThrowIfCancellationRequested();
                var page = await client.RequestAsync("thread/turns/list", Json.Map("threadId", threadId, "cursor", cursor, "limit", 100, "sortDirection", "asc", "itemsView", "full"));
                cancellation.ThrowIfCancellationRequested();
                foreach (var turn in Json.Items(page, "data").OfType<Dictionary<string, object>>())
                foreach (var item in Json.Items(turn, "items").OfType<Dictionary<string, object>>())
                {
                    string type = Json.Text(item, "type");
                    if (type == "agentMessage") result.Add(new ChatMessage { Role = "assistant", Text = Json.Text(item, "text"), PetName = speaker });
                    else if (type == "userMessage")
                    {
                        string text = string.Join("\n", Json.Items(item, "content").OfType<Dictionary<string, object>>().Select(content => Json.Text(content, "type") == "text" ? Json.Text(content, "text") : "[" + Json.Text(content, "type") + "]"));
                        result.Add(new ChatMessage { Role = "user", Text = text });
                    }
                }
                cursor = Json.Text(page, "nextCursor");
                if (cursor.Length > 0 && !visited.Add(cursor)) throw new InvalidOperationException("O Codex repetiu uma página da conversa.");
            } while (cursor.Length > 0);
            return result;
        }
        internal Task<Dictionary<string, object>> Rename(string threadId, string name) { return client.RequestAsync("thread/name/set", Json.Map("threadId", threadId, "name", name)); }
        internal Task<Dictionary<string, object>> Archive(string threadId, bool archived) { return client.RequestAsync(archived ? "thread/archive" : "thread/unarchive", Json.Map("threadId", threadId)); }
    }
}
