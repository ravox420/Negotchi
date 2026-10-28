using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Negotchi
{
    internal static class CommandDialogs
    {
        private static Window Window(Window owner, string title, double height)
        {
            var window = new Window { Title = title, Width = 560, Height = height, MinWidth = 420, MinHeight = 300, Background = Theme.Background, Foreground = Theme.Text, FontFamily = new FontFamily("Segoe UI"), ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            if (owner != null && owner.IsVisible) window.Owner = owner;
            return window;
        }
        private static DockPanel Layout(Window window, string heading, string hint)
        {
            var layout = new DockPanel { Margin = new Thickness(22), LastChildFill = true };
            var title = new TextBlock { Text = heading, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) }; DockPanel.SetDock(title, Dock.Top); layout.Children.Add(title);
            var description = new TextBlock { Text = hint, Foreground = Theme.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) }; DockPanel.SetDock(description, Dock.Top); layout.Children.Add(description);
            window.Content = layout; return layout;
        }
        private static StackPanel Buttons(DockPanel layout)
        {
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            DockPanel.SetDock(buttons, Dock.Bottom); layout.Children.Add(buttons); return buttons;
        }
        private static TextBlock Label(string text) { return new TextBlock { Text = text, Margin = new Thickness(0, 0, 0, 7), Foreground = Theme.Text, FontSize = 13 }; }

        public static void Info(Window owner, string title, string text)
        {
            var window = Window(owner, title, 540); var layout = Layout(window, title, "Dados e controles do Codex nesta interface.");
            Buttons(layout).Children.Add(Theme.Button("Fechar", delegate { window.Close(); }, true));
            layout.Children.Add(new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(13), FontSize = 13 });
            Theme.BindTypography(window); window.ShowDialog();
        }
        public static string Prompt(Window owner, string title, string hint, string current)
        {
            var window = Window(owner, title, 310); var layout = Layout(window, title, hint); var buttons = Buttons(layout); string result = null;
            var input = new TextBox { Text = current ?? "", Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8), MaxLength = 200 }; layout.Children.Add(input);
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.Close(); }, false));
            buttons.Children.Add(Theme.Button("Salvar", delegate { if (string.IsNullOrWhiteSpace(input.Text)) return; result = input.Text.Trim(); window.Close(); }, true));
            window.Loaded += delegate { input.Focus(); input.SelectAll(); }; Theme.BindTypography(window); window.ShowDialog(); return result;
        }

        public static ModelSelection Model(Window owner, List<ModelChoice> models, string current, string currentEffort)
        {
            var window = Window(owner, "Modelo do Codex", 460);
            var layout = Layout(window, "/model", "A selecao fica salva no Negotchi e vale para os proximos pedidos. O catalogo vem do Codex instalado.");
            var buttons = Buttons(layout); ModelSelection selection = null;
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.Close(); }, false));
            var body = new StackPanel(); body.Children.Add(Label("Modelo"));
            var model = new ComboBox { ItemsSource = models, DisplayMemberPath = "DisplayName", MinHeight = 34, Margin = new Thickness(0, 0, 0, 16) }; body.Children.Add(model);
            body.Children.Add(Label("Esforco de raciocinio"));
            var effort = new ComboBox { MinHeight = 34, Margin = new Thickness(0, 0, 0, 16) }; body.Children.Add(effort);
            var description = new TextBlock { Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 }; body.Children.Add(description);
            var apply = Theme.Button("Aplicar", delegate {
                var chosen = model.SelectedItem as ModelChoice;
                if (chosen == null) return;
                selection = new ModelSelection { Model = chosen.Model, Effort = Convert.ToString(effort.SelectedItem) }; window.DialogResult = true;
            }, true); buttons.Children.Add(apply);
            model.SelectionChanged += delegate {
                var chosen = model.SelectedItem as ModelChoice; apply.IsEnabled = chosen != null;
                if (chosen == null) return;
                effort.ItemsSource = chosen.Efforts; effort.IsEnabled = chosen.Efforts.Length > 0;
                effort.SelectedItem = chosen.Model == current && chosen.Efforts.Contains(currentEffort) ? currentEffort : chosen.DefaultEffort;
                if (effort.SelectedItem == null && chosen.Efforts.Length > 0) effort.SelectedIndex = 0;
                description.Text = chosen.Description;
            };
            model.SelectedItem = models.FirstOrDefault(m => m.Model == current) ?? models.FirstOrDefault(m => m.IsDefault) ?? models.FirstOrDefault();
            apply.IsEnabled = model.SelectedItem != null;
            layout.Children.Add(body); Theme.BindTypography(window); window.ShowDialog(); return selection;
        }

        public static SkillChoice Skills(Window owner, List<SkillChoice> skills, string filter)
        {
            var window = Window(owner, "Skills do Codex", 570);
            var layout = Layout(window, "/skills", "Escolha uma skill para anexar ao proximo pedido. A lista considera a pasta de trabalho atual.");
            var buttons = Buttons(layout); SkillChoice selection = null;
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.Close(); }, false));
            var search = new TextBox { Text = filter, MinHeight = 32, Padding = new Thickness(8), Background = Theme.Card, Foreground = Theme.Text, CaretBrush = Theme.Accent, BorderBrush = Theme.Line, Margin = new Thickness(0, 0, 0, 12), ToolTip = "Buscar skill por nome ou descricao" }; DockPanel.SetDock(search, Dock.Top); layout.Children.Add(search);
            var details = new TextBlock { Text = skills.Count == 0 ? "Nenhuma skill disponivel nesta pasta." : "Selecione uma skill para ver os detalhes.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12, MaxHeight = 125, Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(details, Dock.Bottom); layout.Children.Add(details);
            var list = new ListBox { Foreground = Theme.Text, Padding = new Thickness(2), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var use = Theme.Button("Usar skill", delegate { selection = list.SelectedItem as SkillChoice; if (selection != null && selection.Enabled) window.DialogResult = true; }, true); use.IsEnabled = false; buttons.Children.Add(use);
            Action update = delegate {
                string query = search.Text.Trim();
                list.ItemsSource = skills.Where(s => query.Length == 0 || s.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || s.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 || s.Description.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            };
            list.SelectionChanged += delegate {
                var chosen = list.SelectedItem as SkillChoice; use.IsEnabled = chosen != null && chosen.Enabled;
                details.Text = chosen == null ? "Selecione uma skill." : chosen.Description + "\n\n" + chosen.Path;
            };
            search.TextChanged += delegate { update(); }; update(); layout.Children.Add(list);
            Theme.BindTypography(window); window.ShowDialog(); return selection;
        }

        public static MemoryPreferences Memories(Window owner, MemoryPreferences current, RpcRead status)
        {
            var window = Window(owner, "Memorias do Codex", 570);
            var layout = Layout(window, "/memories", "As opcoes globais sao salvas no Codex deste computador. A permissao do chat controla sua contribuicao para futuras memorias.");
            var buttons = Buttons(layout); MemoryPreferences selection = null;
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.Close(); }, false));
            var body = new StackPanel(); body.Children.Add(Label("Configuracao global do Codex"));
            var enabled = MemoryCheck("Ativar o recurso de memorias", current.FeatureEnabled);
            var use = MemoryCheck("Usar memorias existentes em novos contextos", current.UseMemories);
            var generate = MemoryCheck("Permitir gerar memorias de novas conversas", current.GenerateMemories);
            body.Children.Add(enabled); body.Children.Add(use); body.Children.Add(generate);
            body.Children.Add(new TextBlock { Text = "Marcacao intermediaria: manter a configuracao atual. Uma memoria ja presente no historico nao e removida por essa troca.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 11, Margin = new Thickness(0, 0, 0, 20) });
            body.Children.Add(Label("Permissao desta conversa para gerar memorias"));
            var mode = new ComboBox { ItemsSource = new[] { "Manter o padrao atual", "Permitir contribuicao deste chat", "Nao usar este chat para gerar memorias" }, SelectedIndex = current.ThreadMode == "enabled" ? 1 : current.ThreadMode == "disabled" ? 2 : 0, MinHeight = 34, Margin = new Thickness(0, 0, 0, 18) }; body.Children.Add(mode);
            body.Children.Add(new TextBlock { Text = status.Error != null ? "Status indisponivel: " + status.Error : "Memorias consolidadas: " + Json.Text(status.Data, "v2ConsolidatedThreads") + ". " + (Equals(Json.Get(status.Data, "v2Ready"), true) ? "Base pronta para uso." : "A base ainda nao esta pronta."), TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12 });
            buttons.Children.Add(Theme.Button("Aplicar", delegate {
                selection = new MemoryPreferences { FeatureEnabled = enabled.IsChecked, UseMemories = use.IsChecked, GenerateMemories = generate.IsChecked, ThreadMode = mode.SelectedIndex == 1 ? "enabled" : mode.SelectedIndex == 2 ? "disabled" : null };
                window.DialogResult = true;
            }, true));
            layout.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Theme.BindTypography(window); window.ShowDialog(); return selection;
        }
        public static string Permissions(Window owner, string current)
        {
            var window = Window(owner, "Permissões", 310); var layout = Layout(window, "/permissions", "Alterações de arquivos continuam exigindo confirmação."); var buttons = Buttons(layout); string selection = null;
            var options = new ComboBox { ItemsSource = new[] { "Somente leitura", "Permitir alterações com confirmação", "Acesso completo (alto risco)" }, SelectedIndex = current == "danger-full-access" ? 2 : current == "workspace-write" ? 1 : 0, MinHeight = 34 }; layout.Children.Add(options);
            buttons.Children.Add(Theme.Button("Cancelar", delegate { window.Close(); }, false)); buttons.Children.Add(Theme.Button("Aplicar", delegate { if (options.SelectedIndex == 2 && !DesktopActions.Review(window, "Acesso completo", "O acesso completo remove as restrições do sandbox para os comandos desta conversa. Continue apenas se confiar na tarefa e revisar cada aprovação.")) return; selection = options.SelectedIndex == 2 ? "danger-full-access" : options.SelectedIndex == 1 ? "workspace-write" : "read-only"; window.DialogResult = true; }, true)); Theme.BindTypography(window); window.ShowDialog(); return selection;
        }
        private static CheckBox MemoryCheck(string label, bool? value) { return new CheckBox { Content = label, IsChecked = value, IsThreeState = true, Foreground = Theme.Text, Margin = new Thickness(0, 5, 0, 12), FontSize = 13 }; }
    }
}
