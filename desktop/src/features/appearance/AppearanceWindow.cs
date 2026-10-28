using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Negotchi
{
    internal sealed class AppearanceWindow : Window
    {
        private AppearanceSettings draft;
        private readonly ComboBox mode, font;
        private readonly Slider fontSize;
        private readonly TextBlock sizeLabel;
        private readonly Dictionary<string, Button> colorButtons = new Dictionary<string, Button>();
        private readonly Border preview;
        private bool loading;
        public AppearanceSettings SelectedSettings { get; private set; }
        public AppearanceWindow(Window owner, AppearanceSettings settings)
        {
            draft = Theme.Normalize(settings);
            Title = "Aparencia"; Width = 780; Height = 760; MinWidth = 620; MinHeight = 540;
            if (owner != null && owner.IsVisible) Owner = owner;
            ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text;
            var root = new DockPanel { Margin = new Thickness(22), LastChildFill = true }; Content = root;
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            heading.Children.Add(new TextBlock { Text = "Aparencia", FontSize = 22, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = "Escolha um tema base e personalize cada cor pela roda seletora. As mudancas sao salvas ao aplicar.", FontSize = 12, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            buttons.Children.Add(Theme.Button("Restaurar padrao", delegate { draft = Theme.Preset("dark"); LoadControls(); }, false));
            buttons.Children.Add(Theme.Button("Cancelar", delegate { Close(); }, false));
            buttons.Children.Add(Theme.Button("Aplicar", delegate {
                SelectedSettings = Theme.Normalize(draft); DialogResult = true;
            }, true));
            var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            var typography = new Grid { Margin = new Thickness(0, 0, 12, 20) };
            typography.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            typography.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            typography.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            var themeColumn = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; themeColumn.Children.Add(Label("Tema base"));
            mode = new ComboBox { ItemsSource = new[] { "Escuro", "Claro" }, MinHeight = 34 }; themeColumn.Children.Add(mode); typography.Children.Add(themeColumn);
            var fontColumn = new StackPanel { Margin = new Thickness(0, 0, 16, 0) }; fontColumn.Children.Add(Label("Fonte"));
            var families = Fonts.SystemFontFamilies.Select(family => family.Source).Concat(new[] { draft.FontFamily }).Distinct().OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            font = new ComboBox { ItemsSource = families, MinHeight = 34 }; fontColumn.Children.Add(font); Grid.SetColumn(fontColumn, 1); typography.Children.Add(fontColumn);
            var sizeColumn = new StackPanel(); sizeLabel = Label("Tamanho da fonte: 13"); sizeColumn.Children.Add(sizeLabel);
            fontSize = new Slider { Minimum = 10, Maximum = 22, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 8, 0, 0), SmallChange = 1, LargeChange = 2 }; sizeColumn.Children.Add(fontSize); Grid.SetColumn(sizeColumn, 2); typography.Children.Add(sizeColumn); body.Children.Add(typography);
            var columns = new Grid { Margin = new Thickness(0, 0, 12, 0) };
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) }); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var colors = new StackPanel { Margin = new Thickness(0, 0, 20, 0) }; colors.Children.Add(Label("Cores")); columns.Children.Add(colors);
            foreach (var color in Theme.Colors)
            {
                var option = color;
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 7), LastChildFill = true };
                var button = Theme.Button("", delegate {
                    var picker = new ColorPickerWindow(this, option.Label, draft.Colors[option.Key]);
                    if (picker.ShowDialog() == true) { draft.Colors[option.Key] = picker.SelectedColor; UpdateColorButtons(); UpdatePreview(); }
                }, false);
                button.Width = 112; button.Margin = new Thickness(8, 0, 0, 0); button.Padding = new Thickness(7, 6, 7, 6); button.ToolTip = "Abrir roda seletora: " + option.Label;
                System.Windows.Automation.AutomationProperties.SetName(button, "Cor: " + option.Label);
                colorButtons.Add(option.Key, button); DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
                row.Children.Add(new TextBlock { Text = option.Label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }); colors.Children.Add(row);
            }
            var previewColumn = new StackPanel(); previewColumn.Children.Add(Label("Previa"));
            preview = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(12), BorderThickness = new Thickness(1) }; previewColumn.Children.Add(preview);
            previewColumn.Children.Add(new TextBlock { Text = "A fonte e as cores serao aplicadas ao chat, aos menus, aos controles e ao balao do pet.", FontSize = 11, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) }); Grid.SetColumn(previewColumn, 1); columns.Children.Add(previewColumn); body.Children.Add(columns);
            mode.SelectionChanged += delegate {
                if (loading) return;
                var preset = Theme.Preset(mode.SelectedIndex == 1 ? "light" : "dark"); preset.FontFamily = draft.FontFamily; preset.FontSize = draft.FontSize; draft = preset;
                UpdateColorButtons(); UpdatePreview();
            };
            font.SelectionChanged += delegate { if (!loading && font.SelectedItem != null) { draft.FontFamily = Convert.ToString(font.SelectedItem); UpdatePreview(); } };
            fontSize.ValueChanged += delegate { if (!loading) { draft.FontSize = fontSize.Value; sizeLabel.Text = "Tamanho da fonte: " + draft.FontSize.ToString("0"); UpdatePreview(); } };
            Theme.BindTypography(this); LoadControls();
        }
        private static TextBlock Label(string text) { return new TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap }; }
        private void LoadControls()
        {
            loading = true; mode.SelectedIndex = draft.Mode == "light" ? 1 : 0; font.SelectedItem = draft.FontFamily; fontSize.Value = draft.FontSize; sizeLabel.Text = "Tamanho da fonte: " + draft.FontSize.ToString("0"); loading = false;
            UpdateColorButtons(); UpdatePreview();
        }
        private void UpdateColorButtons()
        {
            foreach (var option in Theme.Colors)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(3), Background = Theme.Brush(draft.Colors[option.Key]), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new TextBlock { Text = draft.Colors[option.Key], FontSize = 11, VerticalAlignment = VerticalAlignment.Center }); colorButtons[option.Key].Content = row; Theme.BindTypography(row);
            }
        }
        private void UpdatePreview()
        {
            preview.Background = Theme.Brush(draft.Colors["Background"]); preview.BorderBrush = Theme.Brush(draft.Colors["Border"]);
            var content = new StackPanel { Margin = new Thickness(0), Background = Brushes.Transparent };
            TextBlock title = SampleText("Seu companheiro", "Text"); title.FontWeight = FontWeights.SemiBold; content.Children.Add(title);
            var secondary = SampleText("Texto secundario", "MutedText"); secondary.Margin = new Thickness(0, 5, 0, 12); content.Children.Add(secondary);
            content.Children.Add(SampleBubble("Bora conversar?", "SpeechBubble", "SpeechBubbleText"));
            content.Children.Add(SampleBubble("Sua mensagem aparece aqui.", "UserBubble", "UserBubbleText"));
            content.Children.Add(SampleBubble("A resposta do pet aparece aqui.", "AssistantBubble", "AssistantBubbleText"));
            var field = new Border { Background = Theme.Brush(draft.Colors["Surface"]), BorderBrush = Theme.Brush(draft.Colors["Border"]), BorderThickness = new Thickness(1), Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 10), Child = SampleText("Campo de mensagem", "Text") }; content.Children.Add(field);
            var accent = SampleText("Destaque", "Accent"); accent.Margin = new Thickness(0, 0, 0, 10); content.Children.Add(accent);
            foreach (bool selected in new[] { false, true })
            {
                var button = Theme.Button(selected ? "Selecionado / Enviar" : "Botao comum", delegate { }, selected);
                button.Background = Theme.Brush(draft.Colors[selected ? "SelectedButton" : "Button"]); button.Foreground = Theme.Brush(draft.Colors[selected ? "SelectedButtonText" : "ButtonText"]); button.BorderBrush = Theme.Brush(draft.Colors["Border"]); button.FontFamily = Theme.FontWithFallback(draft.FontFamily); button.FontSize = draft.FontSize * 12 / 13; button.Margin = new Thickness(0, 0, 0, 8); content.Children.Add(button);
            }
            preview.Child = content;
        }
        private TextBlock SampleText(string text, string key) { return new TextBlock { Text = text, Foreground = Theme.Brush(draft.Colors[key]), FontFamily = Theme.FontWithFallback(draft.FontFamily), FontSize = draft.FontSize, TextWrapping = TextWrapping.Wrap }; }
        private Border SampleBubble(string text, string background, string foreground) { return new Border { Background = Theme.Brush(draft.Colors[background]), CornerRadius = new CornerRadius(8), Padding = new Thickness(9), Margin = new Thickness(0, 0, 0, 10), Child = SampleText(text, foreground) }; }
    }
}
