using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Negotchi
{
    internal sealed class ColorWheel : FrameworkElement
    {
        private static readonly BitmapSource wheel = CreateWheel();
        private double hue, saturation, brightness = 1;
        public event Action<Color> ColorChanged;
        public ColorWheel() { Width = 230; Height = 230; Focusable = true; Cursor = Cursors.Cross; ToolTip = "Arraste para escolher matiz e saturacao. Use as setas para ajustar."; }
        public Color SelectedColor
        {
            get { return FromHsv(hue, saturation, brightness); }
            set
            {
                double red = value.R / 255.0, green = value.G / 255.0, blue = value.B / 255.0;
                double max = Math.Max(red, Math.Max(green, blue)), min = Math.Min(red, Math.Min(green, blue)), delta = max - min;
                brightness = max; saturation = max == 0 ? 0 : delta / max;
                hue = delta == 0 ? 0 : max == red ? 60 * ((green - blue) / delta) : max == green ? 60 * ((blue - red) / delta + 2) : 60 * ((red - green) / delta + 4);
                if (hue < 0) hue += 360;
                Changed();
            }
        }
        public double Brightness { get { return brightness; } set { brightness = Math.Max(0, Math.Min(1, value)); Changed(); } }
        private void Changed() { InvalidateVisual(); if (ColorChanged != null) ColorChanged(SelectedColor); }
        internal static Color FromHsv(double hue, double saturation, double value)
        {
            double h = ((hue % 360) + 360) % 360 / 60, chroma = value * saturation, x = chroma * (1 - Math.Abs(h % 2 - 1)), m = value - chroma;
            double red = 0, green = 0, blue = 0;
            if (h < 1) { red = chroma; green = x; } else if (h < 2) { red = x; green = chroma; } else if (h < 3) { green = chroma; blue = x; } else if (h < 4) { green = x; blue = chroma; } else if (h < 5) { red = x; blue = chroma; } else { red = chroma; blue = x; }
            return Color.FromRgb((byte)Math.Round((red + m) * 255), (byte)Math.Round((green + m) * 255), (byte)Math.Round((blue + m) * 255));
        }
        private static BitmapSource CreateWheel()
        {
            const int size = 256; var pixels = new byte[size * size * 4];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                double dx = (x + 0.5 - size / 2.0) / (size / 2.0), dy = (size / 2.0 - y - 0.5) / (size / 2.0), radius = Math.Sqrt(dx * dx + dy * dy);
                if (radius > 1) continue;
                var color = FromHsv(Math.Atan2(dy, dx) * 180 / Math.PI, radius, 1); int offset = (y * size + x) * 4;
                pixels[offset] = color.B; pixels[offset + 1] = color.G; pixels[offset + 2] = color.R; pixels[offset + 3] = 255;
            }
            var result = BitmapSource.Create(size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4); result.Freeze(); return result;
        }
        protected override void OnRender(DrawingContext context)
        {
            base.OnRender(context);
            double size = Math.Min(ActualWidth, ActualHeight) - 12, left = (ActualWidth - size) / 2, top = (ActualHeight - size) / 2, radius = size / 2;
            if (size <= 0) return;
            context.DrawImage(wheel, new Rect(left, top, size, size));
            context.PushOpacity(1 - brightness); context.DrawEllipse(Brushes.Black, null, new Point(left + radius, top + radius), radius, radius); context.Pop();
            double angle = hue * Math.PI / 180;
            var marker = new Point(left + radius + Math.Cos(angle) * saturation * radius, top + radius - Math.Sin(angle) * saturation * radius);
            context.DrawEllipse(null, new Pen(Brushes.Black, 4), marker, 6, 6); context.DrawEllipse(null, new Pen(Brushes.White, 2), marker, 6, 6);
        }
        private void Select(Point point)
        {
            double radius = (Math.Min(ActualWidth, ActualHeight) - 12) / 2, dx = point.X - ActualWidth / 2, dy = ActualHeight / 2 - point.Y;
            if (radius <= 0) return;
            hue = (Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360; saturation = Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / radius); Changed();
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); Focus(); CaptureMouse(); Select(e.GetPosition(this)); e.Handled = true; }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) Select(e.GetPosition(this)); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); if (IsMouseCaptured) ReleaseMouseCapture(); e.Handled = true; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Left || e.Key == Key.Right) hue = (hue + (e.Key == Key.Left ? -1 : 1) + 360) % 360;
            else if (e.Key == Key.Up || e.Key == Key.Down) saturation = Math.Max(0, Math.Min(1, saturation + (e.Key == Key.Up ? 0.01 : -0.01)));
            else return;
            Changed(); e.Handled = true;
        }
    }

    internal sealed class ColorPickerWindow : Window
    {
        private readonly ColorWheel wheel;
        private readonly Slider brightness;
        private readonly TextBox hex;
        private readonly Border swatch;
        private readonly TextBlock error;
        private bool updating;
        public string SelectedColor { get; private set; }
        public ColorPickerWindow(Window owner, string label, string initial)
        {
            Title = "Cor: " + label; Width = 390; Height = 520; MinWidth = 350; MinHeight = 430; Owner = owner; ShowInTaskbar = false; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Theme.Background; Foreground = Theme.Text;
            var root = new DockPanel { Margin = new Thickness(20), LastChildFill = true }; Content = root;
            var heading = new TextBlock { Text = label, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
            buttons.Children.Add(Theme.Button("Cancelar", delegate { Close(); }, false));
            buttons.Children.Add(Theme.Button("Usar cor", delegate {
                if (!Theme.IsColor(hex.Text.Trim())) { error.Text = "Informe uma cor no formato #RRGGBB."; return; }
                SelectedColor = hex.Text.Trim().ToUpperInvariant(); DialogResult = true;
            }, true));
            var body = new StackPanel();
            wheel = new ColorWheel { HorizontalAlignment = HorizontalAlignment.Center, SelectedColor = (Color)ColorConverter.ConvertFromString(initial) }; body.Children.Add(wheel);
            body.Children.Add(new TextBlock { Text = "Luminosidade", Margin = new Thickness(0, 12, 0, 5) });
            brightness = new Slider { Minimum = 0, Maximum = 1, Value = wheel.Brightness, SmallChange = 0.01, LargeChange = 0.1 }; body.Children.Add(brightness);
            var row = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
            swatch = new Border { Width = 42, Height = 34, CornerRadius = new CornerRadius(6), BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0) }; DockPanel.SetDock(swatch, Dock.Left); row.Children.Add(swatch);
            hex = new TextBox { Text = initial, Background = Theme.Card, Foreground = Theme.Text, BorderBrush = Theme.Line, Padding = new Thickness(8), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Cor hexadecimal #RRGGBB" }; row.Children.Add(hex); body.Children.Add(row);
            error = new TextBlock { Foreground = Theme.Accent, TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 5, 0, 0) }; body.Children.Add(error);
            root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            wheel.ColorChanged += UpdateColor;
            brightness.ValueChanged += delegate { if (!updating) wheel.Brightness = brightness.Value; };
            hex.TextChanged += delegate { if (!updating && Theme.IsColor(hex.Text.Trim())) wheel.SelectedColor = (Color)ColorConverter.ConvertFromString(hex.Text.Trim()); };
            UpdateColor(wheel.SelectedColor); Theme.BindTypography(this);
        }
        private void UpdateColor(Color color)
        {
            updating = true;
            hex.Text = "#" + color.R.ToString("X2", CultureInfo.InvariantCulture) + color.G.ToString("X2", CultureInfo.InvariantCulture) + color.B.ToString("X2", CultureInfo.InvariantCulture);
            brightness.Value = wheel.Brightness; swatch.Background = new SolidColorBrush(color); error.Text = ""; updating = false;
        }
    }
}
