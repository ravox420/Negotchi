using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal static class TerminalPolicy
    {
        internal static Dictionary<string, object> Sandbox(string mode, string folder)
        {
            return mode == "danger-full-access" ? Json.Map("type", "dangerFullAccess") : mode == "workspace-write" ? Json.Map("type", "workspaceWrite", "writableRoots", new[] { folder }, "networkAccess", false) : Json.Map("type", "readOnly", "networkAccess", false);
        }
        internal static Dictionary<string, object> Command(string text, string folder, string sandbox, string processId)
        {
            string script = "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false); $OutputEncoding = [Console]::OutputEncoding;\r\n" + text;
            return Json.Map("command", new[] { "powershell.exe", "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(script)) }, "cwd", folder, "sandboxPolicy", Sandbox(sandbox, folder), "processId", processId, "streamStdoutStderr", true, "timeoutMs", 300000, "outputBytesCap", 1024 * 1024);
        }
    }
    internal sealed class TerminalWindow : Window
    {
        private sealed class Session
        {
            public string ProcessId;
            public TextBox Output, Input;
            public Button Run, Stop;
            public bool Running;
            public readonly Decoder Stdout = Encoding.UTF8.GetDecoder(), Stderr = Encoding.UTF8.GetDecoder();
        }
        private readonly CodexClient client;
        private readonly string folder, sandbox;
        private readonly TabControl tabs;
        private readonly List<Session> sessions = new List<Session>();
        private bool closed;
        public TerminalWindow(Window owner, CodexClient client, string folder, string sandbox)
        {
            this.client = client; this.folder = folder; this.sandbox = sandbox; Owner = owner; Title = "Terminais da conversa"; Width = 900; Height = 650; MinWidth = 550; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text; ShowInTaskbar = false;
            var root = new DockPanel { Margin = new Thickness(18) }; var header = new TextBlock { Text = folder + "\nPolítica: " + sandbox + ". Cada comando inicia nesta pasta; limite de 5 minutos e 1 MB de saída por fluxo.", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            var buttons = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) }; buttons.Children.Add(Theme.Button("Nova aba", delegate { AddTab(); }, false)); DockPanel.SetDock(buttons, Dock.Top); root.Children.Add(buttons);
            tabs = new TabControl { Background = Theme.Background, Foreground = Theme.Text, BorderBrush = Theme.Line }; root.Children.Add(tabs); Content = root; Theme.BindTypography(this); AddTab(); client.Notification += Output;
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs args) { if (sessions.Exists(session => session.Running) && !DesktopActions.Review(this, "Encerrar terminais", "Fechar e interromper os processos destas abas?")) args.Cancel = true; };
            Closed += async delegate { closed = true; client.Notification -= Output; foreach (var session in sessions) if (session.Running) { try { await client.RequestAsync("command/exec/terminate", Json.Map("processId", session.ProcessId)); } catch (Exception) { } } };
        }
        private void AddTab()
        {
            var session = new Session(); sessions.Add(session); var root = new DockPanel { Margin = new Thickness(8) };
            var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            session.Input = new TextBox { AcceptsReturn = true, Height = 70, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8), FontFamily = Theme.FontWithFallback("Consolas") }; bottom.Children.Add(session.Input);
            var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; bottom.Children.Add(actions);
            session.Run = Theme.Button("Executar", async delegate { await Run(session); }, true); actions.Children.Add(session.Run); session.Stop = Theme.Button("Interromper processo", async delegate { if (!session.Running) return; try { await client.RequestAsync("command/exec/terminate", Json.Map("processId", session.ProcessId)); } catch (Exception error) { session.Output.AppendText("\n" + error.Message); } }, false); session.Stop.IsEnabled = false; actions.Children.Add(session.Stop);
            session.Output = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(10), FontFamily = Theme.FontWithFallback("Consolas") }; root.Children.Add(session.Output);
            var tab = new TabItem { Header = "PowerShell " + sessions.Count, Content = root }; tabs.Items.Add(tab); tabs.SelectedItem = tab; Theme.BindTypography(root);
        }
        private async Task Run(Session session)
        {
            string text = session.Input.Text; if (string.IsNullOrWhiteSpace(text) || session.Running) return;
            if (!DesktopActions.Review(this, "Executar no terminal", "Pasta: " + folder + "\nSandbox: " + sandbox + "\n\n" + text)) return;
            session.ProcessId = "negotchi-terminal-" + Guid.NewGuid().ToString("N"); session.Stdout.Reset(); session.Stderr.Reset(); session.Running = true; session.Run.IsEnabled = false; session.Stop.IsEnabled = true; session.Input.IsEnabled = false; session.Output.AppendText("\n> " + text + "\n");
            try { var result = await client.RequestAsync("command/exec", TerminalPolicy.Command(text, folder, sandbox, session.ProcessId), 315000); if (!closed) session.Output.AppendText("\n[Processo encerrado: " + Json.Text(result, "exitCode") + "]\n" + Json.Text(result, "stdout") + Json.Text(result, "stderr")); }
            catch (Exception error) { if (!closed) session.Output.AppendText("\nFalha: " + error.Message + "\n"); try { await client.RequestAsync("command/exec/terminate", Json.Map("processId", session.ProcessId)); } catch (Exception) { } }
            finally { session.Running = false; if (!closed) { session.Run.IsEnabled = true; session.Stop.IsEnabled = false; session.Input.IsEnabled = true; } }
        }
        private void Output(string method, Dictionary<string, object> parameters)
        {
            if (method != "command/exec/outputDelta") return; string id = Json.Text(parameters, "processId"); var session = sessions.Find(item => item.Running && item.ProcessId == id); if (session == null) return;
            try
            {
                var bytes = Convert.FromBase64String(Json.Text(parameters, "deltaBase64")); var chars = new char[bytes.Length + 4]; var decoder = Json.Text(parameters, "stream") == "stderr" ? session.Stderr : session.Stdout; int count = decoder.GetChars(bytes, 0, bytes.Length, chars, 0, false); string text = new string(chars, 0, count);
                Dispatcher.BeginInvoke(new Action(delegate { if (closed) return; session.Output.AppendText(text); if (Equals(Json.Get(parameters, "capReached"), true)) session.Output.AppendText("\n[Limite de saída atingido]\n"); session.Output.ScrollToEnd(); }));
            }
            catch (FormatException) { }
        }
    }
}
