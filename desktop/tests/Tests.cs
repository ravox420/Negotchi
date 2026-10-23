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
    internal static class Tests
    {
        private static int assertions;
        private static string output;
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "app-server") return FakeServer();
            if (args.Length > 0 && args[0] == "--ui-smoke") return UiSmoke(Path.GetFullPath(args[1]));
            output = Path.GetFullPath(args.Length > 0 ? args[0] : ".build");
            Directory.CreateDirectory(output);
            try
            {
                TestAssets(); TestCatalog(); TestFiles(); TestState(); TestPosition(); TestSlashCommands(); TestProtocol().GetAwaiter().GetResult(); TestCommandProtocol().GetAwaiter().GetResult(); TestNativeProtocol().GetAwaiter().GetResult(); TestViews();
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
        private static void TestCatalog()
        {
            var catalog = new PetCatalog();
            Check(catalog.Pets.Length == 9, "inclui PimpDogg e os oito pets importados");
            Check(catalog.Find("ausente").Id == "PimpDogg" && catalog.Find(null).Id == "PimpDogg", "usa PimpDogg quando a escolha salva nao existe");
            Check(catalog.Find("DEWEY").Id == "dewey", "identifica pets sem diferenciar maiusculas");
            foreach (var definition in catalog.Pets)
            {
                var assets = definition.Load();
                Check(assets.Id == definition.Id && assets.DisplayName == definition.DisplayName && definition.Preview.PixelWidth == 192, "carrega manifesto e previa: " + definition.DisplayName);
                var used = assets.Animations.Values.SelectMany(track => track.Frames).Distinct().ToArray();
                bool visible = true;
                foreach (int index in used)
                {
                    var pixels = new byte[192 * 208 * 4];
                    new FormatConvertedBitmap(assets.Frames[index], PixelFormats.Bgra32, null, 0).CopyPixels(pixels, 192 * 4, 0);
                    if (!Enumerable.Range(0, 192 * 208).Any(i => pixels[i * 4 + 3] > 12)) visible = false;
                }
                Check(visible && used.Length == 57, "animacoes nao incluem celulas vazias: " + definition.DisplayName);
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
            store.Save(state); state.Y = 140; store.Save(state);
            var restored = store.Load();
            Check(restored.X == -350 && restored.Y == 140 && restored.Scale == 1.25, "salva posicao e tamanho com substituicao atomica");
            Check(restored.ThreadId == "conversa-1" && restored.Messages[0].Text == state.Messages[0].Text, "preserva conversa e caracteres");
            Check(restored.SelectedPetId == "dewey", "restaura o pet selecionado ao reabrir");
            Check(restored.SelectedModel == "model-a" && restored.ReasoningEffort == "medium" && restored.ThreadMemoryMode == "disabled", "restaura escolhas de modelo, esforco e memoria");
            File.WriteAllText(Path.Combine(directory, "state.json"), "arquivo quebrado");
            Check(store.Load().Messages.Count == 0, "recupera estado corrompido sem impedir inicializacao");
            File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Scale\":99,\"Messages\":null}");
            Check(store.Load().Scale == 1.8, "normaliza tamanho fora do intervalo");
            File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Messages\":[{\"Role\":\"assistant\",\"Text\":\"Resposta antiga\"}]}");
            Check(store.Load().Messages[0].PetName == "PimpDogg", "preserva a autoria de mensagens de versoes anteriores");
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
                    string text = "Ola! Acentuacao: açao, café, coração.\nAbra o arquivo & mantenha $(literal).";
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
            Check(SlashCommands.Match("/mo").Length == 1 && SlashCommands.Match("/reason")[0].Name == "/model", "filtra sugestoes por nome e alias");
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
                }
            }
            finally { Environment.SetEnvironmentVariable("CODEX_HOME", oldHome); }
        }
        private static void TestViews()
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var assets = new PetAssets(); var chat = new ChatWindow(assets); chat.SetFolder(@"C:\Users\Voce\Documents");
            chat.SetConnected(false); chat.SetStatus("Conecte sua conta para comecar.");
            chat.AddMessage("assistant", "Cheguei. Pode me arrastar pela tela e clicar em mim para conversar.\n\nQuer perguntar alguma coisa ou abrir um arquivo?");
            chat.AddMessage("user", "PimpDogg, abre minhas anotacoes.");
            chat.AddMessage("assistant", "Claro. Me diga o caminho ou use o botao Abrir arquivo.");
            Render((FrameworkElement)chat.Content, 464, 621, Path.Combine(output, "chat-preview.png"));
            Check(ButtonFits((FrameworkElement)chat.Content, "Enviar"), "mantem o envio visivel no tamanho inicial");
            Render((FrameworkElement)chat.Content, 414, 481, Path.Combine(output, "chat-compact.png"));
            Check(ButtonFits((FrameworkElement)chat.Content, "Enviar"), "mantem o envio visivel no tamanho minimo");
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
            pet.Close(); chat.CloseForExit(); application.Shutdown();
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
                else if (method == "account/read") Write(Json.Map("id", id, "result", Json.Map("account", Json.Map("type", "chatgpt", "planType", "plus"))));
                else if (method == "account/rateLimits/read") Write(Json.Map("id", id, "result", Json.Map("rateLimits", Json.Map("primary", Json.Map("usedPercent", 25, "windowDurationMins", 300)))));
                else if (method == "account/usage/read") Write(Json.Map("id", id, "result", Json.Map("summary", Json.Map("lifetimeTokens", 12345, "peakDailyTokens", 5000))));
                else if (method == "mcpServerStatus/list") Write(Json.Map("id", id, "result", Json.Map("data", new[] { Json.Map("name", "arquivos", "authStatus", "notLoggedIn", "tools", Json.Map("read_file", Json.Map())) })));
                else if (method == "test/state") Write(Json.Map("id", id, "result", Json.Map("modelSettings", modelSettings, "calls", calls.ToArray())));
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
                        composer.Text = "/";
                        composer.Focus();
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
                        await CommandDialogSmoke(application, "/skills", "Skills do Codex", window => {
                            var list = Descendants<ListBox>(window).Single(); list.SelectedItem = list.Items.Cast<SkillChoice>().First(s => s.Enabled);
                            Render((FrameworkElement)window.Content, 544, 531, Path.Combine(directory, "ui-skills.png"));
                            FindButton(window, "Usar skill").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        });
                        var pending = (SkillChoice)typeof(NegotchiApp).GetField("pendingSkill", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(application);
                        Check(pending != null && pending.Name == "teste" && composer.Text == "$teste Rascunho preservado", "seleciona skill sem perder o pedido em edicao");
                        chat.ClearSkill(); Check(composer.Text == "Rascunho preservado", "remover skill tambem remove a mencao automatica");
                        await CommandDialogSmoke(application, "/mcp", "Conexoes MCP", window => { FindButton(window, "Fechar").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); });
                        var agent = (CodexClient)typeof(NegotchiApp).GetField("agent", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(application);
                        var captured = await agent.RequestAsync("test/state", null);
                        Check(!Json.Items(captured, "calls").Contains("turn/start"), "comandos de controle nao viram turnos de perguntas ao modelo");
                        Render((FrameworkElement)chat.Content, (int)chat.ActualWidth - 16, (int)chat.ActualHeight - 39, Path.Combine(directory, "ui-smoke-chat.png"));
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
