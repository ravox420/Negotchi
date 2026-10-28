using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    public sealed class ReviewComment
    {
        public string File { get; set; }
        public int Line { get; set; }
        public string Selection { get; set; }
        public string Note { get; set; }
        public string Revision { get; set; }
        public string Display { get { return File + ":" + Line + " — " + Note; } }
    }
    internal sealed class DiffHunk
    {
        public int Before, After;
        public string Display { get; set; }
    }
    internal static class GitReview
    {
        internal static string Revision(string text) { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text))); }
        internal static List<GitEntry> Files(string root)
        {
            var fields = GitWindow.Run(root, "status --porcelain=v1 -z").Split('\0'); var result = new List<GitEntry>();
            for (int index = 0; index < fields.Length; index++)
            {
                string field = fields[index]; if (field.Length < 4) continue; string code = field.Substring(0, 2);
                var entry = new GitEntry { Code = code, Path = field.Substring(3) }; result.Add(entry); if ((code.Contains("R") || code.Contains("C")) && index + 1 < fields.Length) entry.OriginalPath = fields[++index];
            }
            return result;
        }
        internal static string Before(string root, string relative)
        {
            string spec = "HEAD:" + relative.Replace('\\', '/');
            try { long size = long.Parse(GitWindow.Run(root, "cat-file -s " + GitWindow.Quote(spec)).Trim()); if (size > 2 * 1024 * 1024) throw new InvalidDataException("Versão anterior excede o limite de 2 MB."); }
            catch (InvalidOperationException) { return ""; }
            return GitWindow.Run(root, "show " + GitWindow.Quote(spec));
        }
        internal static List<DiffHunk> Hunks(string patch)
        {
            return Regex.Matches(patch, "^@@ -(\\d+)(?:,\\d+)? \\+(\\d+)(?:,\\d+)? @@.*$", RegexOptions.Multiline).Cast<Match>().Select(match => new DiffHunk { Before = int.Parse(match.Groups[1].Value), After = int.Parse(match.Groups[2].Value), Display = match.Value.Trim() }).ToList();
        }
    }
    internal sealed class DiffReviewWindow : Window
    {
        private readonly string rootPath;
        private readonly List<ReviewComment> comments;
        private readonly TextBox before, after;
        private readonly ListBox files, hunks, notes;
        private readonly TextBlock status;
        private string selectedPath, revision;
        private int loadVersion;
        private bool closed;
        public DiffReviewWindow(Window owner, string folder, List<ReviewComment> comments, Action<string> sendContext)
        {
            rootPath = GitWindow.Run(folder, "rev-parse --show-toplevel").Trim(); this.comments = comments;
            Owner = owner; Title = "Revisão de alterações"; Width = 1100; Height = 740; MinWidth = 720; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Theme.Background; Foreground = Theme.Text;
            var root = new DockPanel { Margin = new Thickness(18) }; status = new TextBlock { Text = "Somente revisão: selecionar e comentar não aplica nem reverte alterações.", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            notes = new ListBox { DisplayMemberPath = "Display", Height = 70, Foreground = Theme.Text, Margin = new Thickness(0, 8, 0, 8) }; bottom.Children.Add(notes);
            var buttons = new WrapPanel(); bottom.Children.Add(buttons);
            buttons.Children.Add(Theme.Button("Atualizar arquivos", delegate { try { files.ItemsSource = GitReview.Files(rootPath); } catch (Exception error) { status.Text = error.Message; } }, false));
            buttons.Children.Add(Theme.Button("Trecho anterior", delegate { if (hunks.SelectedIndex > 0) hunks.SelectedIndex--; }, false)); buttons.Children.Add(Theme.Button("Próximo trecho", delegate { if (hunks.SelectedIndex + 1 < hunks.Items.Count) hunks.SelectedIndex++; }, false));
            buttons.Children.Add(Theme.Button("Comentar seleção", delegate {
                if (selectedPath == null || after.SelectionLength == 0) { status.Text = "Selecione o trecho na versão atual para comentar."; return; }
                if (after.SelectionLength > 8000) { status.Text = "Selecione até 8 mil caracteres."; return; }
                string note = CommandDialogs.Prompt(this, "Comentário de revisão", "O comentário fica vinculado ao arquivo e à versão revisada.", ""); if (note == null) return;
                comments.Add(new ReviewComment { File = selectedPath, Line = after.Text.Substring(0, after.SelectionStart).Count(character => character == '\n') + 1, Selection = after.SelectedText, Note = note, Revision = revision }); RefreshNotes();
            }, false));
            buttons.Children.Add(Theme.Button("Remover comentário", delegate { var note = notes.SelectedItem as ReviewComment; if (note != null) { comments.Remove(note); RefreshNotes(); } }, false));
            buttons.Children.Add(Theme.Button("Enviar ao rascunho", delegate {
                try
                {
                    foreach (var note in comments) { string path = WorkspaceFiles.Resolve(rootPath, Path.Combine(rootPath, note.File)); if (!File.Exists(path) || GitReview.Revision(ArtifactText.Read(path)) != note.Revision) throw new InvalidOperationException("O arquivo mudou desde a revisão: " + note.File + ". Atualize e revise novamente os comentários desse arquivo."); }
                    sendContext(string.Join("\n\n", comments.Select(note => "Revisão de \"" + Path.Combine(rootPath, note.File) + "\", linha " + note.Line + ":\nTrecho:\n" + note.Selection + "\nComentário do usuário: " + note.Note)) + "\n"); status.Text = "Comentários adicionados ao rascunho. Envie para solicitar alterações.";
                }
                catch (Exception error) { status.Text = error.Message; }
            }, true));
            buttons.Children.Add(Theme.Button("Fechar", delegate { Close(); }, false));
            var content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); content.ColumnDefinitions.Add(new ColumnDefinition());
            var sidebar = new DockPanel(); hunks = new ListBox { DisplayMemberPath = "Display", MaxHeight = 150, Foreground = Theme.Text }; DockPanel.SetDock(hunks, Dock.Bottom); sidebar.Children.Add(hunks);
            files = new ListBox { DisplayMemberPath = "Display", Foreground = Theme.Text }; sidebar.Children.Add(files); content.Children.Add(sidebar);
            var comparison = new Grid { Margin = new Thickness(10, 0, 0, 0) }; comparison.ColumnDefinitions.Add(new ColumnDefinition()); comparison.ColumnDefinitions.Add(new ColumnDefinition());
            before = TextPane(); after = TextPane(); before.ToolTip = "Versão do último commit (HEAD)"; after.ToolTip = "Versão atual no disco"; comparison.Children.Add(before); Grid.SetColumn(after, 1); comparison.Children.Add(after); Grid.SetColumn(comparison, 1); content.Children.Add(comparison); root.Children.Add(content);
            files.SelectionChanged += async delegate { await ReadSelected(); };
            hunks.SelectionChanged += delegate { var hunk = hunks.SelectedItem as DiffHunk; if (hunk != null) { if (before.LineCount > 0) before.ScrollToLine(Math.Min(before.LineCount - 1, Math.Max(0, hunk.Before - 1))); if (after.LineCount > 0) after.ScrollToLine(Math.Min(after.LineCount - 1, Math.Max(0, hunk.After - 1))); } };
            Content = root; Theme.BindTypography(this); files.ItemsSource = GitReview.Files(rootPath); RefreshNotes(); Closed += delegate { closed = true; loadVersion++; };
        }
        private static TextBox TextPane() { return new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(10), FontFamily = Theme.FontWithFallback("Consolas"), Margin = new Thickness(2) }; }
        private void RefreshNotes() { notes.ItemsSource = comments.ToArray(); }
        private async Task ReadSelected()
        {
            int request = ++loadVersion; selectedPath = null; before.Clear(); after.Clear(); hunks.ItemsSource = null;
            var entry = files.SelectedItem as GitEntry; if (entry == null) return;
            try
            {
                var result = await Task.Run(() => {
                    string path = Path.Combine(rootPath, entry.Path); if (File.Exists(path)) WorkspaceFiles.Resolve(rootPath, path);
                    if (!ArtifactText.IsText(path)) throw new NotSupportedException("Comparação textual indisponível para este formato.");
                    string previous = GitReview.Before(rootPath, entry.OriginalPath ?? entry.Path), current = File.Exists(path) ? ArtifactText.Read(path) : "", patch;
                    try { patch = GitWindow.Run(rootPath, "diff --no-ext-diff --no-textconv --unified=3 HEAD -- " + GitWindow.Quote(entry.Path)); } catch (InvalidOperationException) { patch = ""; }
                    return Tuple.Create(previous, current, patch);
                });
                if (closed || request != loadVersion) return; selectedPath = entry.Path; before.Text = result.Item1; after.Text = result.Item2; revision = GitReview.Revision(result.Item2); hunks.ItemsSource = GitReview.Hunks(result.Item3); if (hunks.Items.Count > 0) hunks.SelectedIndex = 0;
                status.Text = entry.Path + " · esquerda: HEAD · direita: disco · " + hunks.Items.Count + " trechos";
            }
            catch (Exception error) { if (!closed && request == loadVersion) status.Text = error.Message; }
        }
    }
}
