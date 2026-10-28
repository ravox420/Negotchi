using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal static partial class Tests
    {
        private static int assertions;
        private static string output;
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "app-server") return FakeServer();
            if (args.Length > 0 && args[0] == "--history-audit") return HistoryAudit().GetAwaiter().GetResult();
            if (args.Length > 0 && args[0] == "--spotify-status") return SpotifyStatus().GetAwaiter().GetResult();
            if (args.Length > 0 && args[0] == "--spotify-test")
            {
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                try { TestSpotifyPlayback().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + assertions + " verificacoes de Spotify e animacoes."); return 0; }
                catch (Exception error) { Console.Error.WriteLine(error); return 1; }
            }
            if (args.Length > 0 && args[0] == "--ui-smoke") return UiSmoke(Path.GetFullPath(args[1]));
            if (args.Length > 0 && args[0] == "--pet-ui-smoke") return PetCreationUiSmoke(Path.GetFullPath(args[1]));
            output = Path.GetFullPath(args.Length > 0 ? args[0] : ".build");
            Directory.CreateDirectory(output);
            try
            {
                TestAssets(); TestCatalog(); TestFiles(); TestState(); TestPosition(); TestSlashCommands(); TestNormalization(); TestGitGraph(); TestPetCreation(); TestSpotifyPlayback().GetAwaiter().GetResult(); TestWorkspace().GetAwaiter().GetResult(); TestProtocol().GetAwaiter().GetResult(); TestCommandProtocol().GetAwaiter().GetResult(); TestNativeProtocol().GetAwaiter().GetResult(); TestViews(); TestAccountCharts();
                File.WriteAllText(Path.Combine(output, "test-results.json"), Json.Encode(Json.Map("passed", assertions, "failed", 0, "nativeProtocol", true, "liveModelTested", false)), Encoding.UTF8);
                Console.WriteLine("PASS: " + assertions + " verificacoes. Previews: .build/chat-preview.png e .build/pet-preview.png");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static void Check(bool value, string description)
        {
            if (!value) throw new Exception("FAIL: " + description);
            assertions++; Console.WriteLine("PASS: " + description);
        }
        private static void TestAccountCharts()
        {
            var window = new AccountWindow(null, null, null);
            var account = new RpcRead { Data = Json.Map("account", Json.Map("email", "teste@example.com", "planType", "plus")) };
            var limits = new RpcRead { Data = Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", 25.5, "windowDurationMins", 300), "secondary", Json.Map("usedPercent", 60, "windowDurationMins", 10080))) };
            var usage = new RpcRead { Data = Json.Map("summary", Json.Map("lifetimeTokens", 12345), "dailyUsageBuckets", new object[] { Json.Map("startDate", "2026-10-27", "tokens", 100), Json.Map("startDate", "2026-10-26", "tokens", 0) }) };
            var tokens = Json.Map("total", Json.Map("totalTokens", 300, "inputTokens", 200, "outputTokens", 100));
            window.ShowData(account, limits, usage, tokens);
            Render((FrameworkElement)window.Content, 620, 600, Path.Combine(output, "account-charts-preview.png"));
            var bars = Descendants<ProgressBar>((FrameworkElement)window.Content).ToArray();
            Check(bars.Length == 6 && bars[0].Value == 25.5 && bars[2].Value == 0 && bars[3].Value == 100 && bars[4].Maximum == 300, "graficos preservam percentuais, datas, zero e divisao dos tokens da conversa");
            var labels = Descendants<TextBlock>((FrameworkElement)window.Content).Select(label => label.Text).ToArray();
            Check(labels.Any(label => label.Contains("26/10/2026")) && labels.Any(label => label.Contains("12.345")), "atividade apresenta datas e totais da conta");
            window.ShowData(account, new RpcRead { Error = "limites indisponíveis" }, new RpcRead { Error = "atividade indisponível" }, null);
            Render((FrameworkElement)window.Content, 620, 600, Path.Combine(output, "account-unavailable-preview.png"));
            labels = Descendants<TextBlock>((FrameworkElement)window.Content).Select(label => label.Text).ToArray();
            Check(!Descendants<ProgressBar>((FrameworkElement)window.Content).Any() && labels.Any(label => label.Contains("limites indisponíveis")) && labels.Any(label => label.Contains("atividade indisponível")) && labels.Any(label => label.Contains("não informado")), "graficos distinguem falhas e ausencia de dados de consumo zero");
            window.Close();
        }
        private static async Task<int> HistoryAudit()
        {
            using (var client = new CodexClient())
            {
                await client.StartAsync(Environment.CurrentDirectory);
                var history = new ConversationHistory(client);
                var sessions = await history.List(false, CancellationToken.None);
                int messages = sessions.Count == 0 ? 0 : (await history.Messages(sessions[0].Id, "Codex")).Count;
                Console.WriteLine(Json.Encode(Json.Map("sessions", sessions.Count, "folders", sessions.Select(session => session.Folder).Distinct().Count(), "sampleMessages", messages)));
                return 0;
            }
        }
        private static void Throws(Action action, string description)
        {
            try { action(); } catch (ArgumentException) { Check(true, description); return; } catch (FileNotFoundException) { Check(true, description); return; }
            throw new Exception("FAIL: " + description);
        }
        private static void TestAssets()
        {
            var assets = new PetAssets();
            Check(assets.Frames.Length == 72, "recorta a grade de 8 por 9");
            Check(assets.Frames[0].PixelWidth == 192 && assets.Frames[0].PixelHeight == 208, "preserva dimensoes dos quadros");
            Check(assets.Animations.Count == 14, "carrega nove animacoes e cinco aliases");
            Check(assets.Animations.Values.SelectMany(t => t.Frames).Distinct().Count() == 57, "usa os 57 quadros originais");
            Check(assets.Animations["running-right"].Frames.SequenceEqual(assets.Animations["move_right"].Frames), "preserva aliases");
            var pixels = new byte[192 * 208 * 4];
            var image = new FormatConvertedBitmap(assets.Frames[0], PixelFormats.Bgra32, null, 0); image.CopyPixels(pixels, 192 * 4, 0);
            Check(Enumerable.Range(0, 192 * 208).Any(i => pixels[i * 4 + 3] == 0), "mantem transparencia do personagem");
        }
        private static void TestFiles()
        {
            string folder = Path.Combine(output, "file-tests"); Directory.CreateDirectory(folder);
            string document = Path.Combine(folder, "anotacoes & teste.txt"); File.WriteAllText(document, "PimpDogg");
            Check(DesktopActions.ResolvePath("anotacoes & teste.txt", folder) == document, "resolve caminhos relativos com caracteres de shell como dados");
            Check(DesktopActions.ResolvePath("\"" + document + "\"", folder) == document, "aceita caminhos entre aspas");
            Throws(() => DesktopActions.ResolvePath("https://example.org", folder), "recusa URLs no abridor de arquivos");
            Throws(() => DesktopActions.ResolvePath("\\\\servidor\\arquivo.txt", folder), "recusa caminhos de rede");
            Throws(() => DesktopActions.ResolvePath("\\\\?\\C:\\arquivo.txt", folder), "recusa caminhos de dispositivo");
            Throws(() => DesktopActions.ResolvePath("ausente.txt", folder), "informa arquivo inexistente");
            Throws(() => DesktopActions.ResolvePath(document + ":script", folder), "recusa fluxos alternativos NTFS");
            string opened = null;
            string result = DesktopActions.Open(document, folder, path => { throw new Exception("Documento nao deveria pedir confirmacao."); }, path => opened = path);
            Check(opened == document && result.StartsWith("Aberto"), "abre documento por caminho exato, sem comando de shell");
            string script = Path.Combine(folder, "acao.ps1"); File.WriteAllText(script, "exit"); opened = null;
            result = DesktopActions.Open(script, folder, path => false, path => opened = path);
            Check(opened == null && result.Contains("recusada"), "nao executa script quando a abertura e recusada");
            DesktopActions.Open(script, folder, path => true, path => opened = path);
            Check(opened == script, "permite abrir programa somente apos revisao");
            Check(!DesktopActions.NeedsConfirmation(folder), "abre pastas diretamente");
        }
        private static void TestNormalization()
        {
            var command = AgentResponseNormalizer.Normalize("item/commandExecution/requestApproval", Json.Map("command", "dotnet test", "cwd", "C:\\repo", "reason", "Validar", "commandActions", new object[] { Json.Map("type", "read") }), Json.Map());
            Check(command.Kind == ApprovalKind.CommandExecution && command.Command == "dotnet test" && command.WorkingDirectory == "C:\\repo" && command.Permissions.Contains("read"), "normaliza aprovacao de comando");
            var files = AgentResponseNormalizer.Normalize("item/fileChange/requestApproval", Json.Map("reason", "Aplicar", "grantRoot", "C:\\repo"), Json.Map("changes", new object[] { Json.Map("path", "src\\App.cs", "kind", "update", "diff", "- antigo\n+ novo") }));
            Check(files.Kind == ApprovalKind.FileChange && files.FileChanges.Count == 1 && files.FileChanges[0].Path == "src\\App.cs", "normaliza alteracoes de arquivos");
            var permissions = AgentResponseNormalizer.Normalize("item/permissions/requestApproval", Json.Map("permissions", Json.Map("network", Json.Map("host", "example.com"), "filesystem", new object[] { "C:\\repo" })), null);
            Check(permissions.Kind == ApprovalKind.Permissions && permissions.Permissions.Contains("network") && permissions.Permissions.Contains("filesystem"), "normaliza permissoes solicitadas");
            var unknown = AgentResponseNormalizer.Normalize("future/request", Json.Map(), null);
            Check(unknown.Kind == ApprovalKind.Unknown && unknown.RawParameters != null, "preserva solicitacao desconhecida para diagnostico");
        }
        private static void TestGitGraph()
        {
            string repository = Path.Combine(output, "git-unicode-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(repository);
            GitWindow.Run(repository, "init");
            GitWindow.Run(repository, "-c user.name=Teste -c user.email=teste@example.invalid commit --allow-empty -m \"Ação, revisão e café 😀 🚀\"");
            string subject = GitWindow.Run(repository, "log -1 --format=%s").Trim();
            Check(subject == "A\u00e7\u00e3o, revis\u00e3o e caf\u00e9 \U0001f600 \U0001f680", "preserva acentos e emojis na saida real do Git e na compilacao das fontes");
            string remote = Path.Combine(output, "git-push-remote-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(remote); GitWindow.Run(remote, "init --bare");
            GitWindow.Run(repository, "remote add origin \"" + remote + "\""); string branch = GitWindow.Run(repository, "symbolic-ref --short HEAD").Trim();
            GitWindow.Run(repository, GitWindow.PushArguments("origin", branch));
            Check(GitWindow.Run(remote, "rev-parse refs/heads/" + branch).Trim() == GitWindow.Run(repository, "rev-parse HEAD").Trim(), "push publica somente a branch escolhida em remoto local de teste");
            Check(!GitWindow.RedactRemote("https://user:secret@example.com/repo.git").Contains("secret"), "revisao do push nao mostra credenciais da URL");
            var graph = GitGraph.Read("a1\u001fb1 c1\u001fHEAD -> main\u001fMerge branch\u001eb1\u001fd1\u001f\u001fPrimeiro\u001ec1\u001fd1\u001ffeature\u001fFeature\u001ed1\u001f\u001f\u001fBase\u001e");
            Check(graph.Count == 4 && graph[0].Parents.Length == 2 && graph[2].Lane != graph[1].Lane, "organiza commits e ramificacoes em trilhas do grafo Git");
            var view = GitGraph.Draw(graph); Render(view, 520, 160, Path.Combine(output, "git-graph-preview.png"));
            Check(view.Children.Count > graph.Count && File.Exists(Path.Combine(output, "git-graph-preview.png")), "desenha nos e ligacoes do grafo Git");
        }
        private static void TestCatalog()
        {
            var catalog = new PetCatalog();
            Check(catalog.Pets.Length == 10, "inclui PimpDogg, Guts e os oito pets importados");
            Check(!Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("Negotchi.pet.json"), "usa somente os pets da pasta pets, sem manifesto padrao separado");
            Check(catalog.Find("ausente").Id == "PimpDogg" && catalog.Find(null).Id == "PimpDogg", "usa PimpDogg quando a escolha salva nao existe");
            Check(catalog.Find("DEWEY").Id == "dewey", "identifica pets sem diferenciar maiusculas");
            foreach (var definition in catalog.Pets)
            {
                var assets = definition.Load();
                Check(assets.Id == definition.Id && assets.DisplayName == definition.DisplayName && definition.Preview.PixelWidth > 0 && definition.Preview.PixelHeight > 0, "carrega manifesto e previa: " + definition.DisplayName);
                var used = assets.Animations.Values.SelectMany(track => track.Frames).Distinct().ToArray();
                bool visible = true;
                foreach (int index in used)
                {
                    var pixels = new byte[192 * 208 * 4];
                    new FormatConvertedBitmap(assets.Frames[index], PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 192 * 4, 0);
                    if (!Enumerable.Range(0, 192 * 208).Any(i => pixels[i * 4 + 3] > 12)) visible = false;
                }
                Check(visible && used.Length == (assets.Id == "Guts" ? 72 : 57) + (assets.Animations.ContainsKey("dancing") ? 8 : 0), "animacoes nao incluem celulas vazias: " + definition.DisplayName);
                if (assets.Id != "PimpDogg") Check(assets.Animations["running-right"].Frames[0] == 8 && assets.Animations["review"].Frames[0] == 64, "respeita o padding entre estados: " + definition.DisplayName);
            }
            string instructions = Json.Text(NegotchiApp.ThreadOptions(output, "Dewey"), "developerInstructions");
            Check(instructions.StartsWith("Voce e Dewey,") && !instructions.Contains("{PET_NAME}"), "usa o pet escolhido nas instrucoes da conversa");
        }
        private static void TestState()
        {
            string directory = Path.Combine(output, "state-tests"); Directory.CreateDirectory(directory);
            var store = new StateStore(directory);
            var state = new AppState { X = -350, Y = 120, Scale = 1.25, Folder = output, ThreadId = "conversa-1", SelectedPetId = "dewey", SelectedModel = "model-a", ReasoningEffort = "medium", ThreadMemoryMode = "disabled" };
            state.Messages.Add(new ChatMessage { Role = "user", Text = "Voce consegue ler acentuacao? cafe, acoes, coracao." });
            state.Appearance = Theme.Preset("light"); state.Appearance.FontFamily = "Consolas"; state.Appearance.FontSize = 17; state.Appearance.Colors["Background"] = "#EFF5F9";
            store.Save(state); state.Y = 140; for (int save = 0; save < 30; save++) store.Save(state);
            var restored = store.Load();
            Check(restored.X == -350 && restored.Y == 140 && restored.Scale == 1.25, "salva posicao e tamanho com substituicao atomica");
            Check(restored.ThreadId == "conversa-1" && restored.Messages[0].Text == state.Messages[0].Text, "preserva conversa e caracteres");
            Check(restored.SelectedPetId == "dewey", "restaura o pet selecionado ao reabrir");
            Check(restored.SelectedModel == "model-a" && restored.ReasoningEffort == "medium" && restored.ThreadMemoryMode == "disabled", "restaura escolhas de modelo, esforco e memoria");
            Check(restored.Appearance.Mode == "light" && restored.Appearance.FontFamily == "Consolas" && restored.Appearance.FontSize == 17 && restored.Appearance.Colors["Background"] == "#EFF5F9", "restaura tema, fonte, tamanho e cores personalizadas");
            File.WriteAllText(Path.Combine(directory, "state.json"), "arquivo quebrado");
            Check(store.Load().Messages.Count == 0, "recupera estado corrompido sem impedir inicializacao");
            File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Scale\":99,\"Messages\":null}");
            Check(store.Load().Scale == 1.8, "normaliza tamanho fora do intervalo");
            File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Messages\":[{\"Role\":\"assistant\",\"Text\":\"Resposta antiga\"}]}");
            Check(store.Load().Messages[0].PetName == "PimpDogg", "preserva a autoria de mensagens de versoes anteriores");
            Check(store.Load().Appearance.Mode == "dark" && store.Load().Appearance.Colors.Count == Theme.Colors.Length, "usa a paleta padrao em preferencias antigas sem aparencia");
            File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Appearance\":{\"FontSize\":999,\"Colors\":{\"Background\":\"invalida\"}}}");
            restored = store.Load();
            Check(restored.Appearance.FontSize == 22 && restored.Appearance.Colors["Background"] == "#17131C", "recupera cores invalidas e limita tamanho da fonte ao carregar");
            var history = new AppState { Folder = directory, Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Text = "primeira" } } };
            history.EnsureConversations(); string first = history.ActiveConversationId;
            history.Conversations.Add(new ConversationState { Id = "segunda", Title = "Segunda conversa", Folder = directory, Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Text = "segunda" } } });
            history.SaveActiveConversation(); history.ActiveConversationId = "segunda"; history.LoadActiveConversation(); store.Save(history); restored = store.Load();
            Check(restored.Conversations.Count == 2 && restored.ActiveConversationId == "segunda" && restored.Messages[0].Text == "segunda" && restored.Conversations.Any(c => c.Id == first && c.Messages[0].Text == "primeira"), "preserva historico de conversas e conversa ativa");
            restored.Conversations[0].Archived = true;
            Check(restored.Conversations[0].Matches("primeira") && !restored.Conversations[0].Matches("ausente"), "busca conversas locais por titulo e conteudo salvo");
            store.Save(restored); restored = store.Load();
            Check(restored.Conversations.Any(c => c.Archived), "preserva conversas arquivadas sem apagar o historico");
            string projectId = restored.ActiveProjectId;
            restored.Projects.Add(new ProjectState { Id = "trabalho", Name = "Trabalho" }); restored.ActiveProjectId = "trabalho";
            restored.Conversations.Add(new ConversationState { Id = "terceira", ProjectId = "trabalho", Title = "Terceira conversa", Folder = directory, Messages = new List<ChatMessage>() });
            restored.ActiveConversationId = "terceira"; restored.LoadActiveConversation();
            store.Save(restored); restored = store.Load();
            Check(restored.Projects.Count == 2 && restored.ActiveProjectId == "trabalho" && restored.Conversations.Any(c => c.ProjectId == "trabalho"), "preserva projetos e a associacao de cada conversa");
            Check(restored.Conversations.First(c => c.Id == first).ProjectId == projectId, "atribui conversas existentes ao projeto padrao na migracao");
            var largeHistory = new AppState { Folder = directory };
            largeHistory.Messages.Add(new ChatMessage { Role = "assistant", Text = new string('a', 2200000) + " 😀", PetName = "Codex" });
            store.Save(largeHistory);
            Check(store.Load().Messages[0].Text == largeHistory.Messages[0].Text, "reabre historicos acima de 2 MB usando o mesmo limite do salvamento");
            var policies = new AppState { Folder = directory, Sandbox = "danger-full-access" }; policies.EnsureConversations(); policies.SaveActiveConversation(); string privileged = policies.ActiveConversationId;
            policies.Conversations.Add(new ConversationState { Id = "safe", Folder = directory, Sandbox = "read-only", Messages = new List<ChatMessage>() }); policies.ActiveConversationId = "safe"; policies.LoadActiveConversation();
            Check(policies.Sandbox == "read-only", "nova conversa nao herda acesso completo de outra conversa");
            store.Save(policies); policies = store.Load(); policies.ActiveConversationId = privileged; policies.LoadActiveConversation();
            Check(policies.Sandbox == "danger-full-access" && policies.Conversations.First(conversation => conversation.Id == "safe").Sandbox == "read-only", "permissoes independentes sobrevivem ao reinicio e a troca de conversa");
        }
        private static void TestPosition()
        {
            var clamped = PetWindow.Clamp(new Rect(1900, 1100, 192, 252), new Rect(0, 0, 1920, 1040));
            Check(clamped.Right <= 1920 && clamped.Bottom <= 1040, "mantem pet dentro da area util");
            clamped = PetWindow.Clamp(new Rect(-2100, -200, 192, 252), new Rect(-1920, 0, 1920, 1040));
            Check(clamped.Left == -1920 && clamped.Top == 0, "aceita monitores com coordenadas negativas");
        }
        private static async Task TestProtocol()
        {
            string old = Environment.GetEnvironmentVariable("NEGOTCHI_CODEX_PATH");
            Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", Assembly.GetExecutingAssembly().Location);
            try
            {
                using (var client = new CodexClient())
                {
                    var completed = new TaskCompletionSource<bool>(); var delta = new StringBuilder();
                    bool toolCalled = false;
                    client.HandleRequest = async delegate(string method, Dictionary<string, object> parameters) {
                        await Task.Delay(50); toolCalled = method == "item/tool/call" && Json.Text(parameters, "tool") == "negotchi_open_path";
                        return Json.Map("success", true, "contentItems", new[] { Json.Map("type", "inputText", "text", "Aberto." ) });
                    };
                    client.Notification += delegate(string method, Dictionary<string, object> parameters) {
                        if (method == "item/agentMessage/delta") delta.Append(Json.Text(parameters, "delta"));
                        if (method == "turn/completed") completed.TrySetResult(true);
                    };
                    await Task.WhenAll(client.StartAsync(output), client.StartAsync(output));
                    Check(client.IsConnected, "inicializa uma unica conexao para chamadas simultaneas");
                    var thread = await client.RequestAsync("thread/start", NegotchiApp.ThreadOptions(output));
                    Check(Json.Text(Json.Object(thread, "thread"), "id") == "thread-fake", "envia ferramenta dinamica e politica de permissao");
                    string text = "Ola! Acentuacao: açao, café, coração. 😀 🚀\nAbra o arquivo & mantenha $(literal).";
                    var result = await client.RequestAsync("turn/start", Json.Map("threadId", "thread-fake", "input", new[] { Json.Map("type", "text", "text", text) }));
                    Check(Json.Text(Json.Object(result, "turn"), "echo") == text, "transmite UTF-8 e quebras de linha sem interpretacao de shell");
                    Check(await Task.WhenAny(completed.Task, Task.Delay(5000)) == completed.Task, "recebe conclusao apos ferramenta de desktop");
                    Check(toolCalled && delta.ToString() == "Arquivo aberto.", "responde chamadas do servidor sem bloquear streaming");
                    var resume = await client.RequestAsync("thread/resume", Json.Map("threadId", "thread-fake"));
                    Check(Json.Text(Json.Object(resume, "thread"), "id") == "thread-fake", "retoma conversa existente");
                    bool failed = false;
                    try { await client.RequestAsync("unknown/method", Json.Map()); } catch (InvalidOperationException error) { failed = error.Message == "Metodo desconhecido"; }
                    Check(failed, "propaga erros JSON-RPC para a interface");
                    await client.RequestAsync("turn/interrupt", Json.Map("threadId", "thread-fake", "turnId", "turn-fake"));
                    Check(true, "envia interrupcao de turno");
                }
            }
            finally { Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", old); }
        }
        private static void TestSlashCommands()
        {
            SlashInvocation invocation;
            Check(!SlashCommands.TryParse("Explique /model", out invocation), "mantem perguntas comuns na conversa");
            Check(SlashCommands.TryParse(" /MODEL model-a medium ", out invocation) && invocation.Command.Name == "/model" && invocation.Arguments == "model-a medium", "separa comando e argumentos sem interpretacao de shell");
            Check(SlashCommands.TryParse("/status", out invocation) && invocation.Command.Name == "/usage", "resolve alias de status");
            Check(SlashCommands.TryParse("/nao-existe", out invocation) && invocation.Command == null, "reconhece comando desconhecido sem trata-lo como pergunta");
            Check(SlashCommands.Match("/mo").Any(command => command.Name == "/model") && SlashCommands.Match("/reason")[0].Name == "/model", "filtra sugestoes por nome e alias");
            var disabled = new SkillChoice { Name = "teste", Path = "skill.md", Enabled = false };
            bool rejected = false; try { CodexCommands.TurnInput("teste", disabled); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "nao anexa skill desativada");
            var skill = new SkillChoice { Name = "teste", Path = @"C:\skills\teste\SKILL.md", Enabled = true };
            var input = CodexCommands.TurnInput("$teste explique", skill);
            Check(input.Count == 2 && Json.Text((Dictionary<string, object>)input[1], "type") == "skill" && Json.Text((Dictionary<string, object>)input[1], "path") == skill.Path, "anexa a skill como entrada estruturada nativa");
            var empty = new RpcRead { Data = Json.Map() };
            string usage = CodexCommands.UsageText(empty, empty, new RpcRead { Error = "Sem autenticacao" }, null);
            Check(usage.Contains("Ainda nao informados") && usage.Contains("Sem autenticacao") && !usage.Contains("Tokens acumulados: 0"), "distingue uso indisponivel de consumo zero");
            var tokens = Json.Map("total", Json.Map("totalTokens", 12345L, "inputTokens", 10000L, "outputTokens", 2345L), "last", Json.Map("totalTokens", 100L), "modelContextWindow", 200000L);
            usage = CodexCommands.UsageText(empty, new RpcRead { Data = Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", 25, "windowDurationMins", 300))) }, empty, tokens);
            Check(usage.Contains("12.345") && usage.Contains("5 horas: 25% usado"), "formata tokens reais e janelas de limites");
            var limits = new RpcRead { Data = Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", 25, "windowDurationMins", 300), "secondary", Json.Map("usedPercent", 60, "windowDurationMins", 10080))) };
            string available = CodexCommands.AvailableLimitsText(limits);
            Check(available == "Disponivel: 75% (5 horas) | 40% (7 dias)", "mostra o percentual restante de cada janela da conta");
            limits.Data = Json.Map("rateLimitsByLimitId", Json.Map("codex", Json.Map("limitId", "codex", "primary", Json.Map("usedPercent", 100)), "extra", Json.Map("limitName", "Extra", "primary", Json.Map("usedPercent", 12.5))));
            available = CodexCommands.AvailableLimitsText(limits);
            Check(available.Contains("codex: 0%") && available.Contains("Extra: 87,5%"), "preserva limites separados e percentuais fracionarios");
            Check(CodexCommands.AvailableLimitsText(empty) == "Limites nao informados" && CodexCommands.AvailableLimitsText(new RpcRead { Error = "Falha" }) == "Limites indisponiveis", "nao inventa saldo quando o servico nao informa limites");
            Check(CodexCommands.AvailableLimitsText(new RpcRead { Data = Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", null), "secondary", Json.Map("usedPercent", double.NaN))) }) == "Limites nao informados", "ignora percentuais ausentes ou invalidos");
            var previous = new MemoryPreferences { FeatureEnabled = null, UseMemories = true, GenerateMemories = false };
            Check(CodexCommands.MemoryEdits(previous, new MemoryPreferences { FeatureEnabled = null, UseMemories = true, GenerateMemories = false }).Length == 0, "nao regrava preferencias de memoria sem alteracao");
            Check(CodexCommands.MemoryEdits(previous, new MemoryPreferences { FeatureEnabled = true, UseMemories = null, GenerateMemories = false }).Length == 1, "mantem opcoes indeterminadas e altera apenas a escolha explicita");
        }
        private static async Task TestCommandProtocol()
        {
            string previousExecutable = Environment.GetEnvironmentVariable("NEGOTCHI_CODEX_PATH");
            Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", Assembly.GetExecutingAssembly().Location);
            try
            {
                using (var client = new CodexClient())
                {
                    await client.StartAsync(output); var commands = new CodexCommands(client);
                    var models = await commands.Models();
                    Check(models.Count == 2 && models[1].Model == "model-b", "pagina o catalogo nativo e exclui modelos ocultos");
                    Check(models[1].Efforts.SequenceEqual(new[] { "medium", "high" }), "carrega esforcos oferecidos pelo catalogo");
                    var skills = await commands.Skills(output);
                    Check(skills.Count == 2 && skills.Any(s => !s.Enabled), "consulta skills habilitadas e desativadas na pasta atual");
                    var history = new ConversationHistory(client);
                    var sessions = await history.List(false, CancellationToken.None);
                    Check(sessions.Count == 2 && sessions.Any(session => session.Folder == "D:\\outro-projeto") && sessions[0].DisplayName == "Revisão 😀", "lista todas as paginas e pastas do resume --all com UTF-8");
                    var archived = await history.List(true, CancellationToken.None);
                    Check(archived.All(session => session.Archived), "consulta sessoes arquivadas separadamente");
                    var messages = await history.Messages("history-1", "Codex");
                    Check(messages.Count == 2 && messages[0].Role == "user" && messages[1].Text == "Resposta final 😀", "le historico paginado completo em ordem antes de retomar");
                    await client.RequestAsync("test/history", Json.Map("repeat", true));
                    bool repeated = false; try { await history.List(false, CancellationToken.None); } catch (InvalidOperationException) { repeated = true; }
                    Check(repeated, "interrompe paginacao repetida em vez de perder ou duplicar sessoes");
                    await client.RequestAsync("test/history", Json.Map("repeat", false));
                    await history.Rename("history-1", "Nome revisado 😀"); await history.Archive("history-1", true); await history.Archive("history-1", false);
                    var historyCalls = await client.RequestAsync("test/state", null);
                    Check(Json.Text(Json.Object(historyCalls, "renamed"), "name") == "Nome revisado 😀" && Json.Items(historyCalls, "calls").Contains("thread/archive") && Json.Items(historyCalls, "calls").Contains("thread/unarchive"), "renomeia e arquiva usando operacoes nativas do Codex");
                    await client.RequestAsync("thread/settings/update", CodexCommands.ModelSettings("thread-fake", new ModelSelection { Model = "model-b", Effort = "medium" }));
                    var current = CodexCommands.MemoryConfig(await commands.ReadConfig(output), null);
                    await commands.ApplyMemoryGlobals(current, new MemoryPreferences { FeatureEnabled = true, UseMemories = false, GenerateMemories = false });
                    var config = await commands.ReadConfig(output);
                    Check(Equals(Json.Get(Json.Object(config, "features"), "memories"), true) && Equals(Json.Get(Json.Object(config, "memories"), "use_memories"), false), "aplica preferencias de memoria pela API de configuracao");
                    var captured = await client.RequestAsync("test/state", null);
                    Check(Json.Text(Json.Object(captured, "modelSettings"), "model") == "model-b" && Json.Text(Json.Object(captured, "modelSettings"), "effort") == "medium", "envia mudanca de modelo e esforco ao servidor");
                    var unavailable = await commands.Read("unknown/method", Json.Map());
                    Check(unavailable.Data == null && unavailable.Error == "Metodo desconhecido", "preserva o erro quando uma consulta nativa nao esta disponivel");
                }
            }
            finally { Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", previousExecutable); }
        }
        private static async Task TestNativeProtocol()
        {
            string oldHome = Environment.GetEnvironmentVariable("CODEX_HOME");
            string temporaryHome = Path.Combine(output, "codex-test-home"); Directory.CreateDirectory(temporaryHome);
            Environment.SetEnvironmentVariable("CODEX_HOME", temporaryHome);
            try
            {
                using (var client = new CodexClient())
                {
                    await client.StartAsync(output);
                    var account = await client.RequestAsync("account/read", Json.Map("refreshToken", false));
                    Check(client.IsConnected && account.ContainsKey("account"), "handshake e consulta de conta com codex.exe real, sem inferencia");
                    var result = await client.RequestAsync("thread/start", NegotchiApp.ThreadOptions(output));
                    Check(Json.Text(Json.Object(result, "thread"), "id").Length > 0, "Codex real aceita o manifesto da ferramenta e as opcoes da conversa");
                    var commands = new CodexCommands(client); var models = await commands.Models();
                    Check(models.Count > 0 && models.All(m => m.Efforts != null), "consulta modelos do Codex real sem inferencia");
                    var skills = await commands.Skills(output);
                    Check(skills.All(s => s.Name.Length > 0 && s.Path.Length > 0), "consulta o catalogo real de skills");
                    Check(skills.Any(s => s.Name == "create-pet" && s.Enabled), "Codex real descobre a skill create-pet neste projeto");
                    string thread = Json.Text(Json.Object(result, "thread"), "id");
                    await client.RequestAsync("thread/settings/update", CodexCommands.ModelSettings(thread, new ModelSelection { Model = models[0].Model, Effort = models[0].DefaultEffort }));
                    Check(true, "Codex real aceita a troca de modelo e esforco");
                    var memory = CodexCommands.MemoryConfig(await commands.ReadConfig(output), null);
                    await commands.ApplyMemoryGlobals(memory, new MemoryPreferences { FeatureEnabled = true, UseMemories = false, GenerateMemories = false });
                    var saved = CodexCommands.MemoryConfig(await commands.ReadConfig(output), null);
                    Check(saved.FeatureEnabled == true && saved.UseMemories == false && saved.GenerateMemories == false, "salva memorias apenas no CODEX_HOME isolado do teste");
                    await client.RequestAsync("thread/memoryMode/set", Json.Map("threadId", thread, "mode", "disabled"));
                    var memoryStatus = await client.RequestAsync("memory/status", Json.Map());
                    Check(memoryStatus.ContainsKey("v2Ready"), "consulta status real e altera permissao de memoria do chat");
                    var nativeHistory = await new ConversationHistory(client).List(false, CancellationToken.None);
                    Check(nativeHistory.All(entry => !string.IsNullOrEmpty(entry.Id)), "consulta resume --all pelo protocolo real instalado");
                    var mcp = await McpConfiguration.Read(client);
                    mcp.Servers["negotchi_sequence_test"] = Json.Map("command", "negotchi-test-never-executed", "enabled", false); await mcp.Write(client);
                    mcp = await McpConfiguration.Read(client);
                    Check(mcp.Servers.ContainsKey("negotchi_sequence_test") && Equals(Json.Get((Dictionary<string, object>)mcp.Servers["negotchi_sequence_test"], "enabled"), false), "configura MCP desativado no CODEX_HOME isolado pelo protocolo real");
                    mcp.Servers.Remove("negotchi_sequence_test"); await mcp.Write(client);
                    Check(!(await McpConfiguration.Read(client)).Servers.ContainsKey("negotchi_sequence_test"), "remove MCP sem alterar os outros ajustes de configuracao");
                }
            }
            finally { Environment.SetEnvironmentVariable("CODEX_HOME", oldHome); }
        }
        private static void TestViews()
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            Theme.Apply(Theme.Preset("dark"));
            string unicode = "Ação, não, revisão, café, coração — 😀 🚀 👁️👄👁️";
            Check(unicode == "A\u00e7\u00e3o, n\u00e3o, revis\u00e3o, caf\u00e9, cora\u00e7\u00e3o \u2014 \U0001f600 \U0001f680 \U0001f441\ufe0f\U0001f444\U0001f441\ufe0f", "compila literais UTF-8 sem depender da pagina de codigo do Windows");
            var unicodePreview = new StackPanel { Background = Theme.Card, Margin = new Thickness(12) };
            foreach (string family in new[] { "Rubik", "Consolas", "Segoe UI" })
                unicodePreview.Children.Add(new TextBox { Text = family + ": " + unicode, IsReadOnly = true, FontFamily = Theme.FontWithFallback(family), FontSize = 20, Foreground = Theme.Text, Background = Theme.Card, Padding = new Thickness(8), BorderThickness = new Thickness(0) });
            Render(unicodePreview, 900, 170, Path.Combine(output, "unicode-preview.png"));
            var assets = new PetAssets(); var chat = new ChatWindow(assets); chat.SetFolder(@"C:\Users\Voce\Documents");
            var resumeEntries = new List<HistoryConversation> {
                new HistoryConversation { Id = "sessao-1", Title = "Revisar autenticação e permissões", Folder = @"C:\projetos\api", UpdatedAt = 1792800000 },
                new HistoryConversation { Id = "sessao-2", Title = "Ajustar grafo de branches 😀", Folder = @"D:\projetos\Negotchi", UpdatedAt = 1792700000 }
            };
            var resumePicker = new ConversationPicker(null, @"C:\projetos\api", (archived, cancel) => Task.FromResult(resumeEntries));
            ((Task)typeof(ConversationPicker).GetMethod("Reload", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(resumePicker, null)).GetAwaiter().GetResult();
            Render((FrameworkElement)resumePicker.Content, 910, 500, Path.Combine(output, "resume-picker-preview.png"));
            var resumeList = Descendants<ListBox>((FrameworkElement)resumePicker.Content).Single();
            Check(resumeList.Items.Count == 2 && FindButton((FrameworkElement)resumePicker.Content, "Todas as pastas") != null, "seletor CLI apresenta sessoes de todas as pastas por padrao");
            Descendants<TextBox>((FrameworkElement)resumePicker.Content).Single(text => !text.IsReadOnly).Text = "branches";
            Check(resumeList.Items.Count == 1 && ((HistoryConversation)resumeList.SelectedItem).Id == "sessao-2", "busca do seletor filtra a lista e seleciona um resultado visivel");
            resumePicker.Close();
            var connectButton = FindButton((FrameworkElement)chat.Content, "Conectar conta");
            Check(connectButton.Visibility == Visibility.Collapsed, "aguarda a consulta da conta antes de oferecer login");
            chat.SetConnected(false); chat.SetStatus("Conecte sua conta para comecar.");
            Check(connectButton.Visibility == Visibility.Visible, "exibe Conectar conta quando nao ha conta conectada");
            chat.AddMessage("assistant", "Cheguei. Pode me arrastar pela tela e clicar em mim para conversar.\n\nQuer perguntar alguma coisa ou abrir um arquivo?");
            chat.AddMessage("user", "PimpDogg, abre minhas anotacoes.");
            chat.AddMessage("assistant", "Claro. Me diga o caminho ou use o botao Abrir arquivo.");
            Render((FrameworkElement)chat.Content, 464, 621, Path.Combine(output, "chat-preview.png"));
            Check(ButtonFits((FrameworkElement)chat.Content, "Enviar"), "mantem o envio visivel no tamanho inicial");
            Render((FrameworkElement)chat.Content, 414, 481, Path.Combine(output, "chat-compact.png"));
            Check(ButtonFits((FrameworkElement)chat.Content, "Enviar"), "mantem o envio visivel no tamanho minimo");
            chat.SetConnected(true, "usuario@example.com");
            chat.SetAccountLimits("Disponivel: 75% (5 horas) | 40% (7 dias)");
            Check(connectButton.Visibility == Visibility.Collapsed && Descendants<TextBlock>((FrameworkElement)chat.Content).Any(text => text.Text == "usuario@example.com"), "substitui o botao pelo e-mail da conta conectada");
            Render((FrameworkElement)chat.Content, 464, 621, Path.Combine(output, "chat-account-preview.png"));
            Render((FrameworkElement)chat.Content, 414, 481, Path.Combine(output, "chat-account-compact.png"));
            Check(ButtonFits((FrameworkElement)chat.Content, "Enviar"), "mantem o envio acessivel com os dados da conta no tamanho minimo");
            chat.SetConnected(false);
            Check(!Descendants<TextBlock>((FrameworkElement)chat.Content).Any(text => text.Text.Contains("usuario@example.com") || text.Text.Contains("75%")), "limpa e-mail e limites ao desconectar");
            chat.SetConnected(true);
            Check(Descendants<TextBlock>((FrameworkElement)chat.Content).Any(text => text.Text == "Conta conectada (e-mail nao informado)"), "mantem conta conectada quando o servico nao informa e-mail");
            chat.SetConnected(false);
            var composer = (TextBox)typeof(ChatWindow).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
            string commandText = null, sentText = null;
            chat.CommandRequested += value => commandText = value; chat.Send += value => sentText = value;
            composer.Text = "/usage"; FindButton((FrameworkElement)chat.Content, "Enviar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(commandText == "/usage" && sentText == null, "roteia comandos para o controle local em vez de enviar ao modelo");
            composer.Text = "Pergunta comum"; FindButton((FrameworkElement)chat.Content, "Enviar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(sentText == "Pergunta comum", "preserva o envio de perguntas apos adicionar comandos");
            chat.ShowPets();
            Render((FrameworkElement)chat.Content, 464, 661, Path.Combine(output, "pets-preview.png"));
            Check(FindButton((FrameworkElement)chat.Content, "Usar Dewey") != null && FindButton((FrameworkElement)chat.Content, "Usar PimpDogg") != null, "apresenta cartoes de selecao na aba Pets");
            string chosen = null;
            chat.PetSelected += id => { chosen = id; chat.SetPet(new PetCatalog().Find(id).Load()); };
            FindButton((FrameworkElement)chat.Content, "Usar Dewey").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(chosen == "dewey" && chat.ActivePetId == "dewey" && chat.Title.Contains("Dewey"), "troca o pet pelo cartao e atualiza o cabecalho");
            chat.SetBusy(true); FindButton((FrameworkElement)chat.Content, "Usar PimpDogg").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(chat.ActivePetId == "dewey", "aguarda o fim do pedido antes de trocar o pet"); chat.SetBusy(false);
            var state = new AppState(); var pet = new PetWindow(assets, state, delegate { }, delegate { });
            Render((FrameworkElement)pet.Content, 192, 252, Path.Combine(output, "pet-preview.png"));
            Check(File.Exists(Path.Combine(output, "pet-preview.png")), "renderiza janela transparente com PimpDogg original");
            TestAppearanceViews(chat, pet, composer);
            pet.Close(); chat.CloseForExit(); application.Shutdown();
        }
        private static void TestAppearanceViews(ChatWindow chat, PetWindow pet, TextBox composer)
        {
            TestScrollStyles();
            chat.ShowConversation();
            var wheel = new ColorWheel { SelectedColor = Color.FromRgb(64, 128, 192) };
            Check(wheel.SelectedColor == Color.FromRgb(64, 128, 192), "roda seletora preserva a cor inicial ao converter RGB e HSV");
            wheel.Brightness = 0; Check(wheel.SelectedColor == Colors.Black, "roda seletora permite preto pelo controle de luminosidade");
            wheel.SelectedColor = Colors.White; Check(wheel.SelectedColor == Colors.White && ColorWheel.FromHsv(120, 1, 1) == Colors.Lime, "roda seletora representa branco e matizes saturadas");
            var appearance = new AppearanceWindow(null, Theme.Preset("dark"));
            Render((FrameworkElement)appearance.Content, 736, 677, Path.Combine(output, "appearance-preview.png"));
            Check(Theme.Colors.All(option => FindButton((FrameworkElement)appearance.Content, "Cor: " + option.Label) != null), "cada funcao de cor tem acesso a roda seletora");
            Check(FindButton((FrameworkElement)chat.Content, "Aparencia") != null, "oferece o menu Aparencia no cabecalho");
            appearance.Close();
            var body = chat.AddMessage("assistant", "Mensagem que deve ser preservada."); composer.Text = "Rascunho preservado";
            string petId = chat.ActivePetId;
            var custom = Theme.Preset("light"); custom.FontFamily = "Consolas"; custom.FontSize = 18; custom.Colors["Background"] = "#EAF3F8"; custom.Colors["SelectedButton"] = "#2F6379"; custom.Colors["SpeechBubble"] = "#A0D7E8";
            Theme.Apply(custom);
            Render((FrameworkElement)chat.Content, 464, 700, Path.Combine(output, "chat-light-preview.png"));
            Check(((SolidColorBrush)chat.Background).Color == (Color)ColorConverter.ConvertFromString("#EAF3F8") && ((SolidColorBrush)FindButton((FrameworkElement)chat.Content, "Enviar").Background).Color == (Color)ColorConverter.ConvertFromString("#2F6379"), "atualiza fundo e botoes existentes sem recriar o chat");
            Check(Math.Abs(body.FontSize - 18) < 0.01 && body.FontFamily.Source == Theme.FontWithFallback("Consolas").Source, "atualiza fonte e tamanho das mensagens ja exibidas");
            var later = chat.AddMessage("assistant", "Mensagem nova.");
            Check(Math.Abs(later.FontSize - 18) < 0.01 && later.FontFamily.Source == Theme.FontWithFallback("Consolas").Source, "novas mensagens seguem a tipografia configurada");
            var bubble = (Border)typeof(PetWindow).GetField("bubble", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pet);
            Check(((SolidColorBrush)bubble.Background).Color == (Color)ColorConverter.ConvertFromString("#A0D7E8"), "aplica a cor do balao ao pet existente");
            Check(body.Text == "Mensagem que deve ser preservada." && composer.Text == "Rascunho preservado" && chat.ActivePetId == petId, "troca visual preserva mensagens, rascunho e pet selecionado");
            custom.FontSize = 22; Theme.Apply(custom);
            Render((FrameworkElement)chat.Content, 414, (int)chat.MinHeight - 79, Path.Combine(output, "chat-large-font-preview.png"));
            Check(ButtonFits((FrameworkElement)chat.Content, "Enviar"), "preserva acesso ao envio com o maior tamanho de fonte");
            Theme.Apply(Theme.Preset("dark"));
            Check(Math.Abs(body.FontSize - 13) < 0.01 && body.FontFamily.Source == Theme.FontWithFallback("Segoe UI").Source, "restaura a tipografia sem acumular escala");
        }
        private static bool ButtonFits(FrameworkElement root, string content)
        {
            var button = FindButton(root, content);
            if (button == null) return false;
            var bounds = button.TransformToAncestor(root).TransformBounds(new Rect(button.RenderSize));
            return bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= root.ActualWidth && bounds.Bottom <= root.ActualHeight;
        }
        private static Button FindButton(DependencyObject node, string content)
        {
            var button = node as Button;
            if (button != null && (Convert.ToString(button.Content) == content || System.Windows.Automation.AutomationProperties.GetName(button) == content)) return button;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var result = FindButton(VisualTreeHelper.GetChild(node, i), content);
                if (result != null) return result;
            }
            return null;
        }
        private static void Render(FrameworkElement element, int width, int height, string path)
        {
            element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            if (!path.EndsWith("pet-preview.png", StringComparison.Ordinal))
            {
                var background = new DrawingVisual();
                using (var context = background.RenderOpen()) context.DrawRectangle(Theme.Background, null, new Rect(0, 0, width, height));
                bitmap.Render(background);
            }
            bitmap.Render(element);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
        private static int FakeServer()
        {
            Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = new UTF8Encoding(false);
            bool initialized = false; string toolCall = null;
            var config = Json.Map("model", "model-a", "model_reasoning_effort", "low", "features", Json.Map("memories", false), "memories", Json.Map("use_memories", true, "generate_memories", false));
            var modelSettings = Json.Map(); var calls = new List<string>();
            bool accountLoggedIn = true, limitsUnavailable = false;
            bool repeatHistory = false, failHistoryRead = false;
            Dictionary<string, object> renamed = Json.Map();
            double accountUsedPercent = 25;
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                var request = Json.Decode(line); var parameters = Json.Object(request, "params"); string method = Json.Text(request, "method"); object id = Json.Get(request, "id");
                calls.Add(method);
                if (method == "initialize") { Write(Json.Map("id", id, "result", Json.Map("userAgent", "negotchi-test"))); }
                else if (method == "initialized") initialized = true;
                else if (method == "config/read") Write(Json.Map("id", id, "result", Json.Map("config", config)));
                else if (method == "config/batchWrite")
                {
                    foreach (var edit in Json.Items(parameters, "edits").OfType<Dictionary<string, object>>())
                    {
                        string[] parts = Json.Text(edit, "keyPath").Split('.');
                        Json.Object(config, parts[0])[parts[1]] = Json.Get(edit, "value");
                    }
                    Write(Json.Map("id", id, "result", Json.Map()));
                }
                else if (method == "model/list")
                {
                    bool second = Json.Text(parameters, "cursor") == "page2";
                    var model = Json.Map("id", second ? "b" : "a", "model", second ? "model-b" : "model-a", "displayName", second ? "Modelo B" : "Modelo A", "description", "Modelo do teste de comandos.", "isDefault", !second, "defaultReasoningEffort", second ? "high" : "low", "supportedReasoningEfforts", new[] { Json.Map("reasoningEffort", second ? "medium" : "low"), Json.Map("reasoningEffort", second ? "high" : "medium") });
                    Write(Json.Map("id", id, "result", Json.Map("data", new object[] { model, Json.Map("hidden", true) }, "nextCursor", second ? null : "page2")));
                }
                else if (method == "skills/list")
                {
                    if (!Equals(Json.Get(parameters, "forceReload"), true) || Json.Items(parameters, "cwds").Length != 1) return 4;
                    Write(Json.Map("id", id, "result", Json.Map("data", new[] { Json.Map("skills", new[] {
                        Json.Map("name", "teste", "description", "Skill do teste", "enabled", true, "path", @"C:\skills\teste\SKILL.md", "interface", Json.Map("displayName", "Skill Teste")),
                        Json.Map("name", "desativada", "description", "Skill desativada", "enabled", false, "path", @"C:\skills\desativada\SKILL.md")
                    }, "errors", new object[0]) })));
                }
                else if (method == "memory/status") Write(Json.Map("id", id, "result", Json.Map("v2Ready", false, "v2ConsolidatedThreads", 3)));
                else if (method == "thread/memoryMode/set") Write(Json.Map("id", id, "result", Json.Map()));
                else if (method == "thread/settings/update") { modelSettings = parameters; Write(Json.Map("id", id, "result", Json.Map())); }
                else if (method == "test/history") { repeatHistory = Equals(Json.Get(parameters, "repeat"), true); failHistoryRead = Equals(Json.Get(parameters, "failRead"), true); Write(Json.Map("id", id, "result", Json.Map())); }
                else if (method == "thread/list")
                {
                    if (Json.Get(parameters, "cwd") != null || Json.Items(parameters, "modelProviders").Length != 0 || !parameters.ContainsKey("modelProviders")) return 7;
                    bool second = Json.Text(parameters, "cursor") == "history-page-2";
                    var thread = Json.Map("id", second ? "history-2" : "history-1", "name", second ? "Outra conversa" : "Revisão 😀", "preview", "Pergunta anterior", "cwd", second ? "D:\\outro-projeto" : Environment.CurrentDirectory, "updatedAt", second ? 100 : 200, "createdAt", 50);
                    Write(Json.Map("id", id, "result", Json.Map("data", new[] { thread }, "nextCursor", !second || repeatHistory ? "history-page-2" : null)));
                }
                else if (method == "thread/read")
                {
                    if (failHistoryRead) Write(Json.Map("id", id, "error", Json.Map("code", -32000, "message", "Falha simulada ao ler sessao")));
                    else Write(Json.Map("id", id, "result", Json.Map("thread", Json.Map("id", Json.Text(parameters, "threadId"), "cwd", Environment.CurrentDirectory, "model", "model-b", "reasoningEffort", "high"))));
                }
                else if (method == "thread/turns/list")
                {
                    if (Json.Text(parameters, "itemsView") != "full" || Json.Text(parameters, "sortDirection") != "asc") return 8;
                    bool second = Json.Text(parameters, "cursor") == "turn-page-2";
                    var item = second ? Json.Map("type", "agentMessage", "text", "Resposta final 😀") : Json.Map("type", "userMessage", "content", new[] { Json.Map("type", "text", "text", "Primeira pergunta") });
                    Write(Json.Map("id", id, "result", Json.Map("data", new[] { Json.Map("id", second ? "turn-2" : "turn-1", "items", new[] { item }) }, "nextCursor", second ? null : "turn-page-2")));
                }
                else if (method == "thread/unarchive") Write(Json.Map("id", id, "result", Json.Map()));
                else if (method == "account/read") Write(Json.Map("id", id, "result", Json.Map("account", accountLoggedIn ? Json.Map("type", "chatgpt", "email", "usuario@example.com", "planType", "plus") : null)));
                else if (method == "account/rateLimits/read")
                {
                    if (limitsUnavailable) Write(Json.Map("id", id, "error", Json.Map("code", -32000, "message", "Limites temporariamente indisponiveis")));
                    else Write(Json.Map("id", id, "result", Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", accountUsedPercent, "windowDurationMins", 300), "secondary", Json.Map("usedPercent", 60, "windowDurationMins", 10080)))));
                }
                else if (method == "test/account")
                {
                    accountLoggedIn = Equals(Json.Get(parameters, "connected"), true);
                    Write(Json.Map("id", id, "result", Json.Map()));
                    Write(Json.Map("method", "account/updated", "params", Json.Map("authMode", accountLoggedIn ? "chatgpt" : null)));
                }
                else if (method == "test/limits")
                {
                    if (Json.Get(parameters, "usedPercent") != null) accountUsedPercent = Convert.ToDouble(parameters["usedPercent"]);
                    limitsUnavailable = Equals(Json.Get(parameters, "unavailable"), true);
                    Write(Json.Map("id", id, "result", Json.Map()));
                    if (!Equals(Json.Get(parameters, "notify"), false)) Write(Json.Map("method", "account/rateLimits/updated", "params", Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", accountUsedPercent, "windowDurationMins", 300)))));
                }
                else if (method == "test/completeTurn")
                {
                    Write(Json.Map("id", id, "result", Json.Map()));
                    Write(Json.Map("method", "turn/completed", "params", Json.Map("turn", Json.Map("status", "completed"))));
                }
                else if (method == "account/usage/read") Write(Json.Map("id", id, "result", Json.Map("summary", Json.Map("lifetimeTokens", 12345, "peakDailyTokens", 5000))));
                else if (method == "mcpServerStatus/list") Write(Json.Map("id", id, "result", Json.Map("data", new[] { Json.Map("name", "arquivos", "authStatus", "notLoggedIn", "tools", Json.Map("read_file", Json.Map())) })));
                else if (method == "test/state") Write(Json.Map("id", id, "result", Json.Map("modelSettings", modelSettings, "calls", calls.ToArray(), "renamed", renamed)));
                else if (method == "thread/name/set") { renamed = parameters; Write(Json.Map("id", id, "result", Json.Map())); }
                else if (method == "thread/archive") Write(Json.Map("id", id, "result", Json.Map()));
                else if (method == "thread/start")
                {
                    var tools = Json.Items(parameters, "dynamicTools");
                    var tool = tools.Length > 0 ? (Dictionary<string, object>)tools[0] : Json.Map();
                    if (!initialized || Json.Text(parameters, "sandbox") != "read-only" || Json.Text(tool, "type") != "function") return 2;
                    Write(Json.Map("id", id, "result", Json.Map("thread", Json.Map("id", "thread-fake"))));
                }
                else if (method == "turn/start")
                {
                    var input = (Dictionary<string, object>)Json.Items(parameters, "input")[0];
                    Write(Json.Map("id", id, "result", Json.Map("turn", Json.Map("id", "turn-fake", "echo", Json.Text(input, "text")))));
                    toolCall = "desktop-call-1";
                    Write(Json.Map("id", toolCall, "method", "item/tool/call", "params", Json.Map("threadId", "thread-fake", "turnId", "turn-fake", "tool", "negotchi_open_path", "arguments", Json.Map("path", "README.md"))));
                }
                else if (method.Length == 0 && Convert.ToString(id) == toolCall)
                {
                    if (!Equals(Json.Get(Json.Object(request, "result"), "success"), true)) return 3;
                    Write(Json.Map("method", "item/agentMessage/delta", "params", Json.Map("itemId", "answer-1", "delta", "Arquivo ")));
                    Write(Json.Map("method", "item/agentMessage/delta", "params", Json.Map("itemId", "answer-1", "delta", "aberto.")));
                    Write(Json.Map("method", "turn/completed", "params", Json.Map("turn", Json.Map("status", "completed"))));
                }
                else if (method == "thread/resume") Write(Json.Map("id", id, "result", Json.Map("thread", Json.Map("id", Json.Text(parameters, "threadId")))));
                else if (method == "turn/interrupt") Write(Json.Map("id", id, "result", Json.Map()));
                else Write(Json.Map("id", id, "error", Json.Map("code", -32601, "message", "Metodo desconhecido")));
            }
            return 0;
        }
        private static void Write(object message) { Console.WriteLine(Json.Encode(message)); Console.Out.Flush(); }

        // Explicit interactive check, separate from the headless test run.
        private static int UiSmoke(string directory)
        {
            int exitCode = 0, stage = 0;
            string previousExecutable = Environment.GetEnvironmentVariable("NEGOTCHI_CODEX_PATH");
            Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", Assembly.GetExecutingAssembly().Location);
            var initialStore = new StateStore(Path.Combine(directory, "ui-smoke-home"));
            var initialState = new AppState { Folder = directory, SelectedPetId = "rocky", ThreadId = "thread-preserved" };
            initialState.Messages.Add(new ChatMessage { Role = "assistant", Text = "Conversa preservada.", PetName = "PimpDogg" }); initialStore.Save(initialState);
            var application = new NegotchiApp(Path.Combine(directory, "ui-smoke-home"));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            application.Startup += delegate {
                application.Dispatcher.BeginInvoke(new Action(() => timer.Start()));
            };
            timer.Tick += async delegate {
                try
                {
                    var pet = (PetWindow)application.MainWindow;
                    var chat = application.Windows.OfType<ChatWindow>().Single();
                    if (stage++ == 0)
                    {
                        Check(pet.IsVisible && pet.AllowsTransparency, "inicia a janela real com transparencia");
                        Check(pet.ActivePetId == "rocky" && chat.ActivePetId == "rocky", "reabre com o pet escolhido no estado salvo");
                        pet.Left = SystemParameters.WorkArea.Left + 80; pet.Top = SystemParameters.WorkArea.Top + 80; pet.PersistPosition();
                        pet.ResizePet(1.25);
                        Check(Math.Abs(pet.Width - 240) < 1, "redimensiona a janela real do personagem");
                        var restored = new StateStore(Path.Combine(directory, "ui-smoke-home")).Load();
                        Check(restored.Scale == 1.25 && restored.X >= SystemParameters.WorkArea.Left, "persiste preferencias pelo ciclo de vida real");
                        chat.Left = SystemParameters.WorkArea.Left + 350; chat.Top = SystemParameters.WorkArea.Top + 50; chat.Show();
                    }
                    else
                    {
                        timer.Stop();
                        Check(chat.IsVisible, "abre a janela real de conversa");
                        TestWindowChrome(chat, true);
                        var accountAgent = (CodexClient)typeof(NegotchiApp).GetField("agent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(application);
                        var accountEmail = (TextBlock)typeof(ChatWindow).GetField("accountEmail", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
                        var accountLimits = (TextBlock)typeof(ChatWindow).GetField("accountLimits", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
                        var accountInfo = (Border)typeof(ChatWindow).GetField("accountInfo", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
                        var connectButton = FindButton((FrameworkElement)chat.Content, "Conectar conta");
                        await WaitFor(() => accountEmail.Text == "usuario@example.com" && accountLimits.Text.Contains("75%"));
                        Check(connectButton.Visibility == Visibility.Collapsed && accountLimits.Text.Contains("40%"), "reconhece a conta ao abrir e mostra e-mail e limites sem clicar em conectar");
                        await accountAgent.RequestAsync("test/limits", Json.Map("usedPercent", 35));
                        await WaitFor(() => accountLimits.Text.Contains("65%"));
                        Check(true, "atualiza o percentual ao receber notificacao de limites");
                        await accountAgent.RequestAsync("test/limits", Json.Map("usedPercent", 45, "notify", false));
                        await accountAgent.RequestAsync("test/completeTurn", Json.Map());
                        await WaitFor(() => accountLimits.Text.Contains("55%"));
                        Check(true, "consulta os limites ao concluir um pedido mesmo sem notificacao de consumo");
                        await accountAgent.RequestAsync("test/limits", Json.Map("unavailable", true));
                        await WaitFor(() => accountLimits.Text == "Limites indisponiveis");
                        Check(connectButton.Visibility == Visibility.Collapsed && accountEmail.Text == "usuario@example.com", "falha de limites preserva a conta sem exibir saldo antigo");
                        await accountAgent.RequestAsync("test/account", Json.Map("connected", false));
                        await WaitFor(() => connectButton.Visibility == Visibility.Visible);
                        Check(accountInfo.Visibility == Visibility.Collapsed && accountEmail.Text == "" && accountLimits.Text == "", "logout restaura o botao e remove dados da conta");
                        await accountAgent.RequestAsync("test/limits", Json.Map("notify", false));
                        await accountAgent.RequestAsync("test/account", Json.Map("connected", true));
                        await WaitFor(() => accountEmail.Text == "usuario@example.com" && accountLimits.Text.Contains("55%"));
                        Check(connectButton.Visibility == Visibility.Collapsed, "reconexao restaura e-mail e percentual disponivel");
                        chat.ShowPets();
                        Render((FrameworkElement)chat.Content, (int)chat.ActualWidth - 16, (int)chat.ActualHeight - 39, Path.Combine(directory, "ui-smoke-pets.png"));
                        var catalog = new PetCatalog();
                        foreach (var definition in catalog.Pets)
                        {
                            FindButton((FrameworkElement)chat.Content, "Usar " + definition.DisplayName).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            Check(pet.ActivePetId == definition.Id && chat.ActivePetId == definition.Id, "troca real pelo cartao: " + definition.DisplayName);
                        }
                        FindButton((FrameworkElement)chat.Content, "Usar Dewey").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        var saved = initialStore.Load();
                        Check(saved.SelectedPetId == "dewey", "salva a troca real de pet");
                        Check(saved.ThreadId == "thread-preserved" && saved.Messages.Count == 1 && saved.Messages[0].PetName == "PimpDogg", "troca real preserva conversa e autoria");
                        var selectedTray = (System.Windows.Forms.NotifyIcon)typeof(NegotchiApp).GetField("tray", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(application);
                        Check(selectedTray.Text.Contains("Dewey"), "atualiza o icone e o nome da bandeja");
                        chat.ShowConversation(); chat.UpdateLayout();
                        var composer = (TextBox)typeof(ChatWindow).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
                        chat.Activate();
                        composer.Focus();
                        await WaitFor(() => composer.IsKeyboardFocused);
                        composer.Text = "/";
                        composer.UpdateLayout();
                        var menu = typeof(ChatWindow).GetField("commandMenu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chat);
                        var popup = (System.Windows.Controls.Primitives.Popup)typeof(CommandMenu).GetField("popup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(menu);
                        Check(popup.IsOpen, "abre o seletor real de comandos ao digitar barra");
                        Render((FrameworkElement)popup.Child, 420, 290, Path.Combine(directory, "ui-command-menu.png")); popup.IsOpen = false;
                        Check(composer.Text == "/", "o seletor de comandos preserva a entrada");
                        await CommandDialogSmoke(application, "/model", "Modelo do Codex", window => {
                            var combos = Descendants<ComboBox>(window).ToArray();
                            combos[0].SelectedItem = combos[0].Items.Cast<ModelChoice>().First(m => m.Model == "model-b"); combos[1].SelectedItem = "medium";
                            Render((FrameworkElement)window.Content, 544, 421, Path.Combine(directory, "ui-model.png"));
                            FindButton(window, "Aplicar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        Check(initialStore.Load().SelectedModel == "model-b" && initialStore.Load().ReasoningEffort == "medium", "aplica modelo e esforco pela janela real");
                        await CommandDialogSmoke(application, "/usage", "Uso e status do Codex", window => {
                            Render((FrameworkElement)window.Content, 544, 501, Path.Combine(directory, "ui-usage.png"));
                            Check(Descendants<TextBox>(window).Any(box => box.Text.Contains("12.345")), "mostra o uso recebido do servidor");
                            FindButton(window, "Fechar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        await CommandDialogSmoke(application, "/memories", "Memorias do Codex", window => {
                            var options = Descendants<CheckBox>(window).ToArray(); options[0].IsChecked = true; options[1].IsChecked = false; options[2].IsChecked = false;
                            Descendants<ComboBox>(window).Single().SelectedIndex = 2;
                            Render((FrameworkElement)window.Content, 544, 531, Path.Combine(directory, "ui-memories.png"));
                            FindButton(window, "Aplicar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        Check(initialStore.Load().ThreadMemoryMode == "disabled", "aplica permissao de memorias pela janela real");
                        composer.Text = "Rascunho preservado";
                        await CommandDialogSmoke(application, "/skills", "Skills do Codex", window => {
                            var list = Descendants<ListBox>(window).Single(); list.SelectedItem = list.Items.Cast<SkillChoice>().First(s => s.Enabled);
                            Render((FrameworkElement)window.Content, 544, 531, Path.Combine(directory, "ui-skills.png"));
                            FindButton(window, "Usar skill").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        var pending = (SkillChoice)typeof(NegotchiApp).GetField("pendingSkill", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(application);
                        Check(pending != null && pending.Name == "teste" && composer.Text == "$teste Rascunho preservado", "seleciona skill sem perder o pedido em edicao");
                        chat.ClearSkill(); Check(composer.Text == "Rascunho preservado", "remover skill tambem remove a mencao automatica");
                        await CommandDialogSmoke(application, "/mcp", "Servidores MCP", window => { FindButton(window, "Fechar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
                        await CommandDialogSmoke(application, "/account", "Perfil e atividade", window => { Render((FrameworkElement)window.Content, 580, 500, Path.Combine(directory, "account-preview.png")); FindButton(window, "Fechar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
                        await CommandDialogSmoke(application, "/files", "Arquivos da conversa", window => {
                            Check(Descendants<TreeView>(window).Single().Items.Count == 1 && Descendants<TextBox>(window).Any(box => box.ToolTip as string == "Buscar por nome na pasta da conversa") && FindButton(window, "Prévia") != null && FindButton(window, "Referenciar") != null, "abre o explorador limitado ao contexto da conversa");
                            FindButton(window, "Fechar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        var agent = (CodexClient)typeof(NegotchiApp).GetField("agent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(application);
                        var captured = await agent.RequestAsync("test/state", null);
                        Check(!Json.Items(captured, "calls").Contains("turn/start"), "comandos de controle nao viram turnos de perguntas ao modelo");
                        var beforeAppearance = initialStore.Load();
                        DialogSmoke(application, () => FindButton((FrameworkElement)chat.Content, "Aparencia").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), "Aparencia", window => {
                            var appearance = (AppearanceWindow)window;
                            var mode = (ComboBox)typeof(AppearanceWindow).GetField("mode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(appearance);
                            var font = (ComboBox)typeof(AppearanceWindow).GetField("font", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(appearance);
                            var size = (Slider)typeof(AppearanceWindow).GetField("fontSize", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(appearance);
                            mode.SelectedIndex = 1; font.SelectedItem = "Consolas"; size.Value = 16;
                            foreach (var option in Theme.Colors)
                            {
                                DialogSmoke(application, () => FindButton(window, "Cor: " + option.Label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), "Cor: " + option.Label, picker => {
                                    var wheel = Descendants<ColorWheel>(picker).Single();
                                    string chosen = option.Key == "Background" ? "#EAF3F8" : option.Key == "SelectedButton" ? "#2F6379" : Theme.Preset("light").Colors[option.Key];
                                    wheel.SelectedColor = (Color)ColorConverter.ConvertFromString(chosen);
                                    Check(Descendants<TextBox>(picker).Single().Text == chosen, "roda seletora atualiza a cor: " + option.Label);
                                    if (option.Key == "Background")
                                    {
                                        var content = (FrameworkElement)picker.Content;
                                        Render(content, (int)(content.ActualWidth + content.Margin.Left + content.Margin.Right), (int)(content.ActualHeight + content.Margin.Top + content.Margin.Bottom), Path.Combine(directory, "ui-color-wheel.png"));
                                        Check(ButtonFits(content, "Usar cor"), "mantem a confirmacao da cor visivel na janela");
                                    }
                                    FindButton(picker, "Usar cor").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                });
                            }
                            var dialogContent = (FrameworkElement)window.Content;
                            Render(dialogContent, (int)(dialogContent.ActualWidth + dialogContent.Margin.Left + dialogContent.Margin.Right), (int)(dialogContent.ActualHeight + dialogContent.Margin.Top + dialogContent.Margin.Bottom), Path.Combine(directory, "ui-appearance.png"));
                            FindButton(window, "Aplicar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        var afterAppearance = initialStore.Load();
                        Check(afterAppearance.Appearance.Mode == "light" && afterAppearance.Appearance.FontFamily == "Consolas" && afterAppearance.Appearance.FontSize == 16 && afterAppearance.Appearance.Colors["Background"] == "#EAF3F8", "salva a aparencia aplicada pelo menu e pela roda seletora");
                        TestWindowChrome(chat, false);
                        Check(((SolidColorBrush)chat.Background).Color == (Color)ColorConverter.ConvertFromString("#EAF3F8") && composer.FontFamily.Source == Theme.FontWithFallback("Cascadia Mono, Consolas").Source, "aplica tema mantendo a fonte operacional no compositor");
                        Check(afterAppearance.ThreadId == beforeAppearance.ThreadId && afterAppearance.Messages.Count == beforeAppearance.Messages.Count && afterAppearance.Folder == beforeAppearance.Folder && composer.Text == "Rascunho preservado", "menu de aparencia preserva conversa, pasta e rascunho");
                        DialogSmoke(application, () => FindButton((FrameworkElement)chat.Content, "Aparencia").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)), "Aparencia", window => {
                            FindButton(window, "Restaurar padrao").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            var size = (Slider)typeof(AppearanceWindow).GetField("fontSize", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
                            Check(size.Value == 13, "restaurar padrao recupera a configuracao original na previa");
                            FindButton(window, "Cancelar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        Check(initialStore.Load().Appearance.Mode == "light" && ((SolidColorBrush)chat.Background).Color == (Color)ColorConverter.ConvertFromString("#EAF3F8"), "cancelar mantem as preferencias aplicadas anteriormente");
                        Render((FrameworkElement)chat.Content, (int)chat.ActualWidth - 16, (int)chat.ActualHeight - 39, Path.Combine(directory, "ui-smoke-chat.png"));
                        var import = typeof(NegotchiApp).GetMethod("OpenHistoryConversation", BindingFlags.NonPublic | BindingFlags.Instance);
                        var appStateField = typeof(NegotchiApp).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
                        var commandBusyField = typeof(NegotchiApp).GetField("commandBusy", BindingFlags.NonPublic | BindingFlags.Instance);
                        var historyEntry = new HistoryConversation { Id = "history-1", Title = "Revisão 😀", Folder = directory };
                        string previousConversation = initialStore.Load().ActiveConversationId;
                        await accountAgent.RequestAsync("test/history", Json.Map("failRead", true));
                        import.Invoke(application, new object[] { historyEntry });
                        await WaitFor(() => !(bool)commandBusyField.GetValue(application));
                        Check(initialStore.Load().ActiveConversationId == previousConversation, "falha de leitura do historico preserva a conversa ativa");
                        await accountAgent.RequestAsync("test/history", Json.Map());
                        import.Invoke(application, new object[] { historyEntry });
                        await WaitFor(() => !(bool)commandBusyField.GetValue(application));
                        var imported = (AppState)appStateField.GetValue(application);
                        Check(imported.ThreadId == "history-1" && imported.Messages.Count == 2 && imported.SelectedModel == "model-b", "retoma sessao externa com historico completo e modelo preservado");
                        import.Invoke(application, new object[] { historyEntry });
                        await WaitFor(() => !(bool)commandBusyField.GetValue(application));
                        Check(initialStore.Load().Conversations.Count(conversation => conversation.ThreadId == "history-1") == 1, "reabrir a mesma sessao nao duplica conversas locais");
                        chat.Close(); Check(!chat.IsVisible && pet.IsVisible, "fechar o chat mantem o personagem em execucao");
                        var trayField = typeof(NegotchiApp).GetField("tray", BindingFlags.NonPublic | BindingFlags.Instance);
                        Check(((System.Windows.Forms.NotifyIcon)trayField.GetValue(application)).Visible, "mantem acesso pelo icone perto do relogio");
                        File.WriteAllText(Path.Combine(directory, "ui-smoke-report.json"), Json.Encode(Json.Map("passed", assertions, "failed", 0)), Encoding.UTF8);
                        timer.Stop(); application.Shutdown();
                    }
                }
                catch (Exception error) { Console.Error.WriteLine(error); exitCode = 1; timer.Stop(); application.Shutdown(); }
            };
            try { application.Run(); return exitCode; }
            finally { Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", previousExecutable); }
        }

        private static void DialogSmoke(NegotchiApp application, Action open, string title, Action<Window> interact)
        {
            bool found = false; Exception interactionError = null; var deadline = DateTime.UtcNow.AddSeconds(10);
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
            timer.Tick += delegate {
                var window = application.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsVisible && candidate.Title == title);
                if (window == null)
                {
                    if (DateTime.UtcNow >= deadline) { timer.Stop(); interactionError = new TimeoutException("A janela nao abriu: " + title); application.Shutdown(); }
                    return;
                }
                found = true; timer.Stop();
                try { window.UpdateLayout(); interact(window); }
                catch (Exception error) { interactionError = error; window.Close(); }
            };
            timer.Start();
            try { open(); if (interactionError != null) throw interactionError; Check(found, "abre a janela: " + title); }
            finally { timer.Stop(); }
        }
        private static async Task WaitFor(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(8);
            while (!condition())
            {
                if (DateTime.UtcNow >= deadline) throw new Exception("A interface nao recebeu a atualizacao esperada.");
                await Task.Delay(25);
            }
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T) yield return (T)root;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        private static async Task CommandDialogSmoke(NegotchiApp application, string command, string title, Action<Window> interact)
        {
            bool found = false; Exception interactionError = null;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
            timer.Tick += delegate {
                var window = application.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible && w.Title == title);
                if (window == null) return;
                found = true; timer.Stop();
                try { window.UpdateLayout(); interact(window); }
                catch (Exception error) { interactionError = error; window.Close(); }
            };
            timer.Start();
            try
            {
                Task execution = application.ExecuteCommandAsync(command);
                if (await Task.WhenAny(execution, Task.Delay(12000)) != execution) throw new TimeoutException("A janela do comando nao respondeu: " + command);
                await execution;
                if (interactionError != null) throw interactionError;
                Check(found, "abre o controle nativo de " + command);
            }
            finally { timer.Stop(); }
        }
    }
}
