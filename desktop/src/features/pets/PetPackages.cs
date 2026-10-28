using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal static class PetPackages
    {
        internal static readonly string[] RequiredStates = { "idle", "thinking", "running-right", "running-left", "waving", "jumping", "failed", "waiting", "running", "review", "responding" };
        public static void CheckId(string id)
        {
            if (string.IsNullOrEmpty(id) || !Regex.IsMatch(id, "^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$") || Regex.IsMatch(id, "^(con|prn|aux|nul|com[1-9]|lpt[1-9])$", RegexOptions.IgnoreCase))
                throw new InvalidDataException("Use um id com ate 64 letras, numeros, hifen ou sublinhado, sem nomes reservados do Windows.");
        }
        private static void CheckLocalFile(string path, long limit)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > limit || (info.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Arquivo de pet ausente, muito grande ou vinculado: " + info.Name);
        }
        public static PetAssets Load(string directory)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("A pasta do pet precisa ser local e sem vinculos.");
            return LoadFiles(Path.Combine(directory, "pet.json"), Path.Combine(directory, "spritesheet.png"));
        }
        public static PetAssets LoadCreated(string directory)
        {
            var assets = Load(directory);
            var manifest = Json.Decode(File.ReadAllText(Path.Combine(directory, "pet.json"), Encoding.UTF8));
            if (Convert.ToInt32(Json.Get(manifest, "schemaVersion") ?? 1) != 2) throw new InvalidDataException("O pet criado precisa usar o contrato v2 com onze estados.");
            return assets;
        }
        internal static PetAssets LoadFiles(string manifestPath, string sheetPath)
        {
            CheckLocalFile(manifestPath, 1024 * 1024); CheckLocalFile(sheetPath, 64 * 1024 * 1024);
            var data = Json.Decode(File.ReadAllText(manifestPath, Encoding.UTF8)); CheckId(Json.Text(data, "id"));
            if (string.IsNullOrWhiteSpace(Json.Text(data, "displayName"))) throw new InvalidDataException("Informe displayName no pet.json.");
            // Read the PNG dimensions before allocating the decoded bitmap.
            using (var file = File.OpenRead(sheetPath))
            {
                var header = new byte[24];
                if (file.Read(header, 0, header.Length) != header.Length || !header.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) throw new InvalidDataException("O spritesheet precisa ser PNG.");
                long width = 0, height = 0;
                for (int i = 16; i < 20; i++) width = width * 256 + header[i];
                for (int i = 20; i < 24; i++) height = height * 256 + header[i];
                if (width <= 0 || height <= 0 || width > 8192 || height > 8192 || width * height > 32000000) throw new InvalidDataException("Spritesheet excede as dimensoes permitidas.");
            }
            using (var manifest = File.OpenRead(manifestPath))
            using (var sheet = File.OpenRead(sheetPath))
            {
                var assets = new PetAssets(manifest, sheet);
                int version = Convert.ToInt32(Json.Get(data, "schemaVersion") ?? 1);
                if (version < 1 || version > 2) throw new InvalidDataException("Versao de pacote nao suportada.");
                var required = RequiredStates.Where(s => version >= 2 || (s != "thinking" && s != "responding"));
                if (required.Any(s => !assets.Animations.ContainsKey(s))) throw new InvalidDataException("Faltam animacoes obrigatorias no pet.json.");
                if (version == 2) ValidateCreatedPet(assets);
                return assets;
            }
        }
        private static void ValidateCreatedPet(PetAssets assets)
        {
            var signatures = new HashSet<string>();
            foreach (string state in RequiredStates)
            {
                var track = assets.Animations[state];
                bool loop = state != "waving" && state != "jumping" && state != "failed";
                if (track.Frames.Length != 8 || track.Loop != loop || track.Fallback != "idle") throw new InvalidDataException("Trilha v2 invalida: " + state);
                var hashes = new HashSet<string>();
                foreach (int index in track.Frames)
                {
                    int width = assets.FrameWidth, height = assets.FrameHeight;
                    var pixels = new byte[width * height * 4];
                    new FormatConvertedBitmap(assets.Frames[index], PixelFormats.Bgra32, null, 0).CopyPixels(pixels, width * 4, 0);
                    bool visible = false, transparent = false;
                    for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    {
                        byte alpha = pixels[(y * width + x) * 4 + 3];
                        visible |= alpha > 0; transparent |= alpha == 0;
                        if (alpha > 0 && (x == 0 || y == 0 || x == width - 1 || y == height - 1)) throw new InvalidDataException("Quadro toca a borda: " + state);
                    }
                    if (!visible || !transparent) throw new InvalidDataException("Quadro vazio ou sem transparencia: " + state);
                    using (var hash = SHA256.Create()) hashes.Add(Convert.ToBase64String(hash.ComputeHash(pixels)));
                }
                if (hashes.Count < 4) throw new InvalidDataException("Animacao com poucos desenhos distintos: " + state);
                if (!signatures.Add(string.Join(";", hashes.OrderBy(h => h, StringComparer.Ordinal)))) throw new InvalidDataException("Estados com desenhos identicos: " + state);
            }
            var aliases = new Dictionary<string, string> { { "move-right", "running-right" }, { "move_right", "running-right" }, { "move-left", "running-left" }, { "move_left", "running-left" }, { "wave", "waving" }, { "bounce", "jumping" }, { "sad", "failed" } };
            foreach (var alias in aliases)
            {
                AnimationTrack track; var canonical = assets.Animations[alias.Value];
                if (!assets.Animations.TryGetValue(alias.Key, out track) || !track.Frames.SequenceEqual(canonical.Frames) || track.Fps != canonical.Fps || track.Loop != canonical.Loop || track.Fallback != canonical.Fallback) throw new InvalidDataException("Alias inconsistente: " + alias.Key);
            }
        }
        public static string Install(string manifest, string sheet, string root, IEnumerable<string> existingIds)
        {
            var assets = LoadFiles(manifest, sheet); CheckId(assets.Id);
            Directory.CreateDirectory(root);
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("A pasta de destino nao pode ser um vinculo.");
            string destination = Path.Combine(root, assets.Id);
            if (existingIds.Any(id => string.Equals(id, assets.Id, StringComparison.OrdinalIgnoreCase)) || Directory.Exists(destination)) throw new InvalidDataException("Ja existe um pet com esse id. Escolha outro id no manifesto.");
            string staging = Path.Combine(root, ".import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                var data = Json.Decode(File.ReadAllText(manifest, Encoding.UTF8)); data["spritesheetPath"] = "spritesheet.png";
                File.WriteAllText(Path.Combine(staging, "pet.json"), Json.Encode(data), new UTF8Encoding(false));
                File.Copy(sheet, Path.Combine(staging, "spritesheet.png"));
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(assets.Frames[assets.Animations["idle"].Frames[0]]));
                using (var stream = File.Create(Path.Combine(staging, "preview.png"))) encoder.Save(stream);
                Load(staging); // Validate the copied package before publishing it to the catalog.
                Directory.Move(staging, destination);
                return assets.Id;
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        }
    }
}
