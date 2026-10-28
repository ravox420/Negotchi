using System;
using System.Collections.Generic;
using System.Linq;

namespace Negotchi
{
    // Public App Server lifecycle events only. No inspection of private reasoning text.
    internal sealed class PetActivity
    {
        private readonly Dictionary<string, string> items = new Dictionary<string, string>();
        private readonly HashSet<int> waits = new HashSet<int>();
        private readonly HashSet<string> retiredTurns = new HashSet<string>();
        private string threadId, turnId;
        private bool reviewing, responding;
        private int nextWait;
        public bool Active { get; private set; }
        public string Animation { get; private set; }
        public string Text { get; private set; }
        public PetActivity() { Reset(); }
        public void Reset() { if (!string.IsNullOrEmpty(turnId)) retiredTurns.Add(turnId); Active = false; items.Clear(); waits.Clear(); reviewing = responding = false; threadId = turnId = null; Set("idle", "Pronto para conversar."); }
        public void Begin(string thread) { Reset(); Active = true; threadId = thread; Set("thinking", "Pensando..."); }
        public void BindThread(string thread) { if (Active) threadId = thread; }
        public int Wait() { int token = ++nextWait; if (Active) { waits.Add(token); Update(); } return token; }
        public void Resume(int token) { if (waits.Remove(token) && Active) Update(); }
        private void Set(string animation, string text) { Animation = animation; Text = text; }
        private void Update()
        {
            if (waits.Count > 0) Set("waiting", "Aguardando sua resposta...");
            else if (reviewing || items.Values.Contains("review")) Set("review", "Revisando...");
            else if (items.Values.Contains("running")) Set("running", "Executando seu pedido...");
            else if (responding || items.Values.Contains("responding")) Set("responding", "Respondendo...");
            else Set("thinking", "Pensando...");
        }
        public bool Observe(string method, Dictionary<string, object> parameters)
        {
            if (!Active || string.IsNullOrEmpty(threadId) || Json.Text(parameters, "threadId") != threadId) return false;
            var turn = Json.Object(parameters, "turn");
            string incoming = method.StartsWith("turn/", StringComparison.Ordinal) ? Json.Text(turn, "id") : Json.Text(parameters, "turnId");
            if (incoming.Length == 0 || retiredTurns.Contains(incoming) || (!string.IsNullOrEmpty(turnId) && incoming != turnId)) return false;
            if (string.IsNullOrEmpty(turnId) && method != "turn/started") return false;
            if (string.IsNullOrEmpty(turnId)) turnId = incoming;
            if (method == "turn/completed")
            {
                string status = Json.Text(turn, "status");
                Reset();
                Set(status == "failed" ? "failed" : status == "interrupted" ? "idle" : "jumping", status == "failed" ? "O pedido falhou." : status == "interrupted" ? "Pedido interrompido." : "Pronto. Mais alguma coisa?");
                return true;
            }
            if (method == "item/agentMessage/delta") responding = true;
            else if (method == "item/started" || method == "item/completed")
            {
                var item = Json.Object(parameters, "item"); string id = Json.Text(item, "id"), type = Json.Text(item, "type");
                if (type == "enteredReviewMode") reviewing = true;
                if (type == "exitedReviewMode") reviewing = false;
                if (method == "item/completed") { items.Remove(id); if (type == "agentMessage") responding = false; }
                else
                {
                    responding = false;
                    string animation = "thinking";
                    if (type == "agentMessage") animation = "responding";
                    else if (type == "fileChange") animation = "review";
                    else if (type == "commandExecution")
                    {
                        var actions = Json.Items(item, "commandActions").OfType<Dictionary<string, object>>().ToArray();
                        animation = actions.Length > 0 && actions.All(a => new[] { "read", "listFiles", "search" }.Contains(Json.Text(a, "type"))) ? "review" : "running";
                    }
                    else if (new[] { "mcpToolCall", "dynamicToolCall", "webSearch", "imageGeneration" }.Contains(type)) animation = "running";
                    else if (type == "imageView") animation = "review";
                    items[id] = animation;
                }
            }
            else if (method == "error")
            {
                if (!Equals(Json.Get(parameters, "willRetry"), true)) Reset();
                Set("failed", Json.Text(Json.Object(parameters, "error"), "message")); return true;
            }
            else if (method != "turn/started" && !method.StartsWith("item/reasoning/", StringComparison.Ordinal)) return false;
            Update(); return true;
        }
    }
}
