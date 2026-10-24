using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace Negotchi
{
    public sealed class AppearanceSettings
    {
        public string Mode { get; set; }
        public string FontFamily { get; set; }
        public double FontSize { get; set; }
        public Dictionary<string, string> Colors { get; set; }
        public AppearanceSettings() { Mode = "dark"; FontFamily = Theme.DefaultFontFamily; FontSize = 13; Colors = new Dictionary<string, string>(); }
    }

    internal sealed class ThemeColor : INotifyPropertyChanged
    {
        public readonly string Key, Label, Dark, Light;
        public readonly SolidColorBrush Brush;
        private Color value;
        public event PropertyChangedEventHandler PropertyChanged;
        public Color Value
        {
            get { return value; }
            set { this.value = value; if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Value")); }
        }
        public ThemeColor(string key, string label, string dark, string light)
        {
            Key = key; Label = label; Dark = dark; Light = light;
            value = (Color)ColorConverter.ConvertFromString(dark); Brush = new SolidColorBrush();
            // A bound brush stays live when WPF seals styles or caches drawings.
            BindingOperations.SetBinding(Brush, SolidColorBrush.ColorProperty, new Binding("Value") { Source = this });
        }
    }

    internal static partial class Theme
    {
        public const string DefaultFontFamily = "Segoe UI, Segoe UI Emoji, Segoe UI Symbol";
        public static readonly ThemeColor[] Colors = {
            new ThemeColor("Background", "Fundo da janela", "#17131C", "#F6F3F8"),
            new ThemeColor("Surface", "Paineis e campos", "#241D2B", "#FFFFFF"),
            new ThemeColor("Text", "Texto principal", "#F8F1FA", "#241D2B"),
            new ThemeColor("MutedText", "Texto secundario", "#B4A4BC", "#6C6172"),
            new ThemeColor("Border", "Bordas", "#3A2E43", "#D7CEDD"),
            new ThemeColor("Accent", "Destaques", "#F0A5C9", "#9A3D70"),
            new ThemeColor("Button", "Botao nao selecionado", "#241D2B", "#E9E2ED"),
            new ThemeColor("ButtonText", "Texto do botao nao selecionado", "#F8F1FA", "#241D2B"),
            new ThemeColor("SelectedButton", "Botao selecionado", "#F0A5C9", "#B44E82"),
            new ThemeColor("SelectedButtonText", "Texto do botao selecionado", "#17131C", "#FFFFFF"),
            new ThemeColor("AssistantBubble", "Balao de resposta", "#241D2B", "#FFFFFF"),
            new ThemeColor("AssistantBubbleText", "Texto da resposta", "#F8F1FA", "#241D2B"),
            new ThemeColor("UserBubble", "Balao da sua mensagem", "#F0A5C9", "#EDD8E6"),
            new ThemeColor("UserBubbleText", "Texto da sua mensagem", "#17131C", "#3C2033"),
            new ThemeColor("SpeechBubble", "Balao sobre o pet", "#F0A5C9", "#B44E82"),
            new ThemeColor("SpeechBubbleText", "Texto do balao sobre o pet", "#17131C", "#FFFFFF")
        };
        private static readonly Dictionary<string, ThemeColor> palette = Colors.ToDictionary(color => color.Key);
        private static readonly Dictionary<double, string> fontKeys = new Dictionary<double, string>();
        private static AppearanceSettings current = new AppearanceSettings();
        private const string FontFamilyKey = "Negotchi.FontFamily";
        private const string OperationalFontFamilyKey = "Negotchi.OperationalFontFamily";
        public const string ChatMinHeightKey = "Negotchi.ChatMinHeight";
        public static Brush Background { get { return palette["Background"].Brush; } }
        public static Brush Card { get { return palette["Surface"].Brush; } }
        public static Brush Text { get { return palette["Text"].Brush; } }
        public static Brush Muted { get { return palette["MutedText"].Brush; } }
        public static Brush Line { get { return palette["Border"].Brush; } }
        public static Brush Accent { get { return palette["Accent"].Brush; } }
        public static Brush ButtonBackground { get { return palette["Button"].Brush; } }
        public static Brush ButtonText { get { return palette["ButtonText"].Brush; } }
        public static Brush SelectedButton { get { return palette["SelectedButton"].Brush; } }
        public static Brush SelectedButtonText { get { return palette["SelectedButtonText"].Brush; } }
        public static Brush AssistantBubble { get { return palette["AssistantBubble"].Brush; } }
        public static Brush AssistantBubbleText { get { return palette["AssistantBubbleText"].Brush; } }
        public static Brush UserBubble { get { return palette["UserBubble"].Brush; } }
        public static Brush UserBubbleText { get { return palette["UserBubbleText"].Brush; } }
        public static Brush SpeechBubble { get { return palette["SpeechBubble"].Brush; } }
        public static Brush SpeechBubbleText { get { return palette["SpeechBubbleText"].Brush; } }

        public static bool IsColor(string value) { return value != null && Regex.IsMatch(value, "^#[0-9a-fA-F]{6}$"); }
        public static FontFamily FontWithFallback(string family)
        {
            var names = (family ?? "Segoe UI").Split(',').Select(name => name.Trim()).Where(name => name.Length > 0)
                .Concat(new[] { "Segoe UI", "Segoe UI Emoji", "Segoe UI Symbol" }).Distinct(StringComparer.OrdinalIgnoreCase);
            return new FontFamily(string.Join(", ", names));
        }
        public static SolidColorBrush Brush(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
        public static AppearanceSettings Preset(string mode)
        {
            var result = new AppearanceSettings { Mode = mode == "light" ? "light" : "dark" };
            foreach (var color in Colors) result.Colors[color.Key] = result.Mode == "light" ? color.Light : color.Dark;
            return result;
        }
        public static AppearanceSettings Normalize(AppearanceSettings value)
        {
            if (value == null) return Preset("dark");
            var result = Preset(value.Mode);
            if (!string.IsNullOrWhiteSpace(value.FontFamily) && value.FontFamily.Length <= 100)
            {
                string family = value.FontFamily.Trim();
                result.FontFamily = string.Equals(family, "Segoe UI", StringComparison.OrdinalIgnoreCase) ? DefaultFontFamily : family;
            }
            result.FontSize = double.IsNaN(value.FontSize) || double.IsInfinity(value.FontSize) || value.FontSize <= 0 ? 13 : Math.Max(10, Math.Min(22, value.FontSize));
            foreach (var color in Colors)
            {
                string custom;
                if (value.Colors != null && value.Colors.TryGetValue(color.Key, out custom) && IsColor(custom)) result.Colors[color.Key] = custom.ToUpperInvariant();
            }
            return result;
        }
        public static void Apply(AppearanceSettings settings)
        {
            current = Normalize(settings);
            foreach (var color in Colors) color.Value = (Color)ColorConverter.ConvertFromString(current.Colors[color.Key]);
            if (Application.Current == null) return;
            var resources = Application.Current.Resources;
            foreach (var color in Colors) resources["Negotchi.Color." + color.Key] = color.Brush;
            resources[FontFamilyKey] = FontWithFallback(current.FontFamily);
            resources[OperationalFontFamilyKey] = FontWithFallback("Cascadia Mono, Consolas");
            resources[ChatMinHeightKey] = 560 + Math.Max(0, current.FontSize - 13) * 16;
            foreach (var entry in fontKeys) resources[entry.Value] = entry.Key * current.FontSize / 13;
            resources[SystemColors.HighlightBrushKey] = SelectedButton;
            resources[SystemColors.HighlightTextBrushKey] = SelectedButtonText;
            resources[SystemColors.WindowBrushKey] = Card;
            resources[SystemColors.WindowTextBrushKey] = Text;
            resources[SystemColors.ControlBrushKey] = ButtonBackground;
            resources[SystemColors.ControlTextBrushKey] = ButtonText;
            if (!resources.Contains("Negotchi.ScrollThumb")) InstallScrollStyles(resources);
            InstallWindowChrome();
            foreach (Window window in Application.Current.Windows) ApplyWindowChrome(window);
            if (!resources.Contains(typeof(ComboBox))) InstallControlStyles(resources);
        }
        private static string FontKey(double size)
        {
            string key;
            if (!fontKeys.TryGetValue(size, out key)) { key = "Negotchi.FontSize." + size.ToString(CultureInfo.InvariantCulture); fontKeys.Add(size, key); }
            if (Application.Current != null) Application.Current.Resources[key] = size * current.FontSize / 13;
            return key;
        }
        public static DynamicResourceExtension ColorResource(string key) { return new DynamicResourceExtension("Negotchi.Color." + key); }
        // Keep the authored size hierarchy, while all existing controls follow the user's base size.
        public static void BindTypography(FrameworkElement root)
        {
            if (Application.Current != null && !Application.Current.Resources.Contains(FontFamilyKey)) Apply(current);
            BindSizes(root);
            root.SetResourceReference(TextElement.FontFamilyProperty, FontFamilyKey);
            if (root.ReadLocalValue(TextElement.FontSizeProperty) == DependencyProperty.UnsetValue) root.SetResourceReference(TextElement.FontSizeProperty, FontKey(13));
            if (root.ContextMenu != null) BindTypography(root.ContextMenu);
        }
        private static void BindSizes(DependencyObject node)
        {
            var element = node as FrameworkElement;
            if (element != null)
            {
                object size = element.ReadLocalValue(TextElement.FontSizeProperty);
                if (size is double) element.SetResourceReference(TextElement.FontSizeProperty, FontKey((double)size));
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) BindSizes(child);
        }
        private static void InstallControlStyles(ResourceDictionary resources)
        {
            var textBox = new Style(typeof(TextBox));
            textBox.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey)));
            textBox.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Surface")));
            textBox.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Text")));
            textBox.Setters.Add(new Setter(Control.BorderBrushProperty, ColorResource("Border")));
            textBox.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            textBox.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 6, 9, 6)));
            var textBorder = new FrameworkElementFactory(typeof(Border), "FieldBorder");
            textBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); textBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty)); textBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            var textContent = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost"); textContent.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty)); textContent.SetValue(UIElement.FocusableProperty, false); textBorder.AppendChild(textContent);
            var textTemplate = new ControlTemplate(typeof(TextBox)) { VisualTree = textBorder };
            var focused = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true }; focused.Setters.Add(new Setter(Border.BorderBrushProperty, ColorResource("Accent"), "FieldBorder")); textTemplate.Triggers.Add(focused);
            var readOnly = new Trigger { Property = TextBox.IsReadOnlyProperty, Value = true }; readOnly.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(FontFamilyKey))); textBox.Triggers.Add(readOnly);
            textBox.Setters.Add(new Setter(Control.TemplateProperty, textTemplate)); resources[typeof(TextBox)] = textBox;
            var item = new Style(typeof(ListBoxItem));
            item.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("ButtonText")));
            item.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            item.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(5, 8, 5, 8)));
            item.Setters.Add(new Setter(Control.TemplateProperty, ItemTemplate(typeof(ListBoxItem))));
            resources[typeof(ListBoxItem)] = item;
            var list = new Style(typeof(ListBox)); list.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent)); list.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0))); list.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey))); resources[typeof(ListBox)] = list;
            var comboItem = new Style(typeof(ComboBoxItem));
            comboItem.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("ButtonText")));
            comboItem.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            comboItem.Setters.Add(new Setter(Control.TemplateProperty, ItemTemplate(typeof(ComboBoxItem))));
            resources[typeof(ComboBoxItem)] = comboItem;
            var combo = new Style(typeof(ComboBox));
            combo.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey)));
            combo.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Button")));
            combo.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("ButtonText")));
            combo.Setters.Add(new Setter(Control.BorderBrushProperty, ColorResource("Border")));
            var grid = new FrameworkElementFactory(typeof(Grid));
            var toggle = new FrameworkElementFactory(typeof(ToggleButton));
            toggle.SetValue(Control.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            toggle.SetValue(Control.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            toggle.SetValue(Control.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            toggle.SetValue(Control.TemplateProperty, ButtonTemplate(typeof(ToggleButton)));
            toggle.SetValue(ContentControl.ContentProperty, "v"); toggle.SetValue(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right);
            toggle.SetValue(Control.PaddingProperty, new Thickness(10, 6, 10, 6)); toggle.SetValue(UIElement.FocusableProperty, false);
            toggle.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay }); grid.AppendChild(toggle);
            var chosen = new FrameworkElementFactory(typeof(ContentPresenter), "Selection");
            chosen.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemProperty));
            chosen.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ComboBox.SelectionBoxItemTemplateProperty));
            chosen.SetValue(FrameworkElement.MarginProperty, new Thickness(10, 4, 28, 4)); chosen.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); chosen.SetValue(UIElement.IsHitTestVisibleProperty, false); chosen.SetValue(UIElement.ClipToBoundsProperty, true); grid.AppendChild(chosen);
            var editor = new FrameworkElementFactory(typeof(TextBox), "PART_EditableTextBox");
            editor.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed); editor.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 4, 28, 4)); editor.SetValue(Control.BorderThicknessProperty, new Thickness(0)); editor.SetResourceReference(Control.BackgroundProperty, "Negotchi.Color.Surface"); editor.SetResourceReference(Control.ForegroundProperty, "Negotchi.Color.Text");
            editor.SetBinding(TextBox.TextProperty, new Binding("Text") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); grid.AppendChild(editor);
            var popup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Bottom); popup.SetValue(Popup.AllowsTransparencyProperty, true); popup.SetValue(UIElement.FocusableProperty, false);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsDropDownOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var popupBorder = new FrameworkElementFactory(typeof(Border)); popupBorder.SetResourceReference(Border.BackgroundProperty, "Negotchi.Color.Surface"); popupBorder.SetResourceReference(Border.BorderBrushProperty, "Negotchi.Color.Border"); popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetBinding(FrameworkElement.MinWidthProperty, new Binding("ActualWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var scroll = new FrameworkElementFactory(typeof(ScrollViewer)); scroll.SetValue(FrameworkElement.MaxHeightProperty, 260.0); scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter))); popupBorder.AppendChild(scroll); popup.AppendChild(popupBorder); grid.AppendChild(popup);
            var comboTemplate = new ControlTemplate(typeof(ComboBox)) { VisualTree = grid };
            var editable = new Trigger { Property = ComboBox.IsEditableProperty, Value = true }; editable.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "PART_EditableTextBox")); editable.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, "Selection")); comboTemplate.Triggers.Add(editable);
            combo.Setters.Add(new Setter(Control.TemplateProperty, comboTemplate)); resources[typeof(ComboBox)] = combo;
            var button = new Style(typeof(Button));
            button.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey)));
            button.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Button")));
            button.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("ButtonText")));
            button.Setters.Add(new Setter(Control.BorderBrushProperty, ColorResource("Border")));
            button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 5, 10, 5)));
            button.Setters.Add(new Setter(Control.MinHeightProperty, 30.0));
            button.Setters.Add(new Setter(Control.TemplateProperty, ButtonTemplate(typeof(Button)))); resources[typeof(Button)] = button;
            var tree = new Style(typeof(TreeView));
            tree.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey)));
            tree.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent)); tree.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0))); resources[typeof(TreeView)] = tree;
            var treeItem = new Style(typeof(TreeViewItem)); treeItem.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey))); treeItem.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Text"))); treeItem.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2, 4, 2, 4))); resources[typeof(TreeViewItem)] = treeItem;
            var tabs = new Style(typeof(TabControl)); tabs.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent)); tabs.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0))); resources[typeof(TabControl)] = tabs;
            var tab = new Style(typeof(TabItem)); tab.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey))); tab.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("ButtonText"))); tab.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Button"))); tab.Setters.Add(new Setter(Control.BorderBrushProperty, ColorResource("Border"))); tab.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 5, 10, 5))); resources[typeof(TabItem)] = tab;
            var check = new Style(typeof(CheckBox)); check.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey))); check.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Text"))); resources[typeof(CheckBox)] = check;
            var menuItem = new Style(typeof(MenuItem)); menuItem.Setters.Add(new Setter(Control.FontFamilyProperty, new DynamicResourceExtension(OperationalFontFamilyKey))); menuItem.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Text"))); menuItem.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Surface"))); resources[typeof(MenuItem)] = menuItem;
            var menu = new Style(typeof(ContextMenu)); menu.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Surface"))); menu.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Text"))); resources[typeof(ContextMenu)] = menu;
            var tooltip = new Style(typeof(ToolTip)); tooltip.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Surface"))); tooltip.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Text"))); tooltip.Setters.Add(new Setter(Control.BorderBrushProperty, ColorResource("Border"))); resources[typeof(ToolTip)] = tooltip;
        }
        private static ControlTemplate ItemTemplate(Type type)
        {
            var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.PaddingProperty, new Thickness(8, 5, 8, 5));
            border.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            var template = new ControlTemplate(type) { VisualTree = border };
            var selected = new Trigger { Property = type == typeof(ComboBoxItem) ? ComboBoxItem.IsHighlightedProperty : ListBoxItem.IsSelectedProperty, Value = true };
            if (type == typeof(ListBoxItem)) { selected.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("Text"))); selected.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("Background"))); }
            else { selected.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("SelectedButton"))); selected.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("SelectedButtonText"))); } template.Triggers.Add(selected);
            return template;
        }
        private static ControlTemplate ButtonTemplate(Type type)
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty)); border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty)); presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)); presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter);
            var template = new ControlTemplate(type) { VisualTree = border };
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Control.BorderBrushProperty, ColorResource("Accent"))); template.Triggers.Add(hover);
            var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true }; pressed.Setters.Add(new Setter(Control.BackgroundProperty, ColorResource("SelectedButton"))); pressed.Setters.Add(new Setter(Control.ForegroundProperty, ColorResource("SelectedButtonText"))); template.Triggers.Add(pressed);
            var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false }; disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.55)); template.Triggers.Add(disabled);
            return template;
        }
        public static Button Button(string label, RoutedEventHandler clicked, bool accent)
        {
            var button = new Button { Content = label, Background = accent ? SelectedButton : ButtonBackground, Foreground = accent ? SelectedButtonText : ButtonText, BorderBrush = Line, Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 7, 0), Cursor = System.Windows.Input.Cursors.Hand, FontSize = 12, MinHeight = 32, HorizontalContentAlignment = HorizontalAlignment.Center, Template = ButtonTemplate(typeof(Button)) };
            button.Click += clicked; return button;
        }
    }
}
