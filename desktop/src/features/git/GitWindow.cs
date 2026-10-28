using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Negotchi
{
    internal sealed class GitEntry
    {
        public string Code;
        public string Path;
        public string OriginalPath;
        public string Display { get { return Code + "  " + Path; } }
    }

    internal sealed class GitCommit
    {
        public string Id;
        public string[] Parents;
        public string Decorations;
        public string Subject;
        public int Lane;
    }

    internal sealed class GitGraphPoint
    {
        public GitCommit Commit;
        public double X, Y;
    }

    internal static class GitGraph
    {
        private static readonly Brush[] Colors = { Brushes.DeepSkyBlue, Brushes.MediumSeaGreen, Brushes.Goldenrod, Brushes.MediumOrchid, Brushes.IndianRed, Brushes.DarkTurquoise };
        internal static List<GitCommit> Read(string text)
        {
            var commits = new List<GitCommit>();
            foreach (string record in (text ?? "").Split(new[] { '\x1e' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] fields = record.Trim('\r', '\n').Split(new[] { '\x1f' });
                if (fields.Length < 4 || string.IsNullOrWhiteSpace(fields[0])) continue;
                commits.Add(new GitCommit { Id = fields[0], Parents = fields[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), Decorations = fields[2], Subject = fields[3] });
            }
            var lanes = new List<string>();
            foreach (var commit in commits)
            {
                int lane = lanes.IndexOf(commit.Id);
                if (lane < 0) { lane = lanes.IndexOf(null); if (lane < 0) { lane = lanes.Count; lanes.Add(null); } }
                commit.Lane = lane; lanes[lane] = commit.Parents.Length == 0 ? null : commit.Parents[0];
                for (int parent = 1; parent < commit.Parents.Length; parent++)
                {
                    int parentLane = lanes.IndexOf(null); if (parentLane < 0) { parentLane = lanes.Count; lanes.Add(null); }
                    lanes[parentLane] = commit.Parents[parent];
                }
            }
            return commits;
        }

        internal static Canvas Draw(IEnumerable<GitCommit> commits)
        {
            var items = commits.ToList(); var canvas = new Canvas { Background = Theme.Card, MinWidth = 500, Height = Math.Max(90, items.Count * 34 + 18) };
            var points = items.Select((commit, index) => new GitGraphPoint { Commit = commit, X = 24 + commit.Lane * 27, Y = 18 + index * 34 }).ToArray();
            var positions = points.ToDictionary(point => point.Commit.Id, point => point);
            foreach (var point in points)
            foreach (string parent in point.Commit.Parents)
            {
                GitGraphPoint target;
                if (positions.TryGetValue(parent, out target)) canvas.Children.Add(new Line { X1 = point.X, Y1 = point.Y, X2 = target.X, Y2 = target.Y, Stroke = Colors[point.Commit.Lane % Colors.Length], StrokeThickness = 2, SnapsToDevicePixels = true });
                else canvas.Children.Add(new Line { X1 = point.X, Y1 = point.Y, X2 = point.X, Y2 = Math.Min(canvas.Height - 8, point.Y + 22), Stroke = Colors[point.Commit.Lane % Colors.Length], StrokeThickness = 2 });
            }
            foreach (var point in points)
            {
                var node = new Ellipse { Width = 12, Height = 12, Fill = Colors[point.Commit.Lane % Colors.Length], Stroke = Theme.Background, StrokeThickness = 2, ToolTip = point.Commit.Id };
                Canvas.SetLeft(node, point.X - 6); Canvas.SetTop(node, point.Y - 6); canvas.Children.Add(node);
                string labels = string.IsNullOrWhiteSpace(point.Commit.Decorations) ? "" : "  " + point.Commit.Decorations.Replace("HEAD -> ", "");
                var text = new TextBlock { Text = point.Commit.Id.Substring(0, Math.Min(8, point.Commit.Id.Length)) + "  " + point.Commit.Subject + labels, Foreground = Theme.Text, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Width = 460, ToolTip = point.Commit.Subject + labels };
                Canvas.SetLeft(text, 110); Canvas.SetTop(text, point.Y - 9); canvas.Children.Add(text);
            }
            if (items.Count == 0) canvas.Children.Add(new TextBlock { Text = "Nenhum commit encontrado.", Foreground = Theme.Muted, Margin = new Thickness(12) });
            return canvas;
        }
    }

    internal static class GitWindow
    {
        public static void Show(Window owner, string folder)
        {
            string root;
            try { root = Run(folder, "rev-parse --show-toplevel").Trim(); if (root.Length == 0) throw new InvalidOperationException(); }
            catch { CommandDialogs.Info(owner, "Git", "A pasta de trabalho não pertence a um repositório Git."); return; }
            var window = new Window { Title = "Git", Width = 760, Height = 680, MinWidth = 540, MinHeight = 440, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Theme.Background, Foreground = Theme.Text, ShowInTaskbar = false };
            var layout = new DockPanel { Margin = new Thickness(20) }; var heading = new TextBlock { FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(heading, Dock.Top); layout.Children.Add(heading);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            var graph = new ScrollViewer { Height = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(graph, Dock.Top); layout.Children.Add(graph);
            var content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.42, GridUnitType.Star) }); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.58, GridUnitType.Star) });
            var list = new ListBox { DisplayMemberPath = "Display", Foreground = Theme.Text, Padding = new Thickness(2) }; Grid.SetColumn(list, 0); content.Children.Add(list);
            var diff = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(10), FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(12, 0, 0, 0) }; Grid.SetColumn(diff, 1); content.Children.Add(diff); layout.Children.Add(content);
            Action refresh = delegate {
                string status = Run(root, "status --porcelain=v1 -b"); var lines = status.Replace("\r", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries); heading.Text = lines.Length == 0 ? "Git" : lines[0].StartsWith("## ") ? lines[0].Substring(3) : "Git";
                list.ItemsSource = lines.Skip(lines.Length > 0 && lines[0].StartsWith("## ") ? 1 : 0).Where(line => line.Length >= 4).Select(line => new GitEntry { Code = line.Substring(0, 2), Path = line.Substring(3) }).ToList(); diff.Clear();
                graph.Content = GitGraph.Draw(GitGraph.Read(Run(root, "log --all --topo-order --date-order -n 120 --pretty=format:%H%x1f%P%x1f%D%x1f%s%x1e")));
            };
            list.SelectionChanged += delegate { var entry = list.SelectedItem as GitEntry; if (entry == null) return; try { string args = entry.Code[0] != ' ' ? "diff --cached -- " + Quote(entry.Path) : "diff -- " + Quote(entry.Path); diff.Text = Run(root, args); } catch (Exception error) { diff.Text = error.Message; } };
            buttons.Children.Add(Theme.Button("Atualizar", delegate { refresh(); }, false));
            buttons.Children.Add(Theme.Button("Stage", delegate { var entry = list.SelectedItem as GitEntry; if (entry == null) return; Run(root, "add -- " + Quote(entry.Path)); refresh(); }, false));
            buttons.Children.Add(Theme.Button("Unstage", delegate { var entry = list.SelectedItem as GitEntry; if (entry == null) return; Run(root, "restore --staged -- " + Quote(entry.Path)); refresh(); }, false));
            buttons.Children.Add(Theme.Button("Reverter", delegate { var entry = list.SelectedItem as GitEntry; if (entry == null) return; if (!DesktopActions.Review(window, "Reverter alteração", "As alterações locais deste arquivo serão descartadas:\n\n" + entry.Path)) return; Run(root, "restore -- " + Quote(entry.Path)); refresh(); }, false));
            buttons.Children.Add(Theme.Button("Commit", delegate { string message = CommitMessage(window); if (string.IsNullOrWhiteSpace(message)) return; if (!DesktopActions.Review(window, "Criar commit", "Criar um commit com a mensagem:\n\n" + message)) return; Run(root, "commit -m " + Quote(message)); refresh(); }, false));
            buttons.Children.Add(Theme.Button("Push", async delegate {
                try
                {
                    string branch = Run(root, "symbolic-ref --quiet --short HEAD").Trim();
                    var remotes = Run(root, "remote").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (remotes.Length == 0) { CommandDialogs.Info(window, "Push", "Este repositório não possui remoto configurado."); return; }
                    string remote = remotes.Contains("origin") ? "origin" : remotes[0];
                    if (remotes.Length > 1) remote = CommandDialogs.Prompt(window, "Remoto do push", "Escolha um remoto configurado: " + string.Join(", ", remotes), remote);
                    if (string.IsNullOrWhiteSpace(remote)) return;
                    if (!remotes.Contains(remote)) throw new ArgumentException("Escolha um remoto que já esteja configurado no repositório.");
                    string destination = Run(root, "remote get-url --push " + Quote(remote)).Trim();
                    if (!DesktopActions.Review(window, "Publicar commits", "Enviar a branch " + branch + " para " + remote + "/" + branch + ".\n\nRemoto: " + RedactRemote(destination) + "\n\nO envio não usa force push.")) return;
                    buttons.IsEnabled = false; heading.Text = "Enviando commits…";
                    string output = await Task.Run(() => Run(root, PushArguments(remote, branch)));
                    refresh(); CommandDialogs.Info(window, "Push concluído", string.IsNullOrWhiteSpace(output) ? "O Git concluiu o envio." : output);
                }
                catch (Exception error) { CommandDialogs.Info(window, "Falha no push", error.Message); }
                finally { buttons.IsEnabled = true; }
            }, false));
            buttons.Children.Add(Theme.Button("Fechar", delegate { window.Close(); }, true)); window.Content = layout; Theme.BindTypography(window); refresh(); window.ShowDialog();
        }
        internal static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
        internal static string PushArguments(string remote, string branch)
        {
            if (string.IsNullOrWhiteSpace(remote) || remote.StartsWith("-", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(branch) || branch.StartsWith("-", StringComparison.Ordinal)) throw new ArgumentException("Remoto ou branch inválido.");
            return "push --set-upstream -- " + Quote(remote) + " " + Quote("refs/heads/" + branch + ":refs/heads/" + branch);
        }
        internal static string RedactRemote(string address)
        {
            Uri uri; if (Uri.TryCreate(address, UriKind.Absolute, out uri) && !string.IsNullOrEmpty(uri.UserInfo)) return new UriBuilder(uri) { UserName = "", Password = "", Query = "", Fragment = "" }.Uri.ToString();
            return address;
        }
        private static string CommitMessage(Window owner)
        {
            var window = new Window { Title = "Mensagem do commit", Width = 440, Height = 220, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Theme.Background, Foreground = Theme.Text, ShowInTaskbar = false };
            var layout = new DockPanel { Margin = new Thickness(20) }; var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons);
            var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8) }; layout.Children.Add(input);
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.DialogResult = false; }, false)); buttons.Children.Add(Theme.Button("Continuar", delegate { if (!string.IsNullOrWhiteSpace(input.Text)) window.DialogResult = true; }, true)); window.Content = layout; Theme.BindTypography(window); return window.ShowDialog() == true ? input.Text.Trim() : null;
        }
        internal static string Run(string folder, string arguments)
        {
            var info = new ProcessStartInfo("git.exe", "-c i18n.logOutputEncoding=utf-8 -c core.quotepath=false " + arguments) { WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            using (var process = Process.Start(info)) { string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); process.WaitForExit(); if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "O Git não concluiu a operação." : error.Trim()); return output; }
        }
    }
}
