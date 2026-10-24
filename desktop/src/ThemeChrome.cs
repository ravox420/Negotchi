using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace Negotchi
{
    internal static partial class Theme
    {
        private static bool windowChromeInstalled;
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        private static void InstallWindowChrome()
        {
            if (windowChromeInstalled) return;
            windowChromeInstalled = true;
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(delegate(object sender, RoutedEventArgs args) {
                if (ReferenceEquals(sender, args.OriginalSource)) ApplyWindowChrome((Window)sender);
            }));
        }
        internal static void ApplyWindowChrome(Window window)
        {
            if (window == null || window.AllowsTransparency || window.WindowStyle == WindowStyle.None || SystemParameters.HighContrast) return;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int dark = current.Mode == "light" ? 0 : 1;
            try
            {
                if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) < 0) DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
                // Caption colors are supported on Windows 11; older systems keep their native dark caption.
                int caption = ColorRef(palette["Background"].Value), text = ColorRef(palette["Text"].Value), border = ColorRef(palette["Border"].Value);
                DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
                DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
                DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        private static int ColorRef(Color color) { return color.R | (color.G << 8) | (color.B << 16); }

        private static void InstallScrollStyles(ResourceDictionary resources)
        {
            // Static XAML only. Track, Thumb and routed page commands retain WPF scrolling behavior.
            const string xaml = @"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='Negotchi.ScrollThumb' TargetType='{x:Type Thumb}'>
    <Setter Property='MinWidth' Value='8'/><Setter Property='MinHeight' Value='8'/>
    <Setter Property='Background' Value='{DynamicResource Negotchi.Color.MutedText}'/>
    <Setter Property='Opacity' Value='0.45'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Thumb}'>
      <Border Background='{TemplateBinding Background}' CornerRadius='3' Margin='3'/>
    </ControlTemplate></Setter.Value></Setter>
    <Style.Triggers>
      <Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger>
      <Trigger Property='IsDragging' Value='True'><Setter Property='Opacity' Value='1'/></Trigger>
      <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.2'/></Trigger>
    </Style.Triggers>
  </Style>
  <Style TargetType='{x:Type ScrollBar}'>
    <Setter Property='OverridesDefaultStyle' Value='True'/>
    <Setter Property='Focusable' Value='False'/>
    <Setter Property='IsTabStop' Value='False'/>
    <Setter Property='Width' Value='12'/><Setter Property='Height' Value='Auto'/>
    <Setter Property='Background' Value='{DynamicResource Negotchi.Color.Surface}'/>
    <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
      <Grid Background='{TemplateBinding Background}'>
        <Track x:Name='PART_Track' Orientation='{TemplateBinding Orientation}' IsDirectionReversed='True'
               Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}'
               Value='{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'
               ViewportSize='{TemplateBinding ViewportSize}'>
          <Track.DecreaseRepeatButton><RepeatButton x:Name='Decrease' Command='{x:Static ScrollBar.PageUpCommand}' Focusable='False' IsTabStop='False'>
            <RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template>
          </RepeatButton></Track.DecreaseRepeatButton>
          <Track.Thumb><Thumb Style='{StaticResource Negotchi.ScrollThumb}'/></Track.Thumb>
          <Track.IncreaseRepeatButton><RepeatButton x:Name='Increase' Command='{x:Static ScrollBar.PageDownCommand}' Focusable='False' IsTabStop='False'>
            <RepeatButton.Template><ControlTemplate TargetType='{x:Type RepeatButton}'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template>
          </RepeatButton></Track.IncreaseRepeatButton>
        </Track>
      </Grid>
      <ControlTemplate.Triggers><Trigger Property='Orientation' Value='Horizontal'>
        <Setter TargetName='PART_Track' Property='IsDirectionReversed' Value='False'/>
        <Setter TargetName='Decrease' Property='Command' Value='{x:Static ScrollBar.PageLeftCommand}'/>
        <Setter TargetName='Increase' Property='Command' Value='{x:Static ScrollBar.PageRightCommand}'/>
      </Trigger></ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
    <Style.Triggers><Trigger Property='Orientation' Value='Horizontal'>
      <Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='12'/>
    </Trigger></Style.Triggers>
  </Style>
</ResourceDictionary>";
            var styles = (ResourceDictionary)XamlReader.Parse(xaml);
            resources["Negotchi.ScrollThumb"] = styles["Negotchi.ScrollThumb"];
            resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = styles[typeof(System.Windows.Controls.Primitives.ScrollBar)];
        }
    }
}
