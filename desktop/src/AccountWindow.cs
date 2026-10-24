using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal sealed class AccountWindow : Window
    {
        private readonly CodexClient client;
        private readonly TextBlock status;
        private readonly TextBox details;
        private readonly Button refresh;
        private bool closed;
        public AccountWindow(Window owner, CodexClient client)
        {
            this.client = client; Owner = owner; Title = "Perfil e atividade"; Width = 620; Height = 560; MinWidth = 420; MinHeight = 350; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text; ShowInTaskbar = false;
            var root = new DockPanel { Margin = new Thickness(22) }; status = new TextBlock { Text = "Consultando conta…", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            refresh = Theme.Button("Atualizar", async delegate { await Refresh(); }, false); buttons.Children.Add(refresh); buttons.Children.Add(Theme.Button("Fechar", delegate { Close(); }, true));
            details = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(12) }; root.Children.Add(details); Content = root; Theme.BindTypography(this); Loaded += async delegate { await Refresh(); }; Closed += delegate { closed = true; };
        }
        private async Task Refresh()
        {
            refresh.IsEnabled = false; status.Text = "Consultando conta, limites e atividade…";
            try
            {
                var commands = new CodexCommands(client); var account = commands.Read("account/read", Json.Map("refreshToken", false)); var limits = commands.Read("account/rateLimits/read", null); var usage = commands.Read("account/usage/read", null); await Task.WhenAll(account, limits, usage); if (closed) return;
                var identity = Json.Object(account.Result.Data, "account");
                details.Text = "PERFIL\nConta: " + (Json.Text(identity, "email").Length > 0 ? Json.Text(identity, "email") : "E-mail não informado") + "\nPlano: " + (Json.Text(identity, "planType").Length > 0 ? Json.Text(identity, "planType") : "Não informado") + "\n\n" + CodexCommands.UsageText(account.Result, limits.Result, usage.Result, null);
                var days = Json.Items(usage.Result.Data, "dailyUsageBuckets").OfType<System.Collections.Generic.Dictionary<string, object>>().ToArray();
                details.AppendText(days.Length == 0 ? "\n\nAtividade diária não informada." : "\n\nATIVIDADE DIÁRIA\n" + string.Join("\n", days.Select(day => Json.Text(day, "startDate") + ": " + Json.Text(day, "tokens") + " tokens")));
                status.Text = account.Result.Error != null ? "Falha na consulta da conta; os outros dados estão identificados abaixo." : Json.Get(account.Result.Data, "account") == null ? "Nenhuma conta conectada." : limits.Result.Error != null || usage.Result.Error != null ? "Consulta parcial: há dados indisponíveis." : "Conta consultada. Dados ausentes são identificados como não informados.";
            }
            catch (Exception error) { if (!closed) { details.Clear(); status.Text = "Não foi possível consultar: " + error.Message; } }
            finally { if (!closed) refresh.IsEnabled = true; }
        }
    }
}
