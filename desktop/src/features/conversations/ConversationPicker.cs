using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Negotchi
{
    internal sealed class ConversationPicker : Window
    {
        private readonly ListBox list;
        private readonly TextBox search;
        private readonly TextBlock status, details;
        private readonly TextBox preview;
        private readonly Func<HistoryConversation, string, Task> rename;
        private readonly Func<HistoryConversation, bool, Task> setArchived;
        private readonly Func<HistoryConversation, CancellationToken, Task<string>> readPreview;
        private CancellationTokenSource previewLoading;
        private bool changing;
        private readonly Button scope, archive, sort, resume;
        private readonly Func<bool, CancellationToken, Task<List<HistoryConversation>>> load;
        private CancellationTokenSource loading;
        private List<HistoryConversation> entries = new List<HistoryConversation>();
        private readonly string folder;
        private bool currentFolder, archived, created, closed;
        public HistoryConversation Selected { get; private set; }
        public bool ManageLocal { get; private set; }

        public ConversationPicker(Window owner, string folder, Func<bool, CancellationToken, Task<List<HistoryConversation>>> load, Func<HistoryConversation, string, Task> rename = null, Func<HistoryConversation, bool, Task> setArchived = null, Func<HistoryConversation, CancellationToken, Task<string>> readPreview = null)
        {
            this.folder = folder; this.load = load; this.rename = rename; this.setArchived = setArchived; this.readPreview = readPreview;
            Title = "Conversas | Codex"; Owner = owner; Width = 960; Height = 580; MinWidth = 700; MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false; Background = Theme.Background; Foreground = Theme.Text;
            FontFamily = Theme.FontWithFallback("Cascadia Mono, Consolas"); FontSize = 13;
            var root = new DockPanel { Margin = new Thickness(24) };
            var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            top.Children.Add(new TextBlock { Text = "Retomar uma conversa", FontWeight = FontWeights.Bold, FontSize = 18, Margin = new Thickness(0, 0, 0, 16) });
            var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) }; top.Children.Add(filters);
            filters.Children.Add(Label("Filtro:")); scope = Option("Todas as pastas", delegate { currentFolder = !currentFolder; scope.Content = currentFolder ? "Pasta atual" : "Todas as pastas"; ApplyFilter(); }); filters.Children.Add(scope);
            filters.Children.Add(Label("Status:")); archive = Option("Ativas", async delegate { archived = !archived; archive.Content = archived ? "Arquivadas" : "Ativas"; resume.Content = archived ? "Restaurar e retomar ↵" : "Retomar ↵"; await Reload(); }); filters.Children.Add(archive);
            filters.Children.Add(Label("Ordenar:")); sort = Option("Atualização ↓", delegate { created = !created; sort.Content = created ? "Criação ↓" : "Atualização ↓"; ApplyFilter(); }); filters.Children.Add(sort);
            top.Children.Add(Label("Digite para buscar por título, prévia, pasta ou ID"));
            search = new TextBox { Background = Brushes.Transparent, Foreground = Theme.Text, CaretBrush = Theme.Text, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(3, 8, 3, 8), Margin = new Thickness(0, 0, 0, 10), ToolTip = "Digite para buscar por título, prévia, pasta ou ID" }; top.Children.Add(search);
            search.TextChanged += delegate { ApplyFilter(); };
            var header = new Grid { Margin = new Thickness(22, 0, 0, 5) }; Columns(header); header.Children.Add(Label("DATA")); var cwd = Label("PASTA"); Grid.SetColumn(cwd, 1); header.Children.Add(cwd); var title = Label("CONVERSA"); Grid.SetColumn(title, 2); header.Children.Add(title); top.Children.Add(header);
            var bottom = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            preview = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 135, Visibility = Visibility.Collapsed, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8) }; bottom.Children.Add(preview);
            details = new TextBlock { Foreground = Theme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8) }; bottom.Children.Add(details);
            status = new TextBlock { Foreground = Theme.Muted, Margin = new Thickness(0, 0, 0, 10) }; bottom.Children.Add(status);
            bottom.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 0, 0, 10) });
            bottom.Children.Add(new TextBlock { Text = "↑/↓ navegar   enter retomar   esc fechar   ctrl+r atualizar   ctrl+t prévia", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap });
            var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) }; bottom.Children.Add(actions);
            actions.Children.Add(Option("Projetos locais", delegate { ManageLocal = true; Close(); }));
            actions.Children.Add(Option("Atualizar", async delegate { await Reload(); }));
            if (rename != null) actions.Children.Add(Option("Renomear", async delegate { await RenameSelected(); }));
            if (setArchived != null) actions.Children.Add(Option("Arquivar / restaurar", async delegate { await ArchiveSelected(); }));
            if (readPreview != null) actions.Children.Add(Option("Prévia", async delegate { await TogglePreview(); }));
            resume = Option("Retomar ↵", delegate { Accept(); }); actions.Children.Add(resume);
            list = new ListBox { Background = Brushes.Transparent, Foreground = Theme.Text, BorderThickness = new Thickness(0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
            var style = new Style(typeof(ListBoxItem)); style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3, 8, 3, 8)));
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter))); style.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ListBoxItem)) { VisualTree = border }));
            var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true }; selected.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Text)); selected.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Background)); style.Triggers.Add(selected); list.ItemContainerStyle = style;
            list.ItemTemplate = RowTemplate(false); root.Children.Add(list);
            list.SelectionChanged += async delegate { var entry = list.SelectedItem as HistoryConversation; details.Text = entry == null ? "" : entry.Folder + "  ·  " + entry.Id; details.ToolTip = details.Text; if (preview.Visibility == Visibility.Visible) await LoadPreview(); };
            list.MouseDoubleClick += delegate { Accept(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (args.Key == Key.Escape) { args.Handled = true; Close(); }
                else if (args.Key == Key.Enter && (search.IsKeyboardFocusWithin || list.IsKeyboardFocusWithin)) { args.Handled = true; Accept(); }
                else if ((args.Key == Key.Down || args.Key == Key.Up) && search.IsKeyboardFocused) { args.Handled = true; if (list.Items.Count > 0) { list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, list.SelectedIndex + (args.Key == Key.Down ? 1 : -1))); list.ScrollIntoView(list.SelectedItem); } }
                else if (args.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control) { args.Handled = true; ReloadFromKey(); }
                else if (args.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control) { args.Handled = true; TogglePreviewFromKey(); }
            };
            Content = root;
            Loaded += async delegate { search.Focus(); await Reload(); };
            Closed += delegate { closed = true; if (loading != null) loading.Cancel(); if (previewLoading != null) previewLoading.Cancel(); };
        }
        private static TextBlock Label(string text) { return new TextBlock { Text = text, Foreground = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0) }; }
        private static Button Option(string text, RoutedEventHandler action) { var result = Theme.Button(text, action, false); result.Padding = new Thickness(8, 4, 8, 4); return result; }
        private static void Columns(Grid grid) { grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); }
        private static DataTemplate RowTemplate(bool created)
        {
            var row = new FrameworkElementFactory(typeof(DockPanel));
            var marker = new FrameworkElementFactory(typeof(TextBlock)); marker.SetValue(TextBlock.TextProperty, "› "); marker.SetValue(FrameworkElement.WidthProperty, 22.0); row.AppendChild(marker);
            foreach (var field in new[] { new { Name = created ? "Created" : "Updated", Width = 125.0 }, new { Name = "Folder", Width = 210.0 }, new { Name = "DisplayName", Width = 0.0 } })
            {
                var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new Binding(field.Name)); text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); text.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0)); if (field.Width > 0) text.SetValue(FrameworkElement.WidthProperty, field.Width); row.AppendChild(text);
            }
            return new DataTemplate { VisualTree = row };
        }
        private async void ReloadFromKey() { await Reload(); }
        private async void TogglePreviewFromKey() { await TogglePreview(); }
        private async Task TogglePreview()
        {
            if (readPreview == null) return;
            preview.Visibility = preview.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            if (preview.Visibility == Visibility.Visible) await LoadPreview(); else if (previewLoading != null) previewLoading.Cancel();
        }
        private async Task LoadPreview()
        {
            if (previewLoading != null) previewLoading.Cancel(); var request = previewLoading = new CancellationTokenSource();
            var entry = list.SelectedItem as HistoryConversation; preview.Text = entry == null ? "Selecione uma conversa." : "Carregando prévia…";
            if (entry == null) return;
            try { var text = await readPreview(entry, request.Token); if (!closed && !request.IsCancellationRequested) preview.Text = text; }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!closed && !request.IsCancellationRequested) preview.Text = "Prévia indisponível: " + error.Message; }
        }
        private async Task RenameSelected()
        {
            var entry = list.SelectedItem as HistoryConversation; if (entry == null || changing || !list.IsEnabled) return;
            string name = CommandDialogs.Prompt(this, "Renomear conversa", "O nome será atualizado no histórico desta conversa.", entry.DisplayName); if (name == null) return;
            changing = true;
            try { await rename(entry, name); await Reload(); }
            catch (Exception error) { status.Text = "Não foi possível renomear: " + error.Message; }
            finally { changing = false; }
        }
        private async Task ArchiveSelected()
        {
            var entry = list.SelectedItem as HistoryConversation; if (entry == null || changing || !list.IsEnabled) return;
            bool value = !entry.Archived;
            if (!DesktopActions.Review(this, value ? "Arquivar conversa" : "Restaurar conversa", entry.DisplayName + "\n\n" + (value ? "O histórico será preservado na lista de arquivadas." : "A conversa voltará à lista de ativas."))) return;
            changing = true;
            try { await setArchived(entry, value); await Reload(); }
            catch (Exception error) { status.Text = "Não foi possível atualizar: " + error.Message; }
            finally { changing = false; }
        }
        private async Task Reload()
        {
            if (loading != null) loading.Cancel(); var request = loading = new CancellationTokenSource();
            status.Text = "Carregando sessões…"; list.IsEnabled = false;
            try { var result = await load(archived, request.Token); if (closed || request.IsCancellationRequested) return; entries = result; list.IsEnabled = true; ApplyFilter(); }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!closed && !request.IsCancellationRequested) { entries.Clear(); ApplyFilter(); status.Text = "Não foi possível carregar. Ctrl+R para tentar novamente."; status.ToolTip = error.Message; } }
        }
        internal void ApplyFilter()
        {
            var selected = list.SelectedItem as HistoryConversation;
            var rows = entries.Where(entry => (!currentFolder || string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase)) && entry.Matches(search.Text)).OrderByDescending(entry => created ? entry.CreatedAt : entry.UpdatedAt).ToArray();
            list.ItemTemplate = RowTemplate(created); list.ItemsSource = rows; list.SelectedItem = rows.FirstOrDefault(entry => selected != null && entry.Id == selected.Id) ?? rows.FirstOrDefault();
            status.Text = rows.Length == 0 ? "Nenhuma conversa encontrada." : rows.Length + " conversas · " + (currentFolder ? "pasta atual" : "todas as pastas"); status.ToolTip = null;
        }
        private void Accept() { if (!list.IsEnabled || changing) return; var entry = list.SelectedItem as HistoryConversation; if (entry == null) return; Selected = entry; Close(); }
    }
}
