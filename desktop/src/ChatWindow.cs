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
        private readonly CommandMenu commandMenu;
        private readonly Border skillBanner;
        private readonly TextBlock skillText;
        private bool turnBusy, commandBusy;
        private readonly Image portraitImage;
        private readonly TextBlock petTitle;
        private readonly TabControl tabs;
        private readonly TabItem petsTab;
        private readonly PetPicker picker;
        private string petName;
        private bool quitting;
        public event Action<string> Send;
        public Action Stop, Connect, OpenFile, ChooseFolder, NewChat;
        public event Action<string> PetSelected;
        public event Action<string> CommandRequested;
        public Action ClearSkill;

        public ChatWindow(PetAssets assets) : this(assets, new PetCatalog()) { }
        public ChatWindow(PetAssets assets, PetCatalog catalog)
        {
            petName = assets.DisplayName;
            Title = "Negotchi | " + petName; Icon = assets.Frames[0]; Width = 480; Height = 700; MinWidth = 430; MinHeight = 560;
            Background = Theme.Background; Foreground = Theme.Text; FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.Manual; ResizeMode = ResizeMode.CanResizeWithGrip;
            var root = new DockPanel { Margin = new Thickness(20, 16, 20, 16), LastChildFill = true };
            var header = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            portraitImage = new Image { Source = assets.Frames[0], Stretch = Stretch.Uniform };
            var portrait = new Border { Background = Theme.Card, CornerRadius = new CornerRadius(12), Width = 46, Height = 52, Child = portraitImage };
            header.Children.Add(portrait);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            petTitle = new TextBlock { Text = petName, FontSize = 23, FontWeight = FontWeights.SemiBold };
            titles.Children.Add(petTitle);
            titles.Children.Add(new TextBlock { Text = "SEU COMPANHEIRO DE DESKTOP", FontSize = 9, Foreground = Theme.Pink, Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetColumn(titles, 1); header.Children.Add(titles);
            connect = Theme.Button("Conectar conta", delegate { if (Connect != null) Connect(); }, false);
            connect.VerticalAlignment = VerticalAlignment.Center; connect.Margin = new Thickness(0); Grid.SetColumn(connect, 2); header.Children.Add(connect);
            DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
            var conversation = new DockPanel { LastChildFill = true };
            tabs = new TabControl { Background = Theme.Background, BorderThickness = new Thickness(0), Padding = new Thickness(0), Foreground = Theme.Text };
            var tabLayout = new FrameworkElementFactory(typeof(DockPanel));
            tabLayout.SetValue(DockPanel.LastChildFillProperty, true);
            var tabHeaders = new FrameworkElementFactory(typeof(StackPanel));
            tabHeaders.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            tabHeaders.SetValue(Panel.IsItemsHostProperty, true); tabHeaders.SetValue(DockPanel.DockProperty, Dock.Top);
            tabHeaders.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, 12));
            tabLayout.AppendChild(tabHeaders);
            var tabContent = new FrameworkElementFactory(typeof(ContentPresenter));
            tabContent.Name = "PART_SelectedContentHost"; tabContent.SetValue(ContentPresenter.ContentSourceProperty, "SelectedContent");
            tabLayout.AppendChild(tabContent); tabs.Template = new ControlTemplate(typeof(TabControl)) { VisualTree = tabLayout };
            tabs.Items.Add(PetPicker.Tab("Conversa", conversation));
            picker = new PetPicker(catalog); picker.SetActive(assets.Id);
            picker.PetSelected += id => { if (PetSelected != null) PetSelected(id); };
            petsTab = PetPicker.Tab("Pets", picker); tabs.Items.Add(petsTab);
            root.Children.Add(tabs);
            var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var open = Theme.Button("Abrir arquivo", delegate { if (OpenFile != null) OpenFile(); }, false); toolbar.Children.Add(open);
            chooseFolder = Theme.Button("Pasta de trabalho", delegate { if (ChooseFolder != null) ChooseFolder(); }, false); toolbar.Children.Add(chooseFolder);
            newChat = Theme.Button("Nova conversa", delegate { if (NewChat != null) NewChat(); }, false); newChat.Margin = new Thickness(0); toolbar.Children.Add(newChat);
            DockPanel.SetDock(toolbar, Dock.Top); conversation.Children.Add(toolbar);
            folder = new TextBlock { FontSize = 11, Foreground = Theme.Muted, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 13) };
            DockPanel.SetDock(folder, Dock.Top); conversation.Children.Add(folder);
            var bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            status = new TextBlock { Text = "Pronto para conversar", Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; bottom.Children.Add(status);
            input = new TextBox { Height = 82, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Card, Foreground = Theme.Text, CaretBrush = Theme.Pink, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(12), FontSize = 14, ToolTip = "Pergunte ou peca para abrir um arquivo. Enter envia; Shift+Enter quebra a linha." };
            commandMenu = new CommandMenu(input);
            commandMenu.Execute += text => { if (CommandRequested != null) CommandRequested(text); };
            input.PreviewKeyDown += delegate(object sender, KeyEventArgs args) {
                if (commandMenu.HandleKey(args)) { args.Handled = true; return; }
                if (args.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0) { args.Handled = true; Submit(); }
            };
            var skillRow = new DockPanel();
            var removeSkill = Theme.Button("x", delegate { if (ClearSkill != null) ClearSkill(); }, false); removeSkill.Padding = new Thickness(8, 2, 8, 2); removeSkill.MinHeight = 20; removeSkill.ToolTip = "Remover skill deste pedido"; DockPanel.SetDock(removeSkill, Dock.Right); skillRow.Children.Add(removeSkill);
            skillText = new TextBlock { Foreground = Theme.Pink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }; skillRow.Children.Add(skillText);
            skillBanner = new Border { Child = skillRow, Background = Theme.Card, Padding = new Thickness(8, 5, 4, 5), Margin = new Thickness(0, 0, 0, 8), CornerRadius = new CornerRadius(6), Visibility = Visibility.Collapsed }; bottom.Children.Add(skillBanner);
            bottom.Children.Add(input);
            var composerButtons = new DockPanel { Margin = new Thickness(0, 9, 0, 0) };
            attach = Theme.Button("Anexar caminho", delegate { AttachPath(); }, false); DockPanel.SetDock(attach, Dock.Left); composerButtons.Children.Add(attach);
            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            stop = Theme.Button("Parar", delegate { if (Stop != null) Stop(); }, false); stop.IsEnabled = false;
            send = Theme.Button("Enviar", delegate { Submit(); }, true); send.Margin = new Thickness(0);
            rightButtons.Children.Add(stop); rightButtons.Children.Add(send); composerButtons.Children.Add(rightButtons);
            bottom.Children.Add(composerButtons);
            bottom.Children.Add(new TextBlock { Text = "Enter envia  |  Shift+Enter quebra a linha  |  / comandos", Foreground = Theme.Muted, FontSize = 10, Margin = new Thickness(0, 9, 0, 0) });
            DockPanel.SetDock(bottom, Dock.Bottom); conversation.Children.Add(bottom);
            messages = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            scroll = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            conversation.Children.Add(scroll); Content = root;
            Closing += delegate(object sender, System.ComponentModel.CancelEventArgs args) { if (!quitting) { args.Cancel = true; Hide(); } };
        }

        public void SetFolder(string path) { folder.Text = path; folder.ToolTip = path; }
        public void ShowPets() { tabs.SelectedItem = petsTab; }
        public void ShowConversation() { tabs.SelectedIndex = 0; }
        public string ActivePetId { get { return picker.ActivePetId; } }
        public void SetPet(PetAssets assets)
        {
            petName = assets.DisplayName; petTitle.Text = petName; Title = "Negotchi | " + petName;
            portraitImage.Source = assets.Frames[0]; Icon = assets.Frames[0]; picker.SetActive(assets.Id);
        }
        public void SetStatus(string text) { status.Text = text; }
        public void SetConnected(bool value) { connect.Content = value ? "Conta conectada" : "Conectar conta"; }
        public void SetBusy(bool value)
        {
            turnBusy = value; ApplyBusy();
        }
        public void SetCommandBusy(bool value) { commandBusy = value; ApplyBusy(); }
        private void ApplyBusy()
        {
            bool active = turnBusy || commandBusy;
            send.IsEnabled = !active; stop.IsEnabled = turnBusy; connect.IsEnabled = !active;
            chooseFolder.IsEnabled = !active; newChat.IsEnabled = !active;
            picker.IsEnabled = !active; picker.ToolTip = active ? "Termine ou pare o pedido antes de trocar de pet." : null;
            skillBanner.IsEnabled = !active;
            commandMenu.SetEnabled(!active);
        }
        public void SetSkill(string name) { skillBanner.Visibility = string.IsNullOrEmpty(name) ? Visibility.Collapsed : Visibility.Visible; skillText.Text = "Skill: " + name; }
        public void PrefixInput(string prefix) { if (!input.Text.StartsWith(prefix, StringComparison.Ordinal)) input.Text = prefix + input.Text; input.Focus(); input.CaretIndex = input.Text.Length; }
        public void RemoveSkillPrefix(string name) { string prefix = "$" + name + " "; if (input.Text.StartsWith(prefix, StringComparison.Ordinal)) input.Text = input.Text.Substring(prefix.Length); }
        public void SetConnecting(bool value) { connect.IsEnabled = !value; }
        public void CloseForExit() { quitting = true; Close(); }
        public void ClearMessages() { messages.Children.Clear(); }

        private void Submit()
        {
            string text = input.Text.Trim();
            if (text.Length == 0 || !send.IsEnabled) return;
            SlashInvocation invocation;
            if (SlashCommands.TryParse(text, out invocation)) { if (CommandRequested != null) { input.Clear(); CommandRequested(text); } return; }
            if (Send != null) { input.Clear(); Send(text); }
        }

        public TextBox AddMessage(string role, string text, string speaker = null)
        {
            bool user = role == "user", system = role == "system";
            var contents = new StackPanel();
            contents.Children.Add(new TextBlock { Text = user ? "VOCE" : system ? "NEGOTCHI" : (speaker ?? petName).ToUpperInvariant(), Foreground = user ? Theme.Background : Theme.Pink, FontWeight = FontWeights.SemiBold, FontSize = 9, Margin = new Thickness(0, 0, 0, 6) });
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
