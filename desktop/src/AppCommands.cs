using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
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
            if (invocation.Arguments.Length > 0 && name != "/model" && name != "/skills")
            {
                AddSystem("Este comando nao recebe argumentos. Use " + name + "."); Save(); return;
            }
            if (name == "/new") { NewChat(); return; }
            if (name == "/pets") { OpenPetPicker(); return; }
            if (name == "/import-pet") { ImportPet(); return; }
            if (name == "/login") { Connect(); return; }
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
                if (name == "/model")
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
                else if (name == "/usage")
                {
                    var account = commands.Read("account/read", Json.Map("refreshToken", false));
                    var limits = commands.Read("account/rateLimits/read", null);
                    var usage = commands.Read("account/usage/read", null);
                    var config = commands.ReadConfig(state.Folder);
                    await Task.WhenAll(account, limits, usage, config);
                    if (account.Result.Data != null) { accountConnected = Json.Get(account.Result.Data, "account") != null; chat.SetConnected(accountConnected); }
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
                else if (name == "/mcp")
                {
                    var details = new StringBuilder(); string cursor = null; var visited = new HashSet<string>();
                    do
                    {
                        var result = await agent.RequestAsync("mcpServerStatus/list", Json.Map("cursor", cursor, "limit", 100));
                        foreach (var server in Json.Items(result, "data").OfType<Dictionary<string, object>>())
                        {
                            details.AppendLine(Json.Text(server, "name") + " | " + Json.Text(server, "authStatus"));
                            var tools = Json.Object(server, "tools"); details.AppendLine("Ferramentas: " + (tools.Count == 0 ? "Nenhuma informada" : string.Join(", ", tools.Keys))); details.AppendLine();
                        }
                        cursor = Json.Text(result, "nextCursor"); if (cursor.Length > 0 && !visited.Add(cursor)) throw new InvalidOperationException("O Codex repetiu uma pagina de servidores MCP.");
                    } while (cursor.Length > 0);
                    CommandDialogs.Info(chat, "Conexoes MCP", details.Length == 0 ? "Nenhum servidor MCP informado pelo Codex." : details.ToString());
                    feedback = "Conexoes consultadas.";
                }
            }
            catch (Exception error) { feedback = error.Message; animation = "failed"; AddSystem(error.Message); }
            finally { commandBusy = false; chat.SetCommandBusy(false); Status(feedback, animation); Save(); }
        }

        private void ImportPet()
        {
            OpenChat();
            var manifestDialog = new Microsoft.Win32.OpenFileDialog { Title = "Escolha o pet.json", Filter = "Manifesto de pet (pet.json)|pet.json|JSON|*.json", CheckFileExists = true, Multiselect = false };
            if (manifestDialog.ShowDialog(chat) != true) return;
            var sheetDialog = new Microsoft.Win32.OpenFileDialog { Title = "Escolha o spritesheet do pet", Filter = "Imagens PNG|*.png|Todas as imagens|*.png;*.webp", CheckFileExists = true, Multiselect = false };
            if (sheetDialog.ShowDialog(chat) != true) return;
            try
            {
                var manifest = Json.Decode(File.ReadAllText(manifestDialog.FileName));
                string id = Json.Text(manifest, "id");
                if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("O pet.json precisa ter um id de pasta valido.");
                var frame = Json.Object(manifest, "frame"); int width = Convert.ToInt32(frame["width"]), height = Convert.ToInt32(frame["height"]), columns = Convert.ToInt32(frame["columns"]), rows = Convert.ToInt32(frame["rows"]);
                using (var image = Image.FromFile(sheetDialog.FileName))
                    if (image.Width != width * columns || image.Height != height * rows) throw new InvalidDataException("O spritesheet nao corresponde ao frame declarado no pet.json.");
                string petsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pets"); Directory.CreateDirectory(petsRoot);
                string destination = Path.Combine(petsRoot, id); Directory.CreateDirectory(destination);
                File.Copy(manifestDialog.FileName, Path.Combine(destination, "pet.json"), true);
                File.Copy(sheetDialog.FileName, Path.Combine(destination, "spritesheet.png"), true);
                File.Copy(sheetDialog.FileName, Path.Combine(destination, "preview.png"), true);
                AddSystem("Pet " + id + " importado. Recompile ou reinicie o app para ele aparecer na aba Pets."); Save();
            }
            catch (Exception error) { AddSystem("Nao foi possivel importar o pet: " + error.Message); Save(); }
        }
    }
}
