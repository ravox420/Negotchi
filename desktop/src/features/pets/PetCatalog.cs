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
