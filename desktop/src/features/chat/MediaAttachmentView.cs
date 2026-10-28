using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Negotchi
{
    // Local-only renderer for media that the user attached to a message.
    internal static class MediaAttachmentView
    {
        internal static FrameworkElement Create(PendingAttachment attachment, double width)
        {
            if (attachment.Kind == "video") return Video(attachment, width);
            if (attachment.Kind == "gif") return Gif(attachment, width);
            return Still(attachment, width);
        }

        private static FrameworkElement Still(PendingAttachment attachment, double width)
        {
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1200; image.UriSource = new Uri(attachment.Path); image.EndInit(); image.Freeze();
            return Frame(new Image { Source = image, MaxWidth = width, MaxHeight = 320, Stretch = Stretch.Uniform }, attachment.Name);
        }

        private static FrameworkElement Gif(PendingAttachment attachment, double width)
        {
            var decoder = new GifBitmapDecoder(new Uri(attachment.Path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            BitmapFrame[] frames = decoder.Frames.ToArray(); if (frames.Length == 0) throw new InvalidDataException("GIF sem quadros.");
            var image = new Image { Source = frames[0], MaxWidth = width, MaxHeight = 320, Stretch = Stretch.Uniform };
            if (frames.Length > 1)
            {
                int index = 0;
                var timer = new DispatcherTimer();
                timer.Interval = Delay(frames[0]);
                timer.Tick += delegate { index = (index + 1) % frames.Length; image.Source = frames[index]; timer.Interval = Delay(frames[index]); };
                image.Loaded += delegate { timer.Start(); }; image.Unloaded += delegate { timer.Stop(); };
            }
            return Frame(image, attachment.Name + " · GIF local");
        }

        private static FrameworkElement Video(PendingAttachment attachment, double width)
        {
            var player = new MediaElement { Source = new Uri(attachment.Path), LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform, Width = width, Height = Math.Min(width * 9 / 16, 260) };
            var play = Theme.Button("Reproduzir", delegate { player.Play(); }, false);
            var pause = Theme.Button("Pausar", delegate { player.Pause(); }, false);
            var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) }; controls.Children.Add(play); controls.Children.Add(pause);
            var content = new StackPanel(); content.Children.Add(player); content.Children.Add(controls);
            content.Unloaded += delegate { player.Stop(); };
            player.MediaFailed += delegate(object sender, ExceptionRoutedEventArgs args) { play.IsEnabled = pause.IsEnabled = false; controls.Children.Add(new TextBlock { Text = "Não foi possível reproduzir este vídeo com os codecs do Windows.", Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 0, 0, 0) }); };
            return Frame(content, attachment.Name + " · vídeo local");
        }

        private static TimeSpan Delay(BitmapFrame frame)
        {
            try { var reader = frame.Metadata as BitmapMetadata; object value = reader == null ? null : reader.GetQuery("/grctlext/Delay"); ushort delay = value is ushort ? (ushort)value : (ushort)10; return TimeSpan.FromMilliseconds(Math.Max(20, delay * 10)); }
            catch (NotSupportedException) { return TimeSpan.FromMilliseconds(100); }
        }

        private static FrameworkElement Frame(FrameworkElement content, string label)
        {
            var panel = new StackPanel(); panel.Children.Add(content); panel.Children.Add(new TextBlock { Text = label, Foreground = Theme.Muted, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 5, 0, 0), ToolTip = label });
            return new Border { Child = panel, Background = Theme.Card, CornerRadius = new CornerRadius(7), Padding = new Thickness(7), Margin = new Thickness(0, 8, 0, 0) };
        }
    }
}
