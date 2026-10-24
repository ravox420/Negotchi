using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;

namespace Negotchi
{
    internal static partial class Tests
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

        private static void TestScrollStyles()
        {
            var scene = new Grid { Background = Theme.Background, Margin = new Thickness(12) };
            scene.RowDefinitions.Add(new RowDefinition()); scene.RowDefinitions.Add(new RowDefinition());
            var viewer = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 10) };
            viewer.Content = new Border { Width = 1100, Height = 600, Background = Theme.Card, Child = new TextBlock { Text = "Conversa e painéis · barras discretas, arraste e rolagem", Foreground = Theme.Text, Margin = new Thickness(14), FontFamily = Theme.FontWithFallback("Cascadia Mono, Consolas") } }; scene.Children.Add(viewer);
            var text = new TextBox { Text = string.Join("\n", Enumerable.Repeat("Campo de texto — ação, revisão e café — " + new string('x', 90), 25)), AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(text, 1); scene.Children.Add(text);
            Render(scene, 580, 300, Path.Combine(output, "scrollbars-preview.png"));
            var bars = Descendants<ScrollBar>(scene).Where(bar => bar.Visibility == Visibility.Visible && bar.ActualWidth > 0 && bar.ActualHeight > 0).ToArray();
            Check(bars.Any(bar => bar.Orientation == Orientation.Vertical) && bars.Any(bar => bar.Orientation == Orientation.Horizontal), "aplica barras tematicas aos dois eixos e aos campos de texto");
            Check(bars.All(bar => { var track = bar.Template.FindName("PART_Track", bar) as Track; return track != null && track.Thumb != null && ReferenceEquals(track.Thumb.Style, Application.Current.Resources["Negotchi.ScrollThumb"]); }), "preserva Track e Thumb nativos com estilo discreto compartilhado");
            viewer.ScrollToVerticalOffset(150); viewer.ScrollToHorizontalOffset(80); scene.UpdateLayout();
            Check(Math.Abs(viewer.VerticalOffset - 150) < 1 && Math.Abs(viewer.HorizontalOffset - 80) < 1, "barras personalizadas preservam os deslocamentos de rolagem");
        }
        private static void TestWindowChrome(Window window, bool dark)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle; int value;
            int result = DwmGetWindowAttribute(handle, 20, out value, sizeof(int));
            if (result < 0) result = DwmGetWindowAttribute(handle, 19, out value, sizeof(int));
            if (result >= 0) Check(value == (dark ? 1 : 0), "barra de titulo nativa acompanha o tema " + (dark ? "escuro" : "claro"));
            else Console.WriteLine("SKIP: atributo DWM de tema indisponivel neste Windows.");
            result = DwmGetWindowAttribute(handle, 35, out value, sizeof(int));
            if (result >= 0)
            {
                var color = ((SolidColorBrush)Theme.Background).Color;
                Check(value == (color.R | (color.G << 8) | (color.B << 16)), "cor da barra superior acompanha o fundo da aplicacao");
            }
        }
    }
}
