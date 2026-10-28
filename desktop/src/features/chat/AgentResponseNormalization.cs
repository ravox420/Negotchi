using System;
using System.Collections.Generic;
using System.Linq;

namespace Negotchi
{
    internal enum ApprovalKind { CommandExecution, FileChange, Permissions, Unknown }
    internal sealed class ApprovalFileChange { public string Path; public string Kind; public string Diff; }
    internal sealed class NormalizedApprovalRequest
    {
        public ApprovalKind Kind; public string Title; public string Summary; public string Reason; public string WorkingDirectory; public string Command; public string GrantRoot; public string NetworkTarget;
        public readonly List<ApprovalFileChange> FileChanges = new List<ApprovalFileChange>();
        public readonly List<string> Permissions = new List<string>();
        public Dictionary<string, object> RawParameters; public Dictionary<string, object> RawItem;
    }
    internal static class AgentResponseNormalizer
    {
        public static NormalizedApprovalRequest Normalize(string method, Dictionary<string, object> parameters, Dictionary<string, object> item)
        {
            var request = new NormalizedApprovalRequest { RawParameters = parameters ?? Json.Map(), RawItem = item ?? Json.Map(), Reason = Json.Text(parameters, "reason"), WorkingDirectory = Json.Text(parameters, "cwd") };
            if (method == "item/commandExecution/requestApproval") return Command(parameters, item, request);
            if (method == "item/fileChange/requestApproval") return FileChange(parameters, item, request);
            if (method == "item/permissions/requestApproval") return Permission(parameters, request);
            request.Kind = ApprovalKind.Unknown; request.Title = "Solicitação de aprovação"; request.Summary = "O Codex enviou uma solicitação que esta versão não reconhece completamente."; return request;
        }
        private static NormalizedApprovalRequest Command(Dictionary<string, object> parameters, Dictionary<string, object> item, NormalizedApprovalRequest request)
        {
            request.Kind = ApprovalKind.CommandExecution; request.Title = "Executar ação"; request.Command = First(Json.Text(parameters, "command"), Json.Text(item, "command")); request.WorkingDirectory = First(request.WorkingDirectory, Json.Text(item, "cwd"));
            var network = Json.Object(parameters, "networkApprovalContext");
            if (network.Count > 0) { string host = Json.Text(network, "host"), protocol = Json.Text(network, "protocol"), port = Json.Text(network, "port"); request.NetworkTarget = protocol + "://" + host + (port.Length == 0 ? "" : ":" + port); request.Summary = "O Codex solicita acesso de rede para " + request.NetworkTarget + "."; }
            else request.Summary = string.IsNullOrWhiteSpace(request.Command) ? "O Codex solicita executar uma ação." : "O Codex solicita executar um comando.";
            AddActions(request.Permissions, Json.Items(parameters, "commandActions")); if (request.Permissions.Count == 0) AddActions(request.Permissions, Json.Items(item, "commandActions")); AddPermissions(request.Permissions, Json.Object(parameters, "additionalPermissions")); return request;
        }
        private static NormalizedApprovalRequest FileChange(Dictionary<string, object> parameters, Dictionary<string, object> item, NormalizedApprovalRequest request)
        {
            request.Kind = ApprovalKind.FileChange; request.Title = "Alterar arquivos"; request.GrantRoot = Json.Text(parameters, "grantRoot");
            foreach (var value in Json.Items(item, "changes").OfType<Dictionary<string, object>>()) request.FileChanges.Add(new ApprovalFileChange { Path = Json.Text(value, "path"), Kind = Json.Text(value, "kind"), Diff = Json.Text(value, "diff") });
            request.Summary = request.FileChanges.Count == 0 ? "O Codex solicita alterar arquivos." : "O Codex solicita alterar " + request.FileChanges.Count + " arquivo" + (request.FileChanges.Count == 1 ? "." : "s."); return request;
        }
        private static NormalizedApprovalRequest Permission(Dictionary<string, object> parameters, NormalizedApprovalRequest request) { request.Kind = ApprovalKind.Permissions; request.Title = "Permissão solicitada"; request.Summary = "O Codex solicita permissões adicionais para esta tarefa."; AddPermissions(request.Permissions, Json.Object(parameters, "permissions")); return request; }
        private static void AddActions(List<string> target, IEnumerable<object> values) { foreach (var value in values.OfType<Dictionary<string, object>>()) Add(target, Json.Text(value, "type")); }
        private static void AddPermissions(List<string> target, Dictionary<string, object> values) { foreach (var pair in values) { if (pair.Value is Dictionary<string, object> || pair.Value is IEnumerable<object>) Add(target, pair.Key); else if (pair.Value != null) Add(target, pair.Key + ": " + Convert.ToString(pair.Value)); } }
        private static void Add(List<string> target, string value) { if (!string.IsNullOrWhiteSpace(value) && !target.Contains(value)) target.Add(value); }
        private static string First(string first, string second) { return string.IsNullOrWhiteSpace(first) ? second : first; }
    }
}
