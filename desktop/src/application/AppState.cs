using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    public sealed class ChatMessage
    {
        public string Role { get; set; }
        public string Text { get; set; }
        public string PetName { get; set; }
        public List<PendingAttachment> Media { get; set; }
    }

    public sealed class ConversationState
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public string Title { get; set; }
        public string Folder { get; set; }
        public string ThreadId { get; set; }
        public List<ChatMessage> Messages { get; set; }
        public string SelectedModel { get; set; }
        public string ReasoningEffort { get; set; }
        public string ThreadMemoryMode { get; set; }
        public Dictionary<string, object> TokenUsage { get; set; }
        public bool Archived { get; set; }
        public string Sandbox { get; set; }
        public string Draft { get; set; }
        public List<PendingAttachment> Attachments { get; set; }
        public List<ReviewComment> ReviewComments { get; set; }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(Title) ? "Conversa sem titulo" : Title; } }
        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            search = search.Trim();
            if (DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return (Messages ?? new List<ChatMessage>()).Any(message => message != null && (message.Text ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    public sealed class ProjectState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(Name) ? "Projeto sem nome" : Name; } }
    }

    public sealed class AppState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Scale { get; set; }
        public bool Topmost { get; set; }
        public string Folder { get; set; }
        public string ThreadId { get; set; }
        public List<ChatMessage> Messages { get; set; }
        public string SelectedPetId { get; set; }
        public string PendingResetIdempotencyKey { get; set; }
        public string PendingResetAccountEmail { get; set; }
        public string SelectedModel { get; set; }
        public string ReasoningEffort { get; set; }
        public string ThreadMemoryMode { get; set; }
        public Dictionary<string, object> TokenUsage { get; set; }
        public AppearanceSettings Appearance { get; set; }
        public string Sandbox { get; set; }
        public List<ConversationState> Conversations { get; set; }
        public string ActiveConversationId { get; set; }
        public List<ProjectState> Projects { get; set; }
        public string ActiveProjectId { get; set; }
        public List<ProcessReminder> ProcessReminders { get; set; }
        public AppState()
        {
            X = -1; Y = -1; Scale = 1; Topmost = true; SelectedPetId = "PimpDogg";
            Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Sandbox = "read-only";
            Messages = new List<ChatMessage>();
            Conversations = new List<ConversationState>();
            Projects = new List<ProjectState>();
            ProcessReminders = new List<ProcessReminder>();
            Appearance = new AppearanceSettings();
        }
        public void EnsureProjects()
        {
            Projects = Projects ?? new List<ProjectState>();
            if (Projects.Count == 0) Projects.Add(new ProjectState { Id = Guid.NewGuid().ToString("N"), Name = "Meus projetos" });
            foreach (var project in Projects)
            {
                if (string.IsNullOrWhiteSpace(project.Id)) project.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(project.Name)) project.Name = "Projeto sem nome";
            }
            if (!Projects.Any(project => project.Id == ActiveProjectId)) ActiveProjectId = Projects[0].Id;
        }
        public void EnsureConversations()
        {
            EnsureProjects();
            ProcessReminders = ProcessReminders ?? new List<ProcessReminder>();
            Conversations = Conversations ?? new List<ConversationState>();
            if (Conversations.Count == 0) Conversations.Add(new ConversationState { Id = Guid.NewGuid().ToString("N"), Title = "Conversa nova", Folder = Folder, ThreadId = ThreadId, Messages = Copy(Messages), SelectedModel = SelectedModel, ReasoningEffort = ReasoningEffort, ThreadMemoryMode = ThreadMemoryMode, TokenUsage = TokenUsage, Sandbox = SafeSandbox(Sandbox) });
            foreach (var conversation in Conversations)
            {
                if (string.IsNullOrWhiteSpace(conversation.Id)) conversation.Id = Guid.NewGuid().ToString("N");
                if (!Projects.Any(project => project.Id == conversation.ProjectId)) conversation.ProjectId = ActiveProjectId;
                if (string.IsNullOrWhiteSpace(conversation.Title)) conversation.Title = "Conversa nova";
                // Only the previously active conversation inherits a legacy global policy.
                if (conversation.Sandbox == null) conversation.Sandbox = conversation.Id == ActiveConversationId ? SafeSandbox(Sandbox) : "read-only";
                else conversation.Sandbox = SafeSandbox(conversation.Sandbox);
                if (!Directory.Exists(conversation.Folder)) conversation.Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                conversation.Messages = conversation.Messages == null ? new List<ChatMessage>() : conversation.Messages.Where(message => message != null && message.Text != null).ToList();
            }
            if (!Conversations.Any(conversation => conversation.Id == ActiveConversationId)) ActiveConversationId = Conversations[0].Id;
        }
        public ConversationState ActiveConversation() { EnsureConversations(); return Conversations.First(conversation => conversation.Id == ActiveConversationId); }
        public void SaveActiveConversation()
        {
            var conversation = ActiveConversation(); conversation.Folder = Folder; conversation.ThreadId = ThreadId; conversation.Messages = Copy(Messages); conversation.SelectedModel = SelectedModel; conversation.ReasoningEffort = ReasoningEffort; conversation.ThreadMemoryMode = ThreadMemoryMode; conversation.TokenUsage = TokenUsage; conversation.Sandbox = SafeSandbox(Sandbox);
        }
        public void LoadActiveConversation()
        {
            var conversation = ActiveConversation(); ActiveProjectId = conversation.ProjectId; Sandbox = SafeSandbox(conversation.Sandbox); Folder = conversation.Folder; ThreadId = conversation.ThreadId; Messages = Copy(conversation.Messages); SelectedModel = conversation.SelectedModel; ReasoningEffort = conversation.ReasoningEffort; ThreadMemoryMode = conversation.ThreadMemoryMode; TokenUsage = conversation.TokenUsage;
        }
        internal static string SafeSandbox(string value) { return value == "workspace-write" || value == "danger-full-access" ? value : "read-only"; }
        private static List<ChatMessage> Copy(IEnumerable<ChatMessage> messages) { return (messages ?? new ChatMessage[0]).Select(message => new ChatMessage { Role = message.Role, Text = message.Text, PetName = message.PetName, Media = (message.Media ?? new List<PendingAttachment>()).Where(item => item != null).Select(item => new PendingAttachment { Path = item.Path, Name = item.Name, Kind = item.Kind, Size = item.Size }).ToList() }).ToList(); }
    }
}
