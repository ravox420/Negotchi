using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal static partial class Tests
    {
        private static Dictionary<string, object> ActivityItem(string id, string type, string turn = "turn-a")
        {
            return Json.Map("threadId", "thread-a", "turnId", turn, "item", Json.Map("id", id, "type", type));
        }
        private static bool InvalidPackage(Action action)
        {
            try { action(); return false; } catch (InvalidDataException) { return true; }
        }
        private static void TestPetCreation()
        {
            var activity = new PetActivity(); activity.Begin("thread-a");
            Check(activity.Animation == "thinking", "pedido inicia pensando");
            Check(!activity.Observe("item/started", ActivityItem("late", "commandExecution")), "ignora itens antes da confirmacao do turno");
            activity.Observe("turn/started", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-a")));
            activity.Observe("item/started", ActivityItem("cmd", "commandExecution"));
            Check(activity.Animation == "running", "execucao de comando mostra trabalho ativo");
            activity.Observe("item/started", ActivityItem("file", "fileChange"));
            Check(activity.Animation == "review", "alteracoes em arquivos mostram revisao");
            activity.Observe("item/completed", ActivityItem("file", "fileChange"));
            Check(activity.Animation == "running", "mantem ferramenta concorrente ao concluir outro item");
            int first = activity.Wait(), second = activity.Wait(); activity.Resume(first);
            Check(activity.Animation == "waiting", "permanece esperando enquanto outra solicitacao esta aberta");
            activity.Resume(second); Check(activity.Animation == "running", "restaura trabalho real apos aprovacao");
            activity.Observe("item/agentMessage/delta", Json.Map("threadId", "thread-a", "turnId", "turn-a"));
            Check(activity.Animation == "running", "streaming nao mascara ferramenta concorrente");
            activity.Observe("item/completed", ActivityItem("cmd", "commandExecution"));
            Check(activity.Animation == "responding", "resposta em streaming tem animacao propria");
            activity.Observe("item/completed", ActivityItem("msg", "agentMessage"));
            Check(activity.Animation == "thinking", "retoma processamento entre itens");
            activity.Observe("item/started", ActivityItem("review", "enteredReviewMode"));
            activity.Observe("item/completed", ActivityItem("review", "enteredReviewMode"));
            Check(activity.Animation == "review", "modo de revisao persiste apos seu item de entrada");
            activity.Observe("item/completed", ActivityItem("exit", "exitedReviewMode"));
            Check(activity.Animation == "thinking", "saida do modo de revisao restaura o processamento");
            Check(!activity.Observe("item/started", ActivityItem("old", "commandExecution", "other-turn")) && activity.Animation == "thinking", "recusa eventos de outro turno");
            var foreign = ActivityItem("foreign", "commandExecution"); foreign["threadId"] = "other-thread";
            Check(!activity.Observe("item/started", foreign), "recusa eventos de outra conversa");
            activity.Observe("error", Json.Map("threadId", "thread-a", "turnId", "turn-a", "willRetry", true, "error", Json.Map("message", "Tentar novamente")));
            Check(activity.Active && activity.Animation == "failed", "falha recuperavel sinaliza erro sem encerrar turno");
            activity.Observe("item/started", ActivityItem("reason", "reasoning"));
            Check(activity.Animation == "thinking", "tentativa seguinte recupera o contexto de processamento");
            activity.Observe("turn/completed", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-a", "status", "completed")));
            Check(!activity.Active && activity.Animation == "jumping", "sucesso encerra com comemoracao breve");
            Check(!activity.Observe("item/started", ActivityItem("late", "commandExecution")), "evento tardio nao reativa pet apos conclusao");
            activity.Begin("thread-a");
            Check(!activity.Observe("turn/started", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-a"))), "turno encerrado nao contamina pedido novo");
            activity.Observe("turn/started", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-b")));
            activity.Observe("turn/completed", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-b", "status", "interrupted")));
            Check(activity.Animation == "idle", "interrupcao nao comemora sucesso");

            var legacy = new PetAssets();
            Check(legacy.ResolveAnimation("thinking") == "waiting" && legacy.ResolveAnimation("responding") == "review" && legacy.ResolveAnimation("move-right") == "running-right", "pets antigos preservam aliases e fallbacks de contexto");
            SlashInvocation invocation;
            Check(SlashCommands.TryParse("/create-pet um robo azul", out invocation) && invocation.Command.Name == "/create-pet" && invocation.Arguments == "um robo azul", "comando de criacao preserva descricao");
            Check(File.Exists(NegotchiApp.FindCreatePetSkill()), "compilacao inclui a skill create-pet");

            string root = Path.Combine(output, "pet-creation-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string source = Path.Combine(root, "source"); WriteCreatedPetFixture(source);
            var loaded = PetPackages.Load(source);
            Check(loaded.Animations.ContainsKey("thinking") && loaded.Animations.ContainsKey("responding"), "carrega pacote v2 com onze estados");
            string installed = Path.Combine(root, "installed");
            string id = PetPackages.Install(Path.Combine(source, "pet.json"), Path.Combine(source, "spritesheet.png"), installed, new PetCatalog().Pets.Select(p => p.Id));
            var catalog = new PetCatalog(installed);
            Check(catalog.Pets.Length == 11 && catalog.Find(id).Load().Id == id, "importacao aparece sem recompilar e sobrevive ao recarregamento");
            var chat = new ChatWindow(legacy); chat.RefreshPets(catalog); chat.SetPet(catalog.Find(id).Load());
            Check(chat.ActivePetId == id, "seletor recebe e aplica pet importado imediatamente"); chat.CloseForExit();
            Check(InvalidPackage(() => PetPackages.Install(Path.Combine(source, "pet.json"), Path.Combine(source, "spritesheet.png"), installed, catalog.Pets.Select(p => p.Id))), "colisao nao substitui pet existente");
            Check(Directory.GetDirectories(installed).Length == 1, "importacao nao deixa pacote parcial");
            Check(InvalidPackage(() => PetPackages.CheckId("..")) && InvalidPackage(() => PetPackages.CheckId("CON")) && InvalidPackage(() => PetPackages.CheckId("../fora")), "ids nao escapam da pasta nem usam dispositivos Windows");
            var data = Json.Decode(File.ReadAllText(Path.Combine(source, "pet.json")));
            Json.Object(Json.Object(data, "animations"), "thinking")["frames"] = new[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            File.WriteAllText(Path.Combine(source, "pet.json"), Json.Encode(data));
            Check(InvalidPackage(() => PetPackages.Load(source)), "pacote criado nao aceita animacao estatica");
            string broken = Path.Combine(installed, "broken"); Directory.CreateDirectory(broken); File.WriteAllText(Path.Combine(broken, "pet.json"), "{}");
            catalog = new PetCatalog(installed);
            Check(catalog.Errors.Count == 1 && catalog.Find(id).Id == id, "pacote corrompido nao impede carregar os pets validos");
        }
        private static void WriteCreatedPetFixture(string directory, string id = "test-created")
        {
            Directory.CreateDirectory(directory);
            const int width = 16, height = 20, columns = 8, rows = 11;
            var pixels = new byte[width * columns * height * rows * 4];
            var animations = Json.Map();
            for (int row = 0; row < rows; row++)
            {
                string state = PetPackages.RequiredStates[row];
                for (int col = 0; col < columns; col++) for (int y = 3; y < height - 3; y++) for (int x = 3; x < width - 3; x++)
                {
                    int offset = ((row * height + y) * width * columns + col * width + x) * 4;
                    pixels[offset] = (byte)(row * 20); pixels[offset + 1] = (byte)(col * 30); pixels[offset + 2] = 100; pixels[offset + 3] = 255;
                }
                animations[state] = Json.Map("frames", Enumerable.Range(row * 8, 8).ToArray(), "fps", 8, "loop", state != "waving" && state != "jumping" && state != "failed", "fallback", "idle");
            }
            foreach (var pair in new[] { new[] { "move-right", "running-right" }, new[] { "move_right", "running-right" }, new[] { "move-left", "running-left" }, new[] { "move_left", "running-left" }, new[] { "wave", "waving" }, new[] { "bounce", "jumping" }, new[] { "sad", "failed" } }) animations[pair[0]] = animations[pair[1]];
            var manifest = Json.Map("schemaVersion", 2, "id", id, "displayName", "Test created", "spritesheetPath", "spritesheet.png", "frame", Json.Map("width", width, "height", height, "columns", columns, "rows", rows), "animations", animations);
            File.WriteAllText(Path.Combine(directory, "pet.json"), Json.Encode(manifest), Encoding.UTF8);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width * columns, height * rows, 96, 96, PixelFormats.Bgra32, null, pixels, width * columns * 4)));
            using (var stream = File.Create(Path.Combine(directory, "spritesheet.png"))) encoder.Save(stream);
        }
        private static object PetField(object owner, string name) { return owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner); }
        private static void SetPetField(object owner, string name, object value) { owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value); }
        private static object PetCall(object owner, string name, params object[] args) { return owner.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, args); }
        private static int PetCreationUiSmoke(string directory)
        {
            output = directory; Directory.CreateDirectory(directory); int result = 0;
            string previous = Environment.GetEnvironmentVariable("NEGOTCHI_CODEX_PATH");
            Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", Assembly.GetExecutingAssembly().Location);
            string home = Path.Combine(directory, "pet-ui-" + Guid.NewGuid().ToString("N"));
            var store = new StateStore(home); store.Save(new AppState { Folder = directory, ThreadId = "thread-a" });
            var app = new NegotchiApp(home);
            app.Startup += delegate { app.Dispatcher.BeginInvoke(new Action(async delegate {
                try
                {
                    var pet = (PetWindow)app.MainWindow; var chat = app.Windows.OfType<ChatWindow>().Single();
                    var activity = (PetActivity)PetField(app, "activity"); chat.Show();
                    PetCall(app, "SetBusy", true); activity.Begin("thread-a");
                    PetCall(app, "OnNotification", "turn/started", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-a")));
                    await WaitFor(() => pet.CurrentAnimation == "waiting");
                    Check(true, "janela real usa fallback de pensamento do pet antigo");
                    PetCall(app, "OnNotification", "item/started", ActivityItem("cmd", "commandExecution"));
                    await WaitFor(() => pet.CurrentAnimation == "running");
                    Check(true, "janela real acompanha execucao de ferramenta");
                    var read = ActivityItem("read", "commandExecution"); Json.Object(read, "item")["commandActions"] = new[] { Json.Map("type", "read") };
                    PetCall(app, "OnNotification", "item/started", read);
                    await WaitFor(() => pet.CurrentAnimation == "review");
                    Check(true, "leitura identificada pelo protocolo ativa revisao");
                    PetCall(app, "OnNotification", "item/completed", read);
                    await WaitFor(() => pet.CurrentAnimation == "running");
                    Check(true, "concluir leitura restaura execucao ainda ativa");
                    PetCall(app, "OnNotification", "item/agentMessage/delta", Json.Map("threadId", "thread-a", "turnId", "turn-a", "itemId", "message", "delta", "Resposta de teste"));
                    PetCall(app, "OnNotification", "item/completed", ActivityItem("cmd", "commandExecution"));
                    await WaitFor(() => pet.CurrentAnimation == "review");
                    Check(true, "streaming usa fallback de resposta do pet antigo");
                    int wait = activity.Wait(); PetCall(app, "PublishPetActivity");
                    Check(pet.CurrentAnimation == "waiting", "espera por usuario assume a animacao imediatamente");
                    activity.Resume(wait); PetCall(app, "PublishPetActivity"); await WaitFor(() => pet.CurrentAnimation == "review");
                    pet.Play("running-left"); SetPetField(pet, "pressing", true); SetPetField(pet, "dragged", true);
                    PetCall(pet, "OnRelease", PetField(pet, "image"), new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                    Check(pet.CurrentAnimation == "review", "soltar arraste restaura contexto real da resposta");
                    string source = Path.Combine(directory, "ui-created-source"); WriteCreatedPetFixture(source);
                    SetPetField(app, "creatingPet", true);
                    var imported = (Dictionary<string, object>)PetCall(app, "AnswerAgentRequest", "item/tool/call", Json.Map("threadId", "thread-a", "turnId", "turn-a", "tool", "negotchi_import_pet", "arguments", Json.Map("directory", source)));
                    Check(Equals(Json.Get(imported, "success"), true), "ferramenta nativa registra pacote criado durante o turno");
                    Check(((PetCatalog)PetField(app, "catalog")).Pets.Length == 11, "catalogo real atualiza durante a criacao");
                    PetCall(app, "OnNotification", "turn/completed", Json.Map("threadId", "thread-a", "turn", Json.Map("id", "turn-a", "status", "completed")));
                    Check(pet.CurrentAnimation == "jumping", "conclusao real comemora apenas o sucesso");
                    chat.ShowPets(); chat.UpdateLayout();
                    FindButton((FrameworkElement)chat.Content, "Usar Test created").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(pet.ActivePetId == "test-created" && store.Load().SelectedPetId == "test-created", "pet criado pode ser escolhido e persistido sem recompilar");
                    pet.Mood("Pensando", "thinking", true); await WaitFor(() => pet.CurrentAnimation == "thinking");
                    Check(true, "novo pet usa sua propria faixa thinking");
                    pet.Mood("Respondendo", "responding", true); await WaitFor(() => pet.CurrentAnimation == "responding");
                    Check(true, "novo pet usa sua propria faixa responding"); pet.Mood("Pronto", "idle", false);
                    await app.ExecuteCommandAsync("/create-pet");
                    var pending = (SkillChoice)PetField(app, "pendingSkill");
                    Check(pending != null && pending.Name == "create-pet" && File.Exists(pending.Path) && ((TextBox)PetField(chat, "input")).Text.StartsWith("$create-pet "), "comando prepara skill nativa e descricao no campo");
                    chat.ShowPets(); chat.UpdateLayout();
                    Render((FrameworkElement)chat.Content, (int)chat.ActualWidth - 16, (int)chat.ActualHeight - 39, Path.Combine(directory, "create-pet-ui.png"));
                    SetPetField(app, "creatingPet", true); PetCall(app, "PetCreationPrompt", "Criar outro");
                    string draft = (string)PetField(app, "petDraftDirectory"); WriteCreatedPetFixture(draft, "fallback-created");
                    PetCall(app, "FinishPetCreation", true);
                    Check(((PetCatalog)PetField(app, "catalog")).Pets.Any(p => p.Id == "fallback-created"), "conversa antiga importa pasta validada sem depender de ferramenta nova");
                    SetPetField(app, "creatingPet", true); PetCall(app, "PetCreationPrompt", "Criar cancelado");
                    draft = (string)PetField(app, "petDraftDirectory"); WriteCreatedPetFixture(draft, "cancelled-created");
                    PetCall(app, "FinishPetCreation", false);
                    Check(!((PetCatalog)PetField(app, "catalog")).Pets.Any(p => p.Id == "cancelled-created"), "interrupcao nao importa rascunho incompleto");
                    File.WriteAllText(Path.Combine(directory, "pet-ui-smoke-report.json"), Json.Encode(Json.Map("passed", assertions, "failed", 0)), Encoding.UTF8);
                }
                catch (Exception error) { Console.Error.WriteLine(error); result = 1; }
                finally { app.Shutdown(); }
            })); };
            try { app.Run(); return result; }
            finally { Environment.SetEnvironmentVariable("NEGOTCHI_CODEX_PATH", previous); }
        }
    }
}
