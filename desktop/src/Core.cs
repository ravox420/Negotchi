using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public AppState()
        {
            X = -1; Y = -1; Scale = 1; Topmost = true; SelectedPetId = "PimpDogg";
            Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Messages = new List<ChatMessage>();
        }
    }

    internal sealed class StateStore
    {
        private readonly string path;
        public StateStore(string directory) { path = Path.Combine(directory, "state.json"); }
        public AppState Load()
        {
            if (!File.Exists(path)) return new AppState();
            try
            {
                var state = new JavaScriptSerializer().Deserialize<AppState>(File.ReadAllText(path, Encoding.UTF8)) ?? new AppState();
                state.Messages = state.Messages ?? new List<ChatMessage>();
                state.Messages = state.Messages.Where(m => m != null && m.Text != null).ToList();
                foreach (var message in state.Messages) if (message.Role == "assistant" && string.IsNullOrEmpty(message.PetName)) message.PetName = "PimpDogg";
                if (double.IsNaN(state.Scale) || double.IsInfinity(state.Scale)) state.Scale = 1;
                state.Scale = Math.Max(0.65, Math.Min(1.8, state.Scale));
                if (double.IsNaN(state.X) || double.IsInfinity(state.X) || double.IsNaN(state.Y) || double.IsInfinity(state.Y)) { state.X = -1; state.Y = -1; }
                if (!Directory.Exists(state.Folder)) state.Folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return state;
            }
            catch (IOException) { return new AppState(); }
            catch (ArgumentException) { return new AppState(); }
            catch (InvalidOperationException) { return new AppState(); }
        }
        public void Save(AppState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, Json.Encode(state), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
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
        public PetAssets() : this("Negotchi.pet.json", "Negotchi.spritesheet.png") { }
        public PetAssets(string manifestResource, string spritesheetResource)
        {
            var assembly = Assembly.GetExecutingAssembly();
            Dictionary<string, object> manifest;
            using (var reader = new StreamReader(assembly.GetManifestResourceStream(manifestResource), Encoding.UTF8)) manifest = Json.Decode(reader.ReadToEnd());
            Id = Json.Text(manifest, "id"); DisplayName = Json.Text(manifest, "displayName"); Description = Json.Text(manifest, "description");
            BitmapImage sheet;
            using (var stream = assembly.GetManifestResourceStream(spritesheetResource))
            {
                sheet = new BitmapImage(); sheet.BeginInit(); sheet.CacheOption = BitmapCacheOption.OnLoad;
                sheet.StreamSource = stream; sheet.EndInit(); sheet.Freeze();
            }
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
    }

    internal sealed class PetDefinition
    {
        public readonly string Id, DisplayName, Description;
        private readonly string manifestResource, sheetResource, previewResource;
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
        public PetAssets Load() { return new PetAssets(manifestResource, sheetResource); }
    }

    internal sealed class PetCatalog
    {
        public readonly PetDefinition[] Pets;
        public PetCatalog()
        {
            var pets = new List<PetDefinition> { new PetDefinition("Negotchi.pet.json", "Negotchi.spritesheet.png", "Negotchi.preview.png") };
            foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(name => name.StartsWith("Negotchi.pets.", StringComparison.Ordinal) && name.EndsWith(".pet.json", StringComparison.Ordinal)))
            {
                string prefix = name.Substring(0, name.Length - "pet.json".Length);
                pets.Add(new PetDefinition(name, prefix + "spritesheet.png", prefix + "preview.png"));
            }
            if (pets.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pets.Count) throw new InvalidDataException("Existem pets com identificadores repetidos.");
            Pets = pets.OrderBy(p => p.Id == "PimpDogg" ? 0 : 1).ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public PetDefinition Find(string id) { return Pets.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Pets[0]; }
    }

    internal static class Theme
    {
        public static readonly Brush Background = Brush("#17131C"), Card = Brush("#241D2B"), Pink = Brush("#F0A5C9"), Text = Brush("#F8F1FA"), Muted = Brush("#B4A4BC"), Line = Brush("#3A2E43");
        public static SolidColorBrush Brush(string color) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color); brush.Freeze(); return brush; }
        public static System.Windows.Controls.Button Button(string label, RoutedEventHandler clicked, bool accent)
        {
            var button = new System.Windows.Controls.Button { Content = label, Background = accent ? Pink : Card, Foreground = accent ? Background : Text, BorderBrush = Line, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 7, 0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12, MinHeight = 32 };
            var border = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.Border));
            border.SetValue(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(System.Windows.Controls.Border.BackgroundProperty, new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));
            border.SetValue(System.Windows.Controls.Border.BorderBrushProperty, new TemplateBindingExtension(System.Windows.Controls.Control.BorderBrushProperty));
            border.SetValue(System.Windows.Controls.Border.BorderThicknessProperty, new Thickness(1));
            var presenter = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            presenter.SetValue(System.Windows.FrameworkElement.MarginProperty, new TemplateBindingExtension(System.Windows.Controls.Control.PaddingProperty));
            presenter.SetValue(System.Windows.FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(System.Windows.FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            button.Template = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button)) { VisualTree = border };
            button.Click += clicked; return button;
        }
    }
}
