using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Negotchi
{
    internal sealed class SlashCommand
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string[] Aliases { get; private set; }
        public SlashCommand(string name, string description, params string[] aliases) { Name = name; Description = description; Aliases = aliases; }
        public override string ToString() { return Name + "  " + Description; }
    }

    internal sealed class SlashInvocation
    {
        public SlashCommand Command;
        public string Name, Arguments;
    }

    internal static class SlashCommands
    {
        public static readonly SlashCommand[] All = {
            new SlashCommand("/model", "Escolher modelo e esforco de raciocinio", "/reasoning"),
            new SlashCommand("/usage", "Consultar limites, atividade e tokens", "/status"),
            new SlashCommand("/skills", "Escolher uma skill para o proximo pedido"),
            new SlashCommand("/memories", "Configurar memorias do Codex e deste chat"),
            new SlashCommand("/mcp", "Consultar servidores e ferramentas conectados"),
            new SlashCommand("/new", "Iniciar uma nova conversa"),
            new SlashCommand("/login", "Conectar a conta do Codex"),
            new SlashCommand("/pets", "Abrir a selecao de personagens"),
            new SlashCommand("/import-pet", "Importar um pet a partir de pet.json e spritesheet"),
            new SlashCommand("/help", "Ver os comandos disponiveis")
        };
        public static bool TryParse(string text, out SlashInvocation invocation)
        {
            invocation = null;
            text = (text ?? "").Trim();
            if (!text.StartsWith("/", StringComparison.Ordinal)) return false;
            int split = text.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            string name = (split < 0 ? text : text.Substring(0, split)).ToLowerInvariant();
            invocation = new SlashInvocation { Name = name, Arguments = split < 0 ? "" : text.Substring(split).Trim(), Command = All.FirstOrDefault(c => c.Name == name || c.Aliases.Contains(name)) };
            return true;
        }
        public static SlashCommand[] Match(string prefix)
        {
            prefix = (prefix ?? "").Trim();
            return All.Where(c => c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || c.Aliases.Any(alias => alias.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray();
        }
    }

    internal sealed class ModelChoice
    {
        public string Model { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string DefaultEffort { get; set; }
        public string[] Efforts { get; set; }
        public bool IsDefault { get; set; }
    }
    internal sealed class ModelSelection { public string Model, Effort; }
    internal sealed class SkillChoice
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Path { get; set; }
        public bool Enabled { get; set; }
        public override string ToString() { return DisplayName + (Enabled ? "" : " (desativada)"); }
    }
    internal sealed class MemoryPreferences
    {
        public bool? FeatureEnabled, UseMemories, GenerateMemories;
        public string ThreadMode;
    }
    internal sealed class RpcRead
    {
        public Dictionary<string, object> Data;
        public string Error;
    }

    // Native app-server operations behind the slash-command interface.
    internal sealed class CodexCommands
    {
        private readonly CodexClient client;
        public CodexCommands(CodexClient connection) { client = connection; }
        public async Task<Dictionary<string, object>> ReadConfig(string folder)
        {
            return Json.Object(await client.RequestAsync("config/read", Json.Map("cwd", folder, "includeLayers", false)), "config");
        }
        public async Task<List<ModelChoice>> Models()
        {
            var result = new List<ModelChoice>(); string cursor = null; var visited = new HashSet<string>();
            do
            {
                var page = await client.RequestAsync("model/list", Json.Map("includeHidden", false, "limit", 100, "cursor", cursor));
                foreach (var item in Json.Items(page, "data").OfType<Dictionary<string, object>>())
                {
                    if (Equals(Json.Get(item, "hidden"), true)) continue;
                    result.Add(new ModelChoice { Model = Json.Text(item, "model"), DisplayName = Json.Text(item, "displayName"), Description = Json.Text(item, "description"), DefaultEffort = Json.Text(item, "defaultReasoningEffort"), IsDefault = Equals(Json.Get(item, "isDefault"), true), Efforts = Json.Items(item, "supportedReasoningEfforts").OfType<Dictionary<string, object>>().Select(e => Json.Text(e, "reasoningEffort")).Where(e => e.Length > 0).Distinct().ToArray() });
                }
                cursor = Json.Text(page, "nextCursor");
                if (cursor.Length > 0 && !visited.Add(cursor)) throw new InvalidOperationException("O catalogo de modelos retornou uma pagina repetida.");
            } while (cursor.Length > 0);
            return result.Where(m => !string.IsNullOrWhiteSpace(m.Model)).GroupBy(m => m.Model).Select(g => g.First()).ToList();
        }
        public async Task<List<SkillChoice>> Skills(string folder)
        {
            var response = await client.RequestAsync("skills/list", Json.Map("cwds", new[] { folder }, "forceReload", true));
            var result = new List<SkillChoice>();
            foreach (var entry in Json.Items(response, "data").OfType<Dictionary<string, object>>())
            {
                var errors = Json.Items(entry, "errors");
                foreach (var item in Json.Items(entry, "skills").OfType<Dictionary<string, object>>())
                {
                    string name = Json.Text(item, "name"), display = Json.Text(Json.Object(item, "interface"), "displayName");
                    result.Add(new SkillChoice { Name = name, DisplayName = display.Length > 0 ? display : name, Path = Json.Text(item, "path"), Description = Json.Text(item, "description"), Enabled = Equals(Json.Get(item, "enabled"), true) });
                }
                if (result.Count == 0 && errors.Length > 0) throw new InvalidOperationException("O Codex encontrou erros ao carregar skills: " + Json.Encode(errors));
            }
            return result.Where(s => s.Name.Length > 0 && s.Path.Length > 0).GroupBy(s => s.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }
        public static MemoryPreferences MemoryConfig(Dictionary<string, object> config, string threadMode)
        {
            return new MemoryPreferences { FeatureEnabled = Json.Get(Json.Object(config, "features"), "memories") as bool?, UseMemories = Json.Get(Json.Object(config, "memories"), "use_memories") as bool?, GenerateMemories = Json.Get(Json.Object(config, "memories"), "generate_memories") as bool?, ThreadMode = threadMode };
        }
        public static object[] MemoryEdits(MemoryPreferences previous, MemoryPreferences next)
        {
            var edits = new List<object>();
            AddMemoryEdit(edits, "features.memories", previous.FeatureEnabled, next.FeatureEnabled);
            AddMemoryEdit(edits, "memories.use_memories", previous.UseMemories, next.UseMemories);
            AddMemoryEdit(edits, "memories.generate_memories", previous.GenerateMemories, next.GenerateMemories);
            return edits.ToArray();
        }
        private static void AddMemoryEdit(List<object> edits, string key, bool? previous, bool? next)
        {
            if (next.HasValue && next != previous) edits.Add(Json.Map("keyPath", key, "value", next.Value, "mergeStrategy", "replace"));
        }
        public async Task ApplyMemoryGlobals(MemoryPreferences previous, MemoryPreferences next)
        {
            object[] edits = MemoryEdits(previous, next);
            if (edits.Length > 0) await client.RequestAsync("config/batchWrite", Json.Map("edits", edits, "reloadUserConfig", true));
        }
        public async Task<RpcRead> Read(string method, object parameters)
        {
            try { return new RpcRead { Data = await client.RequestAsync(method, parameters) }; }
            catch (Exception error) { return new RpcRead { Error = error.Message }; }
        }
        public static List<object> TurnInput(string text, SkillChoice skill)
        {
            var input = new List<object> { Json.Map("type", "text", "text", text) };
            if (skill != null)
            {
                if (!skill.Enabled || string.IsNullOrWhiteSpace(skill.Path)) throw new InvalidOperationException("Esta skill nao esta disponivel.");
                input.Add(Json.Map("type", "skill", "name", skill.Name, "path", skill.Path));
            }
            return input;
        }
        public static Dictionary<string, object> ModelSettings(string threadId, ModelSelection selection)
        {
            var settings = Json.Map("threadId", threadId, "model", selection.Model);
            if (!string.IsNullOrEmpty(selection.Effort)) settings.Add("effort", selection.Effort);
            return settings;
        }
        public static string UsageText(RpcRead account, RpcRead limits, RpcRead usage, Dictionary<string, object> tokens)
        {
            var text = new StringBuilder();
            text.AppendLine("TOKENS DESTA CONVERSA");
            if (tokens == null || tokens.Count == 0) text.AppendLine("Ainda nao informados pelo Codex.");
            else
            {
                var total = Json.Object(tokens, "total"); var last = Json.Object(tokens, "last");
                text.AppendLine("Total acumulado: " + Metric(total, "totalTokens"));
                text.AppendLine("Entrada: " + Metric(total, "inputTokens") + " | Saida: " + Metric(total, "outputTokens"));
                text.AppendLine("Entrada em cache: " + Metric(total, "cachedInputTokens") + " | Raciocinio: " + Metric(total, "reasoningOutputTokens"));
                text.AppendLine("Ultimo turno: " + Metric(last, "totalTokens"));
                text.AppendLine("Janela de contexto: " + Metric(tokens, "modelContextWindow"));
            }
            text.AppendLine(); text.AppendLine("CONTA E LIMITES");
            if (account.Error != null) text.AppendLine(account.Error);
            else
            {
                var data = Json.Object(account.Data, "account");
                text.AppendLine(data.Count == 0 ? "Conta nao conectada. Use /login." : "Tipo: " + Json.Text(data, "type") + (Json.Text(data, "planType").Length > 0 ? " | Plano: " + Json.Text(data, "planType") : ""));
            }
            if (limits.Error != null) text.AppendLine("Limites indisponiveis: " + limits.Error);
            else
            {
                var buckets = Json.Object(limits.Data, "rateLimitsByLimitId");
                var snapshots = buckets.Count > 0 ? buckets.Values.OfType<Dictionary<string, object>>() : new[] { Json.Object(limits.Data, "rateLimits") };
                bool found = false;
                foreach (var snapshot in snapshots)
                {
                    foreach (string key in new[] { "primary", "secondary" })
                    {
                        var window = Json.Object(snapshot, key);
                        if (Json.Get(window, "usedPercent") == null) continue;
                        found = true;
                        string name = Json.Text(snapshot, "limitName"); if (name.Length == 0) name = Json.Text(snapshot, "limitId");
                        text.Append((name.Length > 0 ? name + " - " : "") + WindowName(window, key) + ": " + Metric(window, "usedPercent") + "% usado");
                        if (Json.Get(window, "resetsAt") != null)
                        {
                            try { text.Append("; renova em " + new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(window["resetsAt"])).ToLocalTime().ToString("dd/MM HH:mm")); } catch (ArgumentOutOfRangeException) { }
                        }
                        text.AppendLine();
                    }
                }
                if (!found) text.AppendLine("Janelas de limite nao informadas.");
                if (Equals(Json.Get(limits.Data, "ordinaryUsageAllowed"), false)) text.AppendLine("A conta informa que o uso normal esta bloqueado.");
            }
            text.AppendLine(); text.AppendLine("ATIVIDADE DA CONTA");
            if (usage.Error != null) text.AppendLine("Indisponivel: " + usage.Error);
            else
            {
                var summary = Json.Object(usage.Data, "summary");
                text.AppendLine("Tokens acumulados: " + Metric(summary, "lifetimeTokens"));
                text.AppendLine("Maior uso diario: " + Metric(summary, "peakDailyTokens"));
                text.AppendLine("Dias consecutivos: " + Metric(summary, "currentStreakDays"));
            }
            return text.ToString().TrimEnd();
        }
        private static string WindowName(Dictionary<string, object> window, string fallback)
        {
            object minutes = Json.Get(window, "windowDurationMins");
            if (minutes == null) return fallback == "primary" ? "Janela principal" : "Janela secundaria";
            long value = Convert.ToInt64(minutes);
            return value % 1440 == 0 ? (value / 1440) + " dias" : value % 60 == 0 ? (value / 60) + " horas" : value + " minutos";
        }
        private static string Metric(Dictionary<string, object> data, string key)
        {
            object value = Json.Get(data, key);
            return value == null ? "Nao informado" : Convert.ToInt64(value).ToString("N0", CultureInfo.GetCultureInfo("pt-BR"));
        }
    }
}
