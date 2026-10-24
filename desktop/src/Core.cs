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
    internal static class Json
    {
        public static string Encode(object value) { return new JavaScriptSerializer { MaxJsonLength = 16777216 }.Serialize(value); }
        public static Dictionary<string, object> Decode(string text) { return new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<Dictionary<string, object>>(text); }
        public static Dictionary<string, object> Map(params object[] values)
        {
            var result = new Dictionary<string, object>();
            for (int i = 0; i < values.Length; i += 2) result.Add((string)values[i], values[i + 1]);
            return result;
        }
        public static object Get(IDictionary<string, object> value, string key) { object item; return value != null && value.TryGetValue(key, out item) ? item : null; }
        public static Dictionary<string, object> Object(IDictionary<string, object> value, string key) { return Get(value, key) as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        public static string Text(IDictionary<string, object> value, string key) { return Convert.ToString(Get(value, key)); }
        public static object[] Items(IDictionary<string, object> value, string key)
        {
            var items = Get(value, key) as IEnumerable;
            return items == null || items is string ? new object[0] : items.Cast<object>().ToArray();
        }
    }

    public sealed class ChatMessage
    {
        public string Role { get; set; }
        public string Text { get; set; }
        public string PetName { get; set; }
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
        public AppState()
        {
            X = -1; Y = -1; Scale = 1; Topmost = true; SelectedPetId = "PimpDogg";
            Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Sandbox = "read-only";
            Messages = new List<ChatMessage>();
            Conversations = new List<ConversationState>();
            Projects = new List<ProjectState>();
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
        private static List<ChatMessage> Copy(IEnumerable<ChatMessage> messages) { return (messages ?? new ChatMessage[0]).Select(message => new ChatMessage { Role = message.Role, Text = message.Text, PetName = message.PetName }).ToList(); }
    }

    internal sealed class StateStore
    {
        private const int ReplaceExisting = 0x1, WriteThrough = 0x8;
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(string source, string destination, int flags);
        private readonly string path;
        public StateStore(string directory) { path = Path.Combine(directory, "state.json"); }
        public AppState Load()
        {
            if (!File.Exists(path)) return new AppState();
            try
            {
                var state = new JavaScriptSerializer { MaxJsonLength = 16777216 }.Deserialize<AppState>(File.ReadAllText(path, Encoding.UTF8)) ?? new AppState();
                state.Messages = state.Messages ?? new List<ChatMessage>();
                state.Messages = state.Messages.Where(m => m != null && m.Text != null).ToList();
                foreach (var message in state.Messages) if (message.Role == "assistant" && string.IsNullOrEmpty(message.PetName)) message.PetName = "PimpDogg";
                if (double.IsNaN(state.Scale) || double.IsInfinity(state.Scale)) state.Scale = 1;
                state.Scale = Math.Max(0.65, Math.Min(1.8, state.Scale));
                if (double.IsNaN(state.X) || double.IsInfinity(state.X) || double.IsNaN(state.Y) || double.IsInfinity(state.Y)) { state.X = -1; state.Y = -1; }
                if (!Directory.Exists(state.Folder)) state.Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (state.Sandbox != "workspace-write" && state.Sandbox != "danger-full-access") state.Sandbox = "read-only";
                state.EnsureConversations(); state.LoadActiveConversation();
                state.Appearance = Theme.Normalize(state.Appearance);
                return state;
            }
            catch (IOException) { return new AppState(); }
            catch (ArgumentException) { return new AppState(); }
            catch (InvalidOperationException) { return new AppState(); }
        }
        public void Save(AppState state)
        {
            state.SaveActiveConversation();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, Json.Encode(state), new UTF8Encoding(false));
            // Rename the completed file on the same volume, without merging metadata from the old state.
            if (!MoveFileEx(temporary, path, ReplaceExisting | WriteThrough)) throw new IOException("Nao foi possivel salvar as preferencias.", new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    internal sealed class AnimationTrack
    {
        public int[] Frames;
        public double Fps;
        public bool Loop;
        public string Fallback;
    }

    internal sealed class PetAssets
    {
        public readonly string Id, DisplayName, Description;
        public readonly int FrameWidth, FrameHeight;
        public readonly BitmapSource[] Frames;
        public readonly Dictionary<string, AnimationTrack> Animations = new Dictionary<string, AnimationTrack>();
        public PetAssets() : this("Negotchi.pets.PimpDogg.pet.json", "Negotchi.pets.PimpDogg.spritesheet.png") { }
        public PetAssets(string manifestResource, string spritesheetResource) : this(Assembly.GetExecutingAssembly().GetManifestResourceStream(manifestResource), Assembly.GetExecutingAssembly().GetManifestResourceStream(spritesheetResource)) { }
        internal PetAssets(Stream manifestStream, Stream sheetStream)
        {
            Dictionary<string, object> manifest;
            BitmapImage sheet;
            using (sheetStream)
            {
                using (var reader = new StreamReader(manifestStream, Encoding.UTF8)) manifest = Json.Decode(reader.ReadToEnd());
                sheet = new BitmapImage(); sheet.BeginInit(); sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.StreamSource = sheetStream; sheet.EndInit(); sheet.Freeze();
            }
            Id = Json.Text(manifest, "id"); DisplayName = Json.Text(manifest, "displayName"); Description = Json.Text(manifest, "description");
            var frame = Json.Object(manifest, "frame");
            int width = Convert.ToInt32(frame["width"]), height = Convert.ToInt32(frame["height"]);
            int columns = Convert.ToInt32(frame["columns"]), rows = Convert.ToInt32(frame["rows"]);
            FrameWidth = width; FrameHeight = height;
            if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0 || (long)columns * rows > 4096) throw new InvalidDataException("Grade do pet invalida.");
            if (sheet.PixelWidth != (long)columns * width || sheet.PixelHeight != (long)rows * height) throw new InvalidDataException("A imagem de " + DisplayName + " nao corresponde ao manifesto.");
            Frames = new BitmapSource[columns * rows];
            for (int i = 0; i < Frames.Length; i++)
            {
                var cropped = new CroppedBitmap(sheet, new Int32Rect((i % columns) * width, (i / columns) * height, width, height));
                cropped.Freeze(); Frames[i] = cropped;
            }
            foreach (var entry in Json.Object(manifest, "animations"))
            {
                var animation = (Dictionary<string, object>)entry.Value;
                var track = new AnimationTrack { Frames = Json.Items(animation, "frames").Select(Convert.ToInt32).ToArray(), Fps = Convert.ToDouble(animation["fps"]), Loop = Convert.ToBoolean(animation["loop"]), Fallback = Json.Text(animation, "fallback") };
                if (track.Frames.Length == 0 || track.Frames.Any(index => index < 0 || index >= Frames.Length) || double.IsNaN(track.Fps) || double.IsInfinity(track.Fps) || track.Fps <= 0 || track.Fps > 60) throw new InvalidDataException("Animacao invalida: " + entry.Key);
                Animations.Add(entry.Key, track);
            }
            if (!Animations.ContainsKey("idle") || Animations.Values.Any(track => !Animations.ContainsKey(track.Fallback))) throw new InvalidDataException("Animacao de repouso ausente.");
        }
        public string ResolveAnimation(string name)
        {
            name = name == "move-right" || name == "move_right" ? "running-right" : name == "move-left" || name == "move_left" ? "running-left" : name;
            if (Animations.ContainsKey(name)) return name;
            string fallback = name == "thinking" ? "waiting" : name == "responding" ? "review" : "idle";
            return Animations.ContainsKey(fallback) ? fallback : "idle";
        }
    }

    internal sealed class PetDefinition
    {
        public readonly string Id, DisplayName, Description;
        private readonly string manifestResource, sheetResource, previewResource;
        private readonly string packageDirectory;
        private BitmapSource preview;
        public PetDefinition(string manifest, string sheet, string thumbnail)
        {
            manifestResource = manifest; sheetResource = sheet; previewResource = thumbnail;
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(manifest), Encoding.UTF8))
            {
                var data = Json.Decode(reader.ReadToEnd()); Id = Json.Text(data, "id"); DisplayName = Json.Text(data, "displayName"); Description = Json.Text(data, "description");
            }
            if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(DisplayName)) throw new InvalidDataException("Pet sem identificacao.");
        }
        internal PetDefinition(string directory)
        {
            packageDirectory = directory;
            var assets = PetPackages.Load(directory);
            Id = assets.Id; DisplayName = assets.DisplayName; Description = assets.Description;
            preview = assets.Frames[assets.Animations["idle"].Frames[0]];
        }
        public BitmapSource Preview
        {
            get
            {
                if (preview == null)
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(previewResource))
                    {
                        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); preview = image;
                    }
                }
                return preview;
            }
        }
        public PetAssets Load() { return packageDirectory == null ? new PetAssets(manifestResource, sheetResource) : PetPackages.Load(packageDirectory); }
    }

    internal sealed class PetCatalog
    {
        public readonly PetDefinition[] Pets;
        public readonly List<string> Errors = new List<string>();
        public PetCatalog() : this(null) { }
        public PetCatalog(string packagesDirectory)
        {
            var pets = new List<PetDefinition>();
            foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(name => name.StartsWith("Negotchi.pets.", StringComparison.Ordinal) && name.EndsWith(".pet.json", StringComparison.Ordinal)))
            {
                string prefix = name.Substring(0, name.Length - "pet.json".Length);
                pets.Add(new PetDefinition(name, prefix + "spritesheet.png", prefix + "preview.png"));
            }
            if (pets.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pets.Count) throw new InvalidDataException("Existem pets com identificadores repetidos.");
            if (!string.IsNullOrEmpty(packagesDirectory) && Directory.Exists(packagesDirectory))
            foreach (string directory in Directory.GetDirectories(packagesDirectory))
            {
                if (Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal)) continue;
                try
                {
                    var definition = new PetDefinition(directory);
                    if (pets.Any(p => string.Equals(p.Id, definition.Id, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Identificador ja utilizado: " + definition.Id);
                    pets.Add(definition);
                }
                catch (Exception error) { Errors.Add(Path.GetFileName(directory) + ": " + error.Message); }
            }
            Pets = pets.OrderBy(p => p.Id == "PimpDogg" ? 0 : 1).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public PetDefinition Find(string id) { return Pets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Pets[0]; }
    }

}
