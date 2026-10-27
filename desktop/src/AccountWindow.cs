using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Negotchi
{
    internal sealed class AccountWindow : Window
    {
        private readonly CodexClient client;
        private readonly Func<Dictionary<string, object>> conversationTokens;
        private readonly TextBlock status;
        private readonly StackPanel details;
        private readonly Button refresh;
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pt-BR");
        private bool closed;

        public AccountWindow(Window owner, CodexClient client, Func<Dictionary<string, object>> conversationTokens)
        {
            this.client = client; this.conversationTokens = conversationTokens;
            Owner = owner; Title = "Perfil e atividade"; Width = 660; Height = 650; MinWidth = 420; MinHeight = 350; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text; ShowInTaskbar = false;
            var root = new DockPanel { Margin = new Thickness(22) };
            status = Label("Consultando conta…", true); status.Margin = new Thickness(0, 0, 0, 12); DockPanel.SetDock(status, Dock.Top); root.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            refresh = Theme.Button("Atualizar", async delegate { await Refresh(); }, false); buttons.Children.Add(refresh); buttons.Children.Add(Theme.Button("Fechar", delegate { Close(); }, true));
            details = new StackPanel(); root.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = details });
            Content = root; Theme.BindTypography(this); Loaded += async delegate { await Refresh(); }; Closed += delegate { closed = true; };
        }
        private static TextBlock Label(string text, bool muted)
        {
            return new TextBlock { Text = text, Foreground = muted ? Theme.Muted : Theme.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 3) };
        }
        private StackPanel Section(string title)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 16, Foreground = Theme.Text, Margin = new Thickness(0, 0, 0, 8) });
            details.Children.Add(new Border { Background = Theme.Card, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14), Margin = new Thickness(0, 0, 0, 10), Child = panel });
            return panel;
        }
        private static void Bar(StackPanel panel, string text, double value, double maximum)
        {
            panel.Children.Add(Label(text, false));
            var bar = new ProgressBar { Minimum = 0, Maximum = maximum > 0 ? maximum : 1, Value = Math.Max(0, Math.Min(value, maximum > 0 ? maximum : 1)), Height = 10, Margin = new Thickness(0, 2, 0, 9), Foreground = Theme.Accent, Background = Theme.Line, BorderThickness = new Thickness(0) };
            System.Windows.Automation.AutomationProperties.SetName(bar, text);
            panel.Children.Add(bar);
        }
        internal static bool Number(object value, out double number)
        {
            return value != null && double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number) && number >= 0 || MissingNumber(out number);
        }
        private static bool MissingNumber(out double number) { number = 0; return false; }
        private static string Metric(Dictionary<string, object> data, string key)
        {
            double value; return Number(Json.Get(data, key), out value) ? value.ToString("N0", Culture) : "Não informado";
        }
        private static string WindowName(Dictionary<string, object> window, string key)
        {
            double minutes; if (!Number(Json.Get(window, "windowDurationMins"), out minutes) || minutes <= 0) return key == "primary" ? "Janela principal" : "Janela secundária";
            return minutes % 1440 == 0 ? (minutes / 1440).ToString("0.##", Culture) + " dias" : minutes % 60 == 0 ? (minutes / 60).ToString("0.##", Culture) + " horas" : minutes.ToString("0.##", Culture) + " minutos";
        }
        private async Task Refresh()
        {
            refresh.IsEnabled = false; status.Text = "Consultando conta, limites e atividade…";
            try
            {
                var commands = new CodexCommands(client); var account = commands.Read("account/read", Json.Map("refreshToken", false)); var limits = commands.Read("account/rateLimits/read", null); var usage = commands.Read("account/usage/read", null);
                await Task.WhenAll(account, limits, usage); if (closed) return;
                ShowData(account.Result, limits.Result, usage.Result, conversationTokens == null ? null : conversationTokens());
                status.Text = account.Result.Error != null ? "Falha na consulta da conta; os outros dados estão identificados abaixo." : Json.Get(account.Result.Data, "account") == null ? "Nenhuma conta conectada." : limits.Result.Error != null || usage.Result.Error != null ? "Consulta parcial: há dados indisponíveis." : "Dados atualizados.";
            }
            catch (Exception error) { if (!closed) { details.Children.Clear(); status.Text = "Não foi possível consultar: " + error.Message; } }
            finally { if (!closed) refresh.IsEnabled = true; }
        }
        internal void ShowData(RpcRead account, RpcRead limits, RpcRead usage, Dictionary<string, object> tokens)
        {
            details.Children.Clear();
            var profile = Section("Perfil"); var identity = Json.Object(account.Data, "account");
            profile.Children.Add(Label(account.Error != null ? "Conta indisponível: " + account.Error : identity.Count == 0 ? "Conta não conectada." : "Conta: " + (Json.Text(identity, "email").Length > 0 ? Json.Text(identity, "email") : "E-mail não informado") + "\nPlano: " + (Json.Text(identity, "planType").Length > 0 ? Json.Text(identity, "planType") : "Não informado"), false));
            var quota = Section("Limites de uso da conta");
            if (limits.Error != null) quota.Children.Add(Label("Indisponíveis: " + limits.Error, true));
            else
            {
                var buckets = Json.Object(limits.Data, "rateLimitsByLimitId");
                var snapshots = buckets.Count > 0 ? buckets.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => entry.Value).OfType<Dictionary<string, object>>().ToArray() : new[] { Json.Object(limits.Data, "rateLimits") };
                int count = 0;
                foreach (var snapshot in snapshots) foreach (string key in new[] { "primary", "secondary" })
                {
                    var window = Json.Object(snapshot, key); double used;
                    if (!Number(Json.Get(window, "usedPercent"), out used) || used > 100) continue;
                    string name = Json.Text(snapshot, "limitName"); if (name.Length == 0) name = Json.Text(snapshot, "limitId");
                    string reset = ""; double timestamp;
                    if (Number(Json.Get(window, "resetsAt"), out timestamp)) try { reset = " · renova em " + new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture); } catch (ArgumentOutOfRangeException) { }
                    Bar(quota, (name.Length > 0 ? name + " · " : "") + WindowName(window, key) + ": " + used.ToString("0.##", Culture) + "% usado · " + (100 - used).ToString("0.##", Culture) + "% disponível" + reset, used, 100); count++;
                }
                if (count == 0) quota.Children.Add(Label("Janelas de limite não informadas.", true));
                if (Equals(Json.Get(limits.Data, "ordinaryUsageAllowed"), false)) quota.Children.Add(Label("A conta informa que o uso normal está bloqueado.", true));
            }
            var activity = Section("Atividade de tokens da conta");
            if (usage.Error != null) activity.Children.Add(Label("Indisponível: " + usage.Error, true));
            else
            {
                var summary = Json.Object(usage.Data, "summary");
                activity.Children.Add(Label("Tokens acumulados: " + Metric(summary, "lifetimeTokens") + "\nMaior uso diário: " + Metric(summary, "peakDailyTokens") + "\nDias consecutivos: " + Metric(summary, "currentStreakDays") + "\nMaior sequência de dias: " + Metric(summary, "longestStreakDays"), false));
                var days = Json.Items(usage.Data, "dailyUsageBuckets").OfType<Dictionary<string, object>>().Where(day => { double n; return Json.Text(day, "startDate").Length > 0 && Number(Json.Get(day, "tokens"), out n); }).OrderBy(day => Json.Text(day, "startDate"), StringComparer.Ordinal).ToArray();
                if (days.Length == 0) activity.Children.Add(Label("Atividade diária não informada.", true));
                else
                {
                    activity.Children.Add(Label("Tokens por dia", true)); double max = days.Max(day => Convert.ToDouble(Json.Get(day, "tokens"), CultureInfo.InvariantCulture));
                    foreach (var day in days) { double value = Convert.ToDouble(Json.Get(day, "tokens"), CultureInfo.InvariantCulture); DateTime date; string raw = Json.Text(day, "startDate"); string dateText = DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ? date.ToString("dd/MM/yyyy", Culture) : raw; Bar(activity, dateText + " · " + value.ToString("N0", Culture) + " tokens", value, max); }
                }
            }
            var conversation = Section("Tokens da conversa ativa"); var total = Json.Object(tokens, "total"); double input, output;
            conversation.Children.Add(Label("Total acumulado: " + Metric(total, "totalTokens") + "\nEntrada em cache: " + Metric(total, "cachedInputTokens") + "\nRaciocínio: " + Metric(total, "reasoningOutputTokens") + "\nÚltimo turno: " + Metric(Json.Object(tokens, "last"), "totalTokens") + "\nJanela de contexto: " + Metric(tokens, "modelContextWindow"), false));
            if (Number(Json.Get(total, "inputTokens"), out input) && Number(Json.Get(total, "outputTokens"), out output))
            {
                double max = input + output; Bar(conversation, "Entrada: " + input.ToString("N0", Culture), input, max); Bar(conversation, "Saída: " + output.ToString("N0", Culture), output, max);
            }
            else conversation.Children.Add(Label("Detalhamento de entrada e saída ainda não informado pelo Codex.", true));
        }
    }
}
