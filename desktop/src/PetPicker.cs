using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Negotchi
{
    internal sealed class PetPicker : DockPanel
    {
        private readonly Dictionary<string, Button> cards = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextBlock> captions = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
        public event Action<string> PetSelected;
        public string ActivePetId { get; private set; }

        public PetPicker(PetCatalog catalog)
        {
            Reload(catalog);
        }

        public void Reload(PetCatalog catalog)
        {
            Children.Clear(); cards.Clear(); captions.Clear();
            LastChildFill = true;
            var description = new TextBlock { Text = "Escolha seu companheiro", Foreground = Theme.Text, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 5) };
            SetDock(description, Dock.Top); Children.Add(description);
            var hint = new TextBlock { Text = catalog.Pets.Length + " pets disponiveis. Clique em um deles para trocar.", Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
            SetDock(hint, Dock.Top); Children.Add(hint);
            var actions = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            actions.Children.Add(Theme.Button("Criar pet", delegate { if (CommandRequested != null) CommandRequested("/create-pet"); }, true));
            actions.Children.Add(Theme.Button("Importar pet", delegate { if (CommandRequested != null) CommandRequested("/import-pet"); }, false));
            actions.Children.Add(Theme.Button("Atualizar", delegate { if (CommandRequested != null) CommandRequested("/pets"); }, false));
            SetDock(actions, Dock.Top); Children.Add(actions);
            var footer = new TextBlock { Text = "Sua escolha fica salva. A conversa continua ao trocar de pet.", Foreground = Theme.Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
            SetDock(footer, Dock.Bottom); Children.Add(footer);
            var grid = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var definition in catalog.Pets)
            {
                var pet = definition;
                var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                var image = new Image { Source = pet.Preview, Width = 78, Height = 85, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 5) };
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
                content.Children.Add(image);
                content.Children.Add(new TextBlock { Text = pet.DisplayName, FontSize = 13, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
                var caption = new TextBlock { Text = "Usar pet", FontSize = 10, Foreground = Theme.Muted, Margin = new Thickness(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
                content.Children.Add(caption);
                var button = Theme.Button(pet.DisplayName, delegate { if (IsEnabled && PetSelected != null) PetSelected(pet.Id); }, false);
                button.Content = content; button.Width = 122; button.MinHeight = 144; button.Padding = new Thickness(6); button.Margin = new Thickness(0, 0, 8, 8); button.ToolTip = pet.Description;
                System.Windows.Automation.AutomationProperties.SetName(button, "Usar " + pet.DisplayName);
                cards.Add(pet.Id, button); captions.Add(pet.Id, caption); grid.Children.Add(button);
            }
            Children.Add(new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        }
        public event Action<string> CommandRequested;

        public void SetActive(string id)
        {
            if (!cards.ContainsKey(id)) throw new ArgumentException("Pet nao encontrado: " + id);
            ActivePetId = id;
            foreach (var item in cards)
            {
                bool selected = string.Equals(item.Key, id, StringComparison.OrdinalIgnoreCase);
                item.Value.Background = selected ? Theme.SelectedButton : Theme.ButtonBackground;
                item.Value.Foreground = selected ? Theme.SelectedButtonText : Theme.ButtonText;
                item.Value.BorderBrush = selected ? Theme.Accent : Theme.Line;
                captions[item.Key].Text = selected ? "Em uso" : "Usar pet";
                captions[item.Key].Foreground = selected ? Theme.SelectedButtonText : Theme.ButtonText;
            }
        }

        public static TabItem Tab(string title, object content)
        {
            var tab = new TabItem { Header = title, Content = content, Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 8, 0), BorderBrush = Theme.Line };
            var style = new Style(typeof(TabItem));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Theme.ColorResource("Button"))); style.Setters.Add(new Setter(Control.ForegroundProperty, Theme.ColorResource("ButtonText")));
            tab.Style = style;
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            presenter.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.AppendChild(presenter);
            var template = new ControlTemplate(typeof(TabItem)) { VisualTree = border };
            var selected = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
            selected.Setters.Add(new Setter(Control.BackgroundProperty, Theme.ColorResource("SelectedButton")));
            selected.Setters.Add(new Setter(Control.ForegroundProperty, Theme.ColorResource("SelectedButtonText")));
            template.Triggers.Add(selected); tab.Template = template;
            return tab;
        }
    }
}
