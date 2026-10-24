using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal static class WorkspaceFiles
    {
        internal static string Resolve(string root, string path)
        {
            root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = DesktopActions.ResolvePath(path, root);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("O arquivo está fora da pasta desta conversa.");
            string current = full;
            while (!string.IsNullOrEmpty(current) && !string.Equals(current.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new UnauthorizedAccessException("Links e junctions não são percorridos pelo explorador."); current = Path.GetDirectoryName(current);
            }
            return full;
        }
        internal static IEnumerable<string> Children(string root, string path)
        {
            path = Resolve(root, path);
            return Directory.EnumerateFileSystemEntries(path).Where(item => (File.GetAttributes(item) & FileAttributes.ReparsePoint) == 0).OrderBy(item => Directory.Exists(item) ? 0 : 1).ThenBy(item => Path.GetFileName(item), StringComparer.OrdinalIgnoreCase).ToArray();
        }
        internal static List<string> Search(string root, string query, CancellationToken cancellation)
        {
            var pending = new Queue<string>(); pending.Enqueue(root); var result = new List<string>();
            while (pending.Count > 0 && result.Count < 1000)
            {
                cancellation.ThrowIfCancellationRequested(); string folder = pending.Dequeue();
                try { foreach (string path in Children(root, folder)) { cancellation.ThrowIfCancellationRequested(); if (Directory.Exists(path)) pending.Enqueue(path); else if (Path.GetFileName(path).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) result.Add(path); if (result.Count >= 1000) break; } }
                catch (UnauthorizedAccessException) { } catch (IOException) { }
            }
            return result;
        }
    }
    internal sealed class FileExplorerWindow : Window
    {
        private readonly string rootPath;
        private string selected;
        private readonly TextBlock status;
        private readonly TreeView tree;
        private readonly ListBox results;
        private CancellationTokenSource searching;
        private bool closed;
        public FileExplorerWindow(Window owner, string rootPath, Action<string> reference, string sandbox = "read-only", Action<string> annotate = null)
        {
            this.rootPath = Path.GetFullPath(rootPath); Owner = owner; Title = "Arquivos da conversa"; Width = 800; Height = 590; MinWidth = 520; MinHeight = 380; WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Theme.Background; Foreground = Theme.Text;
            var root = new DockPanel { Margin = new Thickness(20) }; status = new TextBlock { Text = this.rootPath, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(row, Dock.Top); root.Children.Add(row);
            var search = new TextBox { Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8), ToolTip = "Buscar por nome na pasta da conversa" };
            var find = Theme.Button("Buscar", async delegate { if (searching != null) searching.Cancel(); var request = searching = new CancellationTokenSource(); string query = search.Text.Trim(); try { var found = await Task.Run(() => WorkspaceFiles.Search(this.rootPath, query, request.Token)); if (closed || request.IsCancellationRequested) return; results.ItemsSource = found; results.Visibility = Visibility.Visible; tree.Visibility = Visibility.Collapsed; status.Text = found.Count + " arquivos encontrados (limite 1000)."; } catch (OperationCanceledException) { } catch (Exception error) { if (!closed) status.Text = error.Message; } }, false); DockPanel.SetDock(find, Dock.Right); row.Children.Add(find); row.Children.Add(search);
            var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            buttons.Children.Add(Theme.Button("Árvore", delegate { if (searching != null) searching.Cancel(); results.Visibility = Visibility.Collapsed; tree.Visibility = Visibility.Visible; }, false));
            buttons.Children.Add(Theme.Button("Prévia", delegate { OpenSelected(true); }, false)); buttons.Children.Add(Theme.Button("Abrir no Windows", delegate { OpenSelected(false); }, false));
            buttons.Children.Add(Theme.Button("Editar / anotar", delegate { try { if (selected != null && File.Exists(selected)) new TextEditorWindow(this, this.rootPath, selected, sandbox, annotate ?? reference).ShowDialog(); } catch (Exception error) { status.Text = error.Message; } }, false));
            buttons.Children.Add(Theme.Button("Referenciar", delegate { try { if (selected == null) return; reference(WorkspaceFiles.Resolve(this.rootPath, selected)); Close(); } catch (Exception error) { status.Text = error.Message; } }, false));
            buttons.Children.Add(Theme.Button("Fechar", delegate { Close(); }, true));
            var content = new Grid(); tree = new TreeView { Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line }; content.Children.Add(tree); results = new ListBox { Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Visibility = Visibility.Collapsed }; content.Children.Add(results); root.Children.Add(content);
            tree.SelectedItemChanged += delegate { var item = tree.SelectedItem as TreeViewItem; selected = item == null ? null : item.Tag as string; };
            results.SelectionChanged += delegate { selected = results.SelectedItem as string; };
            tree.MouseDoubleClick += delegate { if (selected != null && File.Exists(selected)) OpenSelected(true); }; results.MouseDoubleClick += delegate { OpenSelected(true); };
            var rootItem = Node(this.rootPath); tree.Items.Add(rootItem); rootItem.IsExpanded = true; Content = root; Theme.BindTypography(this); Closed += delegate { closed = true; if (searching != null) searching.Cancel(); };
        }
        private TreeViewItem Node(string path)
        {
            var node = new TreeViewItem { Header = Path.GetFileName(path), Tag = path, Foreground = Theme.Text }; if (Directory.Exists(path)) { node.Items.Add("Carregando…"); node.Expanded += delegate(object sender, RoutedEventArgs args) { if (args.OriginalSource != node || node.Items.Count != 1 || !(node.Items[0] is string)) return; node.Items.Clear(); try { foreach (string child in WorkspaceFiles.Children(rootPath, path)) node.Items.Add(Node(child)); } catch (Exception error) { node.Items.Add("Não foi possível listar: " + error.Message); } }; } return node;
        }
        private void OpenSelected(bool preview)
        {
            if (selected == null) return;
            try { string path = WorkspaceFiles.Resolve(rootPath, selected); if (preview && File.Exists(path)) new ArtifactWindow(this, path).ShowDialog(); else DesktopActions.Open(path, rootPath, requested => DesktopActions.Review(this, "Abrir arquivo", requested), DesktopActions.Launch); }
            catch (Exception error) { status.Text = error.Message; }
        }
    }
}
