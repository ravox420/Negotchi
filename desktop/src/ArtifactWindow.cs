using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;

namespace Negotchi
{
    internal static class ArtifactText
    {
        internal static bool IsText(string path) { return new[] { ".txt", ".md", ".csv", ".tsv", ".json", ".xml", ".html", ".css", ".js", ".ts", ".tsx", ".jsx", ".cs", ".py", ".ps1", ".sql", ".yaml", ".yml", ".toml", ".log", ".svg", ".ini", ".config" }.Contains(Path.GetExtension(path).ToLowerInvariant()); }
        internal static string Read(string path)
        {
            if (new FileInfo(path).Length > 50 * 1024 * 1024) throw new InvalidDataException("Limite da prévia: arquivo de 50 MB.");
            if (IsText(path))
            {
                using (var reader = new StreamReader(path, new UTF8Encoding(false, true), true))
                {
                    var buffer = new char[2 * 1024 * 1024 + 1]; int count = 0, read; while (count < buffer.Length && (read = reader.Read(buffer, count, buffer.Length - count)) > 0) count += read;
                    if (count > 2 * 1024 * 1024) throw new InvalidDataException("Limite de texto: 2 milhões de caracteres. Abra no aplicativo padrão.");
                    var text = new string(buffer, 0, count); if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Arquivo binário não suportado como texto."); return text;
                }
            }
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".docx" && extension != ".xlsx" && extension != ".pptx") throw new NotSupportedException("Formato sem prévia interna. Use Abrir no Windows.");
            using (var zip = ZipFile.OpenRead(path))
            {
                if (zip.Entries.Sum(entry => entry.Length) > 64 * 1024 * 1024 || zip.Entries.Count > 10000) throw new InvalidDataException("Documento excede os limites de leitura da prévia.");
                if (extension == ".docx") return "PRÉVIA TEXTUAL — estilos e paginação não são reproduzidos.\n\n" + Paragraphs(Xml(zip.GetEntry("word/document.xml")));
                if (extension == ".pptx") return "PRÉVIA TEXTUAL — gráficos, animações e layout não são reproduzidos.\n\n" + string.Join("\n\n", zip.Entries.Where(entry => System.Text.RegularExpressions.Regex.IsMatch(entry.FullName, "^ppt/slides/slide[0-9]+\\.xml$")).OrderBy(entry => int.Parse(Path.GetFileNameWithoutExtension(entry.Name).Substring(5))).Select((entry, index) => "SLIDE " + (index + 1) + "\n" + Paragraphs(Xml(entry))));
                var sharedEntry = zip.GetEntry("xl/sharedStrings.xml"); var shared = sharedEntry == null ? new string[0] : Xml(sharedEntry).Descendants().Where(node => node.Name.LocalName == "si").Select(node => string.Concat(node.Descendants().Where(text => text.Name.LocalName == "t").Select(text => text.Value))).ToArray();
                var result = new StringBuilder("PRÉVIA DE CÉLULAS — valores armazenados; fórmulas não são recalculadas.\n");
                foreach (var sheet in zip.Entries.Where(entry => System.Text.RegularExpressions.Regex.IsMatch(entry.FullName, "^xl/worksheets/sheet[0-9]+\\.xml$")).OrderBy(entry => entry.FullName))
                {
                    result.AppendLine("\n" + sheet.Name);
                    foreach (var row in Xml(sheet).Descendants().Where(node => node.Name.LocalName == "row"))
                    {
                        foreach (var cell in row.Elements().Where(node => node.Name.LocalName == "c"))
                        {
                            string value = cell.Elements().FirstOrDefault(node => node.Name.LocalName == "v") == null ? string.Concat(cell.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value)) : cell.Elements().First(node => node.Name.LocalName == "v").Value;
                            int index; if ((string)cell.Attribute("t") == "s" && int.TryParse(value, out index) && index >= 0 && index < shared.Length) value = shared[index];
                            result.Append((string)cell.Attribute("r") + ": " + value + "\t");
                        }
                        result.AppendLine(); if (result.Length > 2 * 1024 * 1024) throw new InvalidDataException("Planilha excede o limite de prévia.");
                    }
                }
                return result.ToString();
            }
        }
        private static XDocument Xml(ZipArchiveEntry entry)
        {
            if (entry == null) throw new InvalidDataException("Estrutura do documento não suportada.");
            using (var stream = entry.Open()) using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8 * 1024 * 1024 })) return XDocument.Load(reader);
        }
        private static string Paragraphs(XDocument document) { return string.Join("\n", document.Descendants().Where(node => node.Name.LocalName == "p").Select(paragraph => string.Concat(paragraph.Descendants().Where(node => node.Name.LocalName == "t").Select(node => node.Value)))); }
    }

    internal sealed class ArtifactWindow : Window
    {
        private readonly ContentControl preview;
        private readonly TextBlock status;
        private readonly Button previous, next;
        private readonly string path;
        private PdfPreview pdf;
        private uint page;
        private bool closed, reading;
        public ArtifactWindow(Window owner, string path)
        {
            this.path = path; Owner = owner; Title = "Prévia | " + Path.GetFileName(path); Width = 850; Height = 680; MinWidth = 500; MinHeight = 380; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Theme.Background; Foreground = Theme.Text;
            var root = new DockPanel { Margin = new Thickness(20) }; status = new TextBlock { Text = "Prévia local somente leitura. Nenhum arquivo é enviado.", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            previous = Theme.Button("Página anterior", async delegate { if (page > 0 && !reading) { page--; await RenderPdf(); } }, false); previous.IsEnabled = false; buttons.Children.Add(previous);
            next = Theme.Button("Próxima página", async delegate { if (pdf != null && page + 1 < pdf.PageCount && !reading) { page++; await RenderPdf(); } }, false); next.IsEnabled = false; buttons.Children.Add(next);
            buttons.Children.Add(Theme.Button("Abrir no Windows", delegate { try { DesktopActions.Open(path, Path.GetDirectoryName(path), requested => DesktopActions.Review(this, "Abrir arquivo", requested), DesktopActions.Launch); } catch (Exception error) { status.Text = error.Message; } }, false));
            buttons.Children.Add(Theme.Button("Fechar", delegate { Close(); }, true)); preview = new ContentControl(); root.Children.Add(preview); Content = root; Theme.BindTypography(this); Loaded += async delegate { await Load(); }; Closed += delegate { closed = true; };
        }
        private async Task Load()
        {
            try
            {
                if (new FileInfo(path).Length > 50 * 1024 * 1024) throw new InvalidDataException("Limite da prévia: 50 MB.");
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".pdf") { pdf = new PdfPreview(); await pdf.Load(path); await RenderPdf(); }
                else if (new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp" }.Contains(extension))
                {
                    var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 1400; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
                    if (!closed) preview.Content = new ScrollViewer { Content = new Image { Source = bitmap, Stretch = Stretch.Uniform }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                }
                else
                {
                    string text = await Task.Run(() => ArtifactText.Read(path)); if (closed) return;
                    preview.Content = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(12), FontFamily = Theme.FontWithFallback("Consolas") };
                }
            }
            catch (Exception error) { if (!closed) status.Text = "Prévia indisponível: " + error.Message; }
        }
        private async Task RenderPdf()
        {
            if (closed || pdf == null || pdf.PageCount == 0) return; reading = true; previous.IsEnabled = next.IsEnabled = false;
            try { var bitmap = await pdf.Page(page); if (!closed) { preview.Content = new ScrollViewer { Content = new Image { Source = bitmap, Stretch = Stretch.Uniform }, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; status.Text = "PDF somente leitura · página " + (page + 1) + " de " + pdf.PageCount; } }
            catch (Exception error) { if (!closed) status.Text = "Falha ao renderizar PDF: " + error.Message; }
            finally { reading = false; if (!closed) { previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < pdf.PageCount; } }
        }
    }
}
