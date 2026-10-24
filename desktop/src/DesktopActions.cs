using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal static class DesktopActions
    {
        // Documents open directly. Programs, scripts and unknown associations are reviewed first.
        private static readonly HashSet<string> Documents = new HashSet<string>(new[] {
            ".txt", ".md", ".pdf", ".docx", ".odt", ".rtf", ".xlsx", ".xls", ".ods", ".csv", ".tsv", ".pptx", ".odp",
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".ico", ".tif", ".tiff", ".mp3", ".wav", ".flac", ".ogg", ".mp4", ".mkv", ".mov", ".avi", ".json", ".log"
        }, StringComparer.OrdinalIgnoreCase);

        public static string ResolvePath(string input, string folder)
        {
            if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Informe o caminho do arquivo ou da pasta.");
            string path = Environment.ExpandEnvironmentVariables(input.Trim().Trim('"'));
            if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) || path.IndexOf("://", StringComparison.Ordinal) >= 0)
                throw new ArgumentException("Use um arquivo ou pasta local, sem URL nem caminho de rede.");
            int colon = path.IndexOf(':');
            if (colon >= 0 && (colon != 1 || path.IndexOf(':', colon + 1) >= 0)) throw new ArgumentException("Caminho local invalido.");
            if (path.StartsWith("~\\", StringComparison.Ordinal) || path.StartsWith("~/", StringComparison.Ordinal)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.Substring(2));
            string full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(folder, path));
            if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.Substring(Path.GetPathRoot(full).Length).Contains(":")) throw new ArgumentException("Caminho local invalido.");
            if (!File.Exists(full) && !Directory.Exists(full)) throw new FileNotFoundException("Arquivo ou pasta nao encontrado: " + full);
            return full;
        }

        public static bool NeedsConfirmation(string path) { return !Directory.Exists(path) && !Documents.Contains(Path.GetExtension(path)); }

        public static string Open(string input, string folder, Func<string, bool> confirm, Action<string> opener)
        {
            string path = ResolvePath(input, folder);
            if (NeedsConfirmation(path) && !confirm(path)) return "A abertura foi recusada pelo usuario.";
            opener(path);
            return "Aberto no aplicativo padrao do Windows: " + path;
        }

        public static void Launch(string path) { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }

        public static bool Review(Window owner, string title, string description)
        {
            var window = new Window { Title = title, Width = 620, Height = 440, MinWidth = 400, MinHeight = 280, Background = Theme.Background, Foreground = Theme.Text, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Topmost = owner != null && owner.Topmost };
            if (owner != null && owner.IsVisible) window.Owner = owner;
            var layout = new DockPanel { Margin = new Thickness(22) };
            var heading = new TextBlock { Text = title, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) };
            DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            buttons.Children.Add(Theme.Button("Recusar", delegate { window.DialogResult = false; }, false));
            buttons.Children.Add(Theme.Button("Autorizar uma vez", delegate { window.DialogResult = true; }, true));
            DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            layout.Children.Add(new TextBox { Text = description, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(12), FontSize = 13 });
            window.Content = layout;
            Theme.BindTypography(window); return window.ShowDialog() == true;
        }

        public static bool Review(Window owner, NormalizedApprovalRequest request)
        {
            var window = new Window { Title = request.Title, Width = 620, Height = 500, MinWidth = 400, MinHeight = 300, Background = Theme.Background, Foreground = Theme.Text, FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false, Topmost = owner != null && owner.Topmost };
            if (owner != null && owner.IsVisible) window.Owner = owner;
            var layout = new DockPanel { Margin = new Thickness(22) }; var heading = new TextBlock { Text = request.Title, FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) }; buttons.Children.Add(Theme.Button("Recusar", delegate { window.DialogResult = false; }, false)); buttons.Children.Add(Theme.Button("Autorizar uma vez", delegate { window.DialogResult = true; }, true)); DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            var body = new StackPanel(); AddReviewSection(body, null, request.Summary);
            AddReviewSection(body, "Motivo", request.Reason); AddReviewSection(body, "Comando", request.Command); AddReviewSection(body, "Pasta", request.WorkingDirectory); AddReviewSection(body, "Destino de rede", request.NetworkTarget); AddReviewSection(body, "Raiz concedida", request.GrantRoot);
            if (request.FileChanges.Count > 0) AddReviewSection(body, "Arquivos", string.Join("\n", request.FileChanges.Select(change => "• " + change.Path + (string.IsNullOrEmpty(change.Kind) ? "" : " (" + change.Kind + ")"))));
            if (request.Permissions.Count > 0) AddReviewSection(body, "Permissões ou ações", string.Join("\n", request.Permissions.Select(value => "• " + value)));
            string raw = Json.Encode(request.RawParameters) + (request.RawItem.Count == 0 ? "" : "\n\nItem associado\n" + Json.Encode(request.RawItem));
            body.Children.Add(new Expander { Header = "Detalhes técnicos", Foreground = Theme.Muted, Margin = new Thickness(0, 14, 0, 0), Content = new TextBox { Text = raw, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8), FontSize = 11 } });
            layout.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); window.Content = layout; Theme.BindTypography(window); return window.ShowDialog() == true;
        }

        private static void AddReviewSection(StackPanel body, string title, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!string.IsNullOrWhiteSpace(title)) body.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 11, 0, 3) });
            body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Text, FontSize = 13 });
        }
    }
}
