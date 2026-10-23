using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Negotchi
{
    internal sealed class ChatWindow : Window
    {
        private readonly StackPanel messages;
        private readonly ScrollViewer scroll;
        private readonly TextBox input;
        private readonly TextBlock status, folder;
        private readonly Button send, stop, connect, chooseFolder, newChat, attach;
        private bool quitting;
        public event Action<string> Send;
        public Action Stop, Connect, OpenFile, ChooseFolder, NewChat;

        public ChatWindow(PetAssets assets)
        {
            Title = "Negotchi | PimpDogg"; Icon = assets.Frames[0]; Width = 480; Height = 660; MinWidth = 430; MinHeight = 520;
            Background = Theme.Background; Foreground = Theme.Text; FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.Manual; ResizeMode = ResizeMode.CanResizeWithGrip;
            var root = new DockPanel { Margin = new Thickness(20, 16, 20, 16), LastChildFill = true };
            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var portrait = new Border { Background = Theme.Card, CornerRadius = new CornerRadius(12), Width = 46, Height = 52, Child = new Image { Source = assets.Frames[0], Stretch = Stretch.Uniform } };
            header.Children.Add(portrait);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(new TextBlock { Text = "PimpDogg", FontSize = 23, FontWeight = FontWeights.SemiBold });
            titles.Children.Add(new TextBlock { Text = "SEU COMPANHEIRO DE DESKTOP", FontSize = 9, Foreground = Theme.Pink, Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetColumn(titles, 1); header.Children.Add(titles);
            connect = Theme.Button("Conectar conta", delegate { if (Connect != null) Connect(); }, false);
            connect.VerticalAlignment = VerticalAlignment.Center; connect.Margin = new Thickness(0); Grid.SetColumn(connect, 2); header.Children.Add(connect);
            DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var open = Theme.Button("Abrir arquivo", delegate { if (OpenFile != null) OpenFile(); }, false); toolbar.Children.Add(open);
            chooseFolder = Theme.Button("Pasta de trabalho", delegate { if (ChooseFolder != null) ChooseFolder(); }, false); toolbar.Children.Add(chooseFolder);
            newChat = Theme.Button("Nova conversa", delegate { if (NewChat != null) NewChat(); }, false); newChat.Margin = new Thickness(0); toolbar.Children.Add(newChat);
            DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
            folder = new TextBlock { FontSize = 11, Foreground = Theme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 13) };
            DockPanel.SetDock(folder, Dock.Top); root.Children.Add(folder);
            var bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            status = new TextBlock { Text = "Pronto para conversar", Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; bottom.Children.Add(status);
            input = new TextBox { Height = 82, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, CaretBrush = Theme.Pink, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(12), FontSize = 14, ToolTip = "Pergunte ou peca para abrir um arquivo. Enter envia; Shift+Enter quebra a linha." };
            input.PreviewKeyDown += delegate(object sender, KeyEventArgs args) { if (args.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { args.Handled = true; Submit(); } };
            bottom.Children.Add(input);
            var composerButtons = new DockPanel { Margin = new Thickness(0, 9, 0, 0) };
            attach = Theme.Button("Anexar caminho", delegate { AttachPath(); }, false); DockPanel.SetDock(attach, Dock.Left); composerButtons.Children.Add(attach);
            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            stop = Theme.Button("Parar", delegate { if (Stop != null) Stop(); }, false); stop.IsEnabled = false;
            send = Theme.Button("Enviar", delegate { Submit(); }, true); send.Margin = new Thickness(0);
            rightButtons.Children.Add(stop); rightButtons.Children.Add(send); composerButtons.Children.Add(rightButtons);
            bottom.Children.Add(composerButtons);
            bottom.Children.Add(new TextBlock { Text = "Enter envia  /  Shift+Enter quebra a linha", Foreground = Theme.Muted, FontSize = 10, Margin = new Thickness(0, 9, 0, 0) });
            DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
            messages = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            scroll = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            root.Children.Add(scroll); Content = root;
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs args) { if (!quitting) { args.Cancel = true; Hide(); } };
        }

        public void SetFolder(string path) { folder.Text = path; folder.ToolTip = path; }
        public void SetStatus(string text) { status.Text = text; }
        public void SetConnected(bool value) { connect.Content = value ? "Conta conectada" : "Conectar conta"; }
        public void SetBusy(bool value)
        {
            send.IsEnabled = !value; stop.IsEnabled = value; connect.IsEnabled = !value;
            chooseFolder.IsEnabled = !value; newChat.IsEnabled = !value;
        }
        public void SetConnecting(bool value) { connect.IsEnabled = !value; }
        public void CloseForExit() { quitting = true; Close(); }
        public void ClearMessages() { messages.Children.Clear(); }

        private void Submit()
        {
            string text = input.Text.Trim();
            if (text.Length == 0 || !send.IsEnabled || Send == null) return;
            input.Clear(); Send(text);
        }

        public TextBox AddMessage(string role, string text)
        {
            bool user = role == "user", system = role == "system";
            var contents = new StackPanel();
            contents.Children.Add(new TextBlock { Text = user ? "VOCE" : system ? "NEGOTCHI" : "PIMPDOGG", Foreground = user ? Theme.Background : Theme.Pink, FontWeight = FontWeights.SemiBold, FontSize = 9, Margin = new Thickness(0, 0, 0, 6) });
            var body = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = user ? Theme.Background : Theme.Text, FontSize = 13, Padding = new Thickness(0), IsReadOnlyCaretVisible = false };
            contents.Children.Add(body);
            var card = new Border { Background = user ? Theme.Pink : Theme.Card, CornerRadius = new CornerRadius(12), Padding = new Thickness(13), Margin = user ? new Thickness(35, 0, 0, 11) : new Thickness(0, 0, 18, 11), Child = contents };
            messages.Children.Add(card); ScrollToLatest(); return body;
        }

        public void ScrollToLatest()
        {
            // Preserve the reader's place when they scroll back through an answer.
            if (scroll.ScrollableHeight - scroll.VerticalOffset < 90) Dispatcher.BeginInvoke(new Action(() => scroll.ScrollToEnd()));
        }

        private void AttachPath()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Escolha um arquivo para mencionar na conversa", CheckFileExists = true, Multiselect = true };
            if (dialog.ShowDialog(this) != true) return;
            foreach (string path in dialog.FileNames) input.AppendText((input.Text.Length > 0 ? "\n" : "") + "Arquivo: \"" + path + "\"");
            input.Focus(); input.CaretIndex = input.Text.Length;
        }
    }
}
