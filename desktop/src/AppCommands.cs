using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Negotchi
{
    internal sealed partial class NegotchiApp
    {
        private bool commandBusy;
        private SkillChoice pendingSkill;
        private string effectiveModel, effectiveEffort;

        private async void RunCommand(string text) { await ExecuteCommandAsync(text); }
        private void ClearPendingSkill()
        {
            if (pendingSkill != null) chat.RemoveSkillPrefix(pendingSkill.Name);
            pendingSkill = null; chat.SetSkill(null);
        }

        internal async Task ExecuteCommandAsync(string text)
        {
            SlashInvocation invocation;
            if (!SlashCommands.TryParse(text, out invocation)) return;
            if (busy || commandBusy) { chat.SetStatus("Aguarde o pedido atual terminar para executar um comando."); return; }
            if (invocation.Command == null)
            {
                AddSystem("Comando nao disponivel nesta interface: " + invocation.Name + ". Use /help para ver os comandos suportados."); Save(); return;
            }
            string name = invocation.Command.Name;
            if (invocation.Arguments.Length > 0 && name != "/model" && name != "/skills" && name != "/create-pet" && name != "/monitor")
            {
                AddSystem("Este comando nao recebe argumentos. Use " + name + "."); Save(); return;
            }
            if (name == "/new") { NewChat(); return; }
            if (name == "/pets") { OpenPetPicker(); return; }
            if (name == "/monitor")
            {
                string[] parts = invocation.Arguments.Split(new[] { '|' }, 2); int pid;
                if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out pid) || string.IsNullOrWhiteSpace(parts[1])) { AddSystem("Use /monitor PID | lembrete. Exemplo: /monitor 8420 | Fazer café."); Save(); return; }
                try { var process = System.Diagnostics.Process.GetProcessById(pid); state.ProcessReminders.Add(new ProcessReminder { ProcessId = pid, ProcessName = process.ProcessName, Message = parts[1].Trim() }); Save(); Status("Monitorando " + process.ProcessName + " (PID " + pid + ").", "waiting"); }
                catch (ArgumentException) { AddSystem("Não encontrei um processo ativo com esse PID."); Save(); }
                return;
            }
            if (name == "/import-pet") { ImportPet(); return; }
            if (name == "/create-pet") { CreatePet(invocation.Arguments); return; }
            if (name == "/login") { Connect(); return; }
            if (name == "/files") { OpenChat(); new FileExplorerWindow(chat, state.Folder, path => chat.PrefixInput("Arquivo: \"" + path + "\"\n"), state.Sandbox, context => chat.PrefixInput(context)).ShowDialog(); return; }
            if (name == "/diff") { OpenChat(); try { var conversation = state.ActiveConversation(); if (conversation.ReviewComments == null) conversation.ReviewComments = new List<ReviewComment>(); new DiffReviewWindow(chat, state.Folder, conversation.ReviewComments, context => chat.PrefixInput(context)).ShowDialog(); Save(); } catch (Exception error) { Status("Não foi possível revisar: " + error.Message, "failed"); } return; }
            commandBusy = true; chat.SetCommandBusy(true);
            string feedback = "Comando concluido.", animation = "idle";
            try
            {
                OpenChat();
                if (name == "/help")
                {
                    CommandDialogs.Info(chat, "Comandos do Codex", string.Join("\n\n", SlashCommands.All.Select(c => c.Name + (c.Aliases.Length > 0 ? " (" + string.Join(", ", c.Aliases) + ")" : "") + "\n" + c.Description)) + "\n\nVoce pode digitar / para ver sugestoes.\n/model tambem aceita: /model identificador [esforco].\n/skills aceita um termo de busca.\n\nOs comandos acima usam as operacoes nativas do App Server. Outros comandos da interface de terminal podem nao estar disponiveis aqui.");
                    return;
                }
                Status("Consultando o Codex...", "waiting");
                await agent.StartAsync(state.Folder);
                var commands = new CodexCommands(agent);
                if (name == "/terminal")
                {
                    if (state.Sandbox != "danger-full-access") throw new InvalidOperationException("O Windows Terminal abre um processo local e exige Acesso completo. Use /permissions para habilitar nesta conversa.");
                    if (!DesktopActions.Review(chat, "Abrir Windows Terminal", "Perfil: pwsh (perfil do usuário carregado)\nPasta: " + state.Folder + "\n\nO terminal será aberto fora do sandbox do App Server.")) { feedback = "Abertura do terminal cancelada."; return; }
                    DesktopActions.LaunchCommand("wt.exe", "-d " + QuoteTerminal(state.Folder) + " pwsh.exe", state.Folder); feedback = "Windows Terminal aberto com o perfil do pwsh.";
                }
                else if (name == "/model")
                {
                    var modelsTask = commands.Models(); var configTask = commands.ReadConfig(state.Folder); await Task.WhenAll(modelsTask, configTask);
                    var models = modelsTask.Result; var config = configTask.Result;
                    if (models.Count == 0) throw new InvalidOperationException("O Codex nao informou modelos disponiveis.");
                    string current = !string.IsNullOrEmpty(effectiveModel) ? effectiveModel : !string.IsNullOrEmpty(state.SelectedModel) ? state.SelectedModel : Json.Text(config, "model");
                    string effort = !string.IsNullOrEmpty(state.ReasoningEffort) ? state.ReasoningEffort : !string.IsNullOrEmpty(effectiveEffort) ? effectiveEffort : Json.Text(config, "model_reasoning_effort");
                    ModelSelection selection;
                    if (invocation.Arguments.Length > 0)
                    {
                        string[] args = invocation.Arguments.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (args.Length > 2) throw new ArgumentException("Use /model identificador [esforco].");
                        var model = models.FirstOrDefault(m => m.Model == args[0]);
                        if (model == null) throw new ArgumentException("Modelo fora do catalogo disponivel. Use /model para escolher.");
                        string chosenEffort = args.Length > 1 ? args[1] : model.DefaultEffort;
                        if (model.Efforts.Length > 0 && !model.Efforts.Contains(chosenEffort)) throw new ArgumentException("Esforco indisponivel para este modelo: " + chosenEffort);
                        if (model.Efforts.Length == 0 && args.Length > 1) throw new ArgumentException("Este modelo nao oferece ajuste de esforco.");
                        selection = new ModelSelection { Model = model.Model, Effort = model.Efforts.Length == 0 ? null : chosenEffort };
                    }
                    else selection = CommandDialogs.Model(chat, models, current, effort);
                    if (selection == null) { feedback = "Selecao de modelo mantida."; return; }
                    if (!string.IsNullOrEmpty(state.ThreadId)) { await EnsureThread(false); await agent.RequestAsync("thread/settings/update", CodexCommands.ModelSettings(state.ThreadId, selection)); }
                    state.SelectedModel = effectiveModel = selection.Model; state.ReasoningEffort = effectiveEffort = selection.Effort;
                    feedback = "Modelo: " + selection.Model + (string.IsNullOrEmpty(selection.Effort) ? "" : " | Esforco: " + selection.Effort); animation = "waving";
                }
                else if (name == "/skills")
                {
                    var skills = await commands.Skills(state.Folder);
                    var selected = CommandDialogs.Skills(chat, skills, invocation.Arguments);
                    if (selected == null) { feedback = "Selecao de skill mantida."; return; }
                    ClearPendingSkill(); pendingSkill = selected; chat.SetSkill(selected.DisplayName); chat.PrefixInput("$" + selected.Name + " ");
                    feedback = "Skill preparada para o proximo pedido: " + selected.DisplayName;
                }
                else if (name == "/account")
                {
                    new AccountWindow(chat, agent).ShowDialog(); feedback = "Perfil consultado.";
                }
                else if (name == "/usage")
                {
                    int revision = accountRevision;
                    var account = commands.Read("account/read", Json.Map("refreshToken", false));
                    var limits = commands.Read("account/rateLimits/read", null);
                    var usage = commands.Read("account/usage/read", null);
                    var config = commands.ReadConfig(state.Folder);
                    await Task.WhenAll(account, limits, usage, config);
                    if (revision == accountRevision)
                    {
                        if (account.Result.Data != null) UpdateAccount(Json.Get(account.Result.Data, "account") != null, Json.Text(Json.Object(account.Result.Data, "account"), "email"));
                        if (accountConnected) chat.SetAccountLimits(CodexCommands.AvailableLimitsText(limits.Result), limits.Result.Error);
                    }
                    string model = !string.IsNullOrEmpty(effectiveModel) ? effectiveModel : !string.IsNullOrEmpty(state.SelectedModel) ? state.SelectedModel : Json.Text(config.Result, "model");
                    string information = "Pasta: " + state.Folder + "\nConversa: " + (state.ThreadId ?? "Ainda nao iniciada") + "\nModelo: " + (string.IsNullOrEmpty(model) ? "Padrao do Codex" : model) + "\n\n";
                    CommandDialogs.Info(chat, "Uso e status do Codex", information + CodexCommands.UsageText(account.Result, limits.Result, usage.Result, state.TokenUsage));
                    feedback = "Uso consultado no Codex.";
                }
                else if (name == "/memories")
                {
                    var configTask = commands.ReadConfig(state.Folder); var statusTask = commands.Read("memory/status", Json.Map()); await Task.WhenAll(configTask, statusTask);
                    var current = CodexCommands.MemoryConfig(configTask.Result, state.ThreadMemoryMode);
                    var selection = CommandDialogs.Memories(chat, current, statusTask.Result);
                    if (selection == null) { feedback = "Configuracao de memorias mantida."; return; }
                    await commands.ApplyMemoryGlobals(current, selection);
                    if (selection.ThreadMode != null && selection.ThreadMode != state.ThreadMemoryMode)
                    {
                        try { await EnsureThread(false); await agent.RequestAsync("thread/memoryMode/set", Json.Map("threadId", state.ThreadId, "mode", selection.ThreadMode)); state.ThreadMemoryMode = selection.ThreadMode; }
                        catch (Exception error) { throw new InvalidOperationException("As opcoes globais foram aplicadas; nao consegui atualizar a permissao deste chat: " + error.Message); }
                    }
                    feedback = "Configuracoes de memoria aplicadas no Codex.";
                }
                else if (name == "/permissions")
                {
                    var sandbox = CommandDialogs.Permissions(chat, state.Sandbox);
                    if (sandbox == null) { feedback = "Permissões mantidas."; return; }
                    if (sandbox != state.Sandbox && sandbox != "danger-full-access" && !DesktopActions.Review(chat, "Alterar permissões da conversa", "Aplicar " + sandbox + " somente aos próximos pedidos desta conversa?\n\nAs outras conversas mantêm suas próprias permissões.")) { feedback = "Permissões mantidas."; return; }
                    state.Sandbox = sandbox; threadResumed = false;
                    feedback = sandbox == "danger-full-access" ? "Acesso completo ativado para esta conversa." : sandbox == "workspace-write" ? "Alterações de arquivos permitidas mediante confirmação." : "Conversa restrita à leitura.";
                }
                else if (name == "/mcp")
                {
                    new McpManagement(chat, agent).ShowDialog(); feedback = "Servidores MCP consultados.";
                }
            }
            catch (Exception error) { feedback = error.Message; animation = "failed"; AddSystem(error.Message); }
            finally { commandBusy = false; chat.SetCommandBusy(false); Status(feedback, animation); Save(); }
        }

        private static string QuoteTerminal(string value) { return "\"" + (value ?? "").Replace("\"", "\\\"") + "\""; }

    }
}
