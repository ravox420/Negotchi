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
                TestAssets(); TestFiles(); TestState(); TestPosition(); TestProtocol().GetAwaiter().GetResult(); TestNativeProtocol().GetAwaiter().GetResult(); TestViews();
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
        private static void TestState()
        {
            string directory = Path.Combine(output, "state-tests"); Directory.CreateDirectory(directory);
            var store = new StateStore(directory);
            var state = new AppState { X = -350, Y = 120, Scale = 1.25, Folder = output, ThreadId = "conversa-1" };
            state.Messages.Add(new ChatMessage { Role = "user", Text = "Voce consegue ler acentuacao? cafe, acoes, coracao." });
            store.Save(state); state.Y = 140; store.Save(state);
            var restored = store.Load();
            Check(restored.X == -350 && restored.Y == 140 && restored.Scale == 1.25, "salva posicao e tamanho com substituicao atomica");
            Check(restored.ThreadId == "conversa-1" && restored.Messages[0].Text == state.Messages[0].Text, "preserva conversa e caracteres");
            File.WriteAllText(Path.Combine(directory, "state.json"), "arquivo quebrado");
            Check(store.Load().Messages.Count == 0, "recupera estado corrompido sem impedir inicializacao");
            File.WriteAllText(Path.Combine(directory, "state.json"), "{\"Scale\":99,\"Messages\":null}");
            Check(store.Load().Scale == 1.8, "normaliza tamanho fora do intervalo");
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
            if (button != null && Convert.ToString(button.Content) == content) return button;
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
            if (path.Contains("chat-"))
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
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                var request = Json.Decode(line); var parameters = Json.Object(request, "params"); string method = Json.Text(request, "method"); object id = Json.Get(request, "id");
                if (method == "initialize") { Write(Json.Map("id", id, "result", Json.Map("userAgent", "negotchi-test"))); }
                else if (method == "initialized") initialized = true;
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
            var application = new NegotchiApp(Path.Combine(directory, "ui-smoke-home"));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            application.Startup += delegate {
                application.Dispatcher.BeginInvoke(new Action(() => timer.Start()));
            };
            timer.Tick += delegate {
                try
                {
                    var pet = (PetWindow)application.MainWindow;
                    var chat = application.Windows.OfType<ChatWindow>().Single();
                    if (stage++ == 0)
                    {
                        Check(pet.IsVisible && pet.AllowsTransparency, "inicia a janela real com transparencia");
                        pet.Left = SystemParameters.WorkArea.Left + 80; pet.Top = SystemParameters.WorkArea.Top + 80; pet.PersistPosition();
                        pet.ResizePet(1.25);
                        Check(Math.Abs(pet.Width - 240) < 1, "redimensiona a janela real do personagem");
                        var restored = new StateStore(Path.Combine(directory, "ui-smoke-home")).Load();
                        Check(restored.Scale == 1.25 && restored.X >= SystemParameters.WorkArea.Left, "persiste preferencias pelo ciclo de vida real");
                        chat.Left = SystemParameters.WorkArea.Left + 350; chat.Top = SystemParameters.WorkArea.Top + 50; chat.Show();
                    }
                    else
                    {
                        Check(chat.IsVisible, "abre a janela real de conversa");
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
            application.Run(); return exitCode;
        }
    }
}
