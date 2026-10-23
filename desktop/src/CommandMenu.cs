using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Negotchi
{
    internal sealed class CommandMenu
    {
        private readonly TextBox input;
        private readonly Popup popup;
        private readonly ListBox list;
        private bool manual, enabled = true;
        public event Action<string> Execute;
        public CommandMenu(TextBox composer)
        {
            input = composer;
            list = new ListBox { Background = Theme.Card, Foreground = Theme.Text, BorderThickness = new Thickness(0), MaxHeight = 220, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var content = new DockPanel();
            var heading = new TextBlock { Text = "COMANDOS DO CODEX", Foreground = Theme.Pink, FontSize = 10, Margin = new Thickness(8, 4, 8, 10) }; DockPanel.SetDock(heading, Dock.Top); content.Children.Add(heading);
            var hint = new TextBlock { Text = "Enter executa  /  Tab completa  /  Esc fecha", Foreground = Theme.Muted, FontSize = 10, Margin = new Thickness(8, 10, 8, 4) }; DockPanel.SetDock(hint, Dock.Bottom); content.Children.Add(hint);
            content.Children.Add(list);
            popup = new Popup { PlacementTarget = input, Placement = PlacementMode.Top, StaysOpen = false, AllowsTransparency = true, Child = new Border { Background = Theme.Card, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Child = content } };
            input.TextChanged += delegate {
                if (!enabled) return;
                string text = input.Text.TrimStart();
                if (text.StartsWith("/", StringComparison.Ordinal) && text.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0) { manual = false; Fill(text); popup.IsOpen = input.IsKeyboardFocused && list.Items.Count > 0; }
                else popup.IsOpen = false;
            };
            list.PreviewMouseLeftButtonUp += delegate { Invoke(); };
            list.PreviewKeyDown += delegate(object sender, KeyEventArgs args) { if (args.Key == Key.Enter) { args.Handled = true; Invoke(); } else if (args.Key == Key.Escape) { popup.IsOpen = false; input.Focus(); args.Handled = true; } };
        }
        public void Open()
        {
            if (!enabled) return;
            input.Focus(); manual = true; Fill("/"); popup.IsOpen = true;
        }
        public void SetEnabled(bool value) { enabled = value; if (!value) popup.IsOpen = false; }
        public bool HandleKey(KeyEventArgs args)
        {
            if (!popup.IsOpen) return false;
            if (args.Key == Key.Escape) { popup.IsOpen = false; return true; }
            if (args.Key == Key.Down || args.Key == Key.Up)
            {
                list.SelectedIndex = Math.Max(0, Math.Min(list.Items.Count - 1, list.SelectedIndex + (args.Key == Key.Down ? 1 : -1)));
                list.ScrollIntoView(list.SelectedItem); return true;
            }
            if (args.Key == Key.Tab && !manual)
            {
                var command = Selected(); if (command != null) { input.Text = command.Name + " "; input.CaretIndex = input.Text.Length; }
                popup.IsOpen = false; return true;
            }
            if (args.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { Invoke(); return true; }
            return false;
        }
        private SlashCommand Selected() { var item = list.SelectedItem as ListBoxItem; return item == null ? null : item.Tag as SlashCommand; }
        private void Invoke()
        {
            var command = Selected(); if (command == null || !enabled) return;
            bool fromDraft = !manual; popup.IsOpen = false;
            if (fromDraft) input.Clear();
            if (Execute != null) Execute(command.Name);
        }
        private void Fill(string prefix)
        {
            list.Items.Clear(); popup.Width = Math.Max(320, input.ActualWidth);
            foreach (var command in SlashCommands.Match(prefix))
            {
                var row = new StackPanel { Margin = new Thickness(6, 5, 6, 5) };
                row.Children.Add(new TextBlock { Text = command.Name, Foreground = Theme.Text, FontSize = 13, FontWeight = FontWeights.SemiBold });
                row.Children.Add(new TextBlock { Text = command.Description, Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                list.Items.Add(new ListBoxItem { Content = row, Tag = command });
            }
            if (list.Items.Count > 0) list.SelectedIndex = 0;
        }
    }
}
