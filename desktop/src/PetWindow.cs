using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Negotchi
{
    internal sealed class PetWindow : Window
    {
        private PetAssets assets;
        private readonly AppState state;
        private readonly Action talk, save;
        private readonly Image image;
        private readonly Border bubble;
        private readonly TextBlock bubbleText;
        private readonly DispatcherTimer clock;
        private readonly Stopwatch animationClock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private string animation = "idle";
        private DateTime nextIdle = DateTime.UtcNow.AddSeconds(20), bubbleUntil = DateTime.UtcNow.AddSeconds(8);
        private bool busy, pressing, dragged;
        private Point pressPoint, originalPosition;
        public Action OpenFile, ChooseFolder, ChoosePet, Quit;

        public PetWindow(PetAssets petAssets, AppState appState, Action openChat, Action persist)
        {
            assets = petAssets; state = appState; talk = openChat; save = persist;
            Title = "Negotchi - " + assets.DisplayName; Icon = assets.Frames[0]; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = state.Topmost;
            Width = assets.FrameWidth * state.Scale; Height = assets.FrameHeight * state.Scale + 44;
            var layout = new DockPanel { Background = Brushes.Transparent, LastChildFill = true };
            bubbleText = new TextBlock { Text = "Bora conversar?", Foreground = Theme.Background, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center };
            bubble = new Border { Child = bubbleText, Background = Theme.Pink, CornerRadius = new CornerRadius(12), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(2, 2, 2, 6), Height = 32, VerticalAlignment = VerticalAlignment.Top, Cursor = Cursors.Hand };
            DockPanel.SetDock(bubble, Dock.Top); layout.Children.Add(bubble);
            image = new Image { Source = assets.Frames[0], Stretch = Stretch.Uniform, Cursor = Cursors.Hand, ToolTip = "Clique para conversar. Arraste para mover. Botao direito para opcoes." };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            layout.Children.Add(image); Content = layout;
            bubble.MouseLeftButtonUp += delegate { talk(); };
            image.MouseLeftButtonDown += OnPress;
            image.MouseMove += OnMove;
            image.MouseLeftButtonUp += OnRelease;
            image.LostMouseCapture += delegate { pressing = false; };
            MouseWheel += delegate(object sender, MouseWheelEventArgs args) { ResizePet(state.Scale + (args.Delta > 0 ? 0.1 : -0.1)); args.Handled = true; };
            ContextMenu = BuildMenu();
            Loaded += delegate { RestorePosition(); Play("waving"); };
            clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            clock.Tick += delegate { Tick(); }; clock.Start();
            Closed += delegate { clock.Stop(); };
        }

        public void Play(string name)
        {
            if (!assets.Animations.ContainsKey(name)) name = "idle";
            animation = name; animationClock.Restart();
            image.Source = assets.Frames[assets.Animations[name].Frames[0]];
        }

        public string ActivePetId { get { return assets.Id; } }
        public void SetPet(PetAssets replacement)
        {
            assets = replacement; Title = "Negotchi - " + assets.DisplayName; Icon = assets.Frames[0];
            ContextMenu = BuildMenu(); ResizePet(state.Scale); nextIdle = DateTime.UtcNow.AddSeconds(20);
            Play("waving");
            Mood("Oi, sou " + assets.DisplayName + ".", "waving", false);
        }

        public void Mood(string text, string name, bool working)
        {
            busy = working; bubbleText.Text = text; bubbleUntil = DateTime.UtcNow.AddSeconds(7); bubble.Visibility = Visibility.Visible;
            if (animation != name) Play(name);
        }

        private void Tick()
        {
            var track = assets.Animations[animation];
            int tick = (int)(animationClock.Elapsed.TotalSeconds * track.Fps);
            if (tick >= track.Frames.Length && !track.Loop)
            {
                Play(track.Fallback); track = assets.Animations[animation]; tick = 0;
            }
            image.Source = assets.Frames[track.Frames[tick % track.Frames.Length]];
            bubble.Visibility = busy || DateTime.UtcNow < bubbleUntil ? Visibility.Visible : Visibility.Hidden;
            if (!busy && !pressing && animation == "idle" && DateTime.UtcNow >= nextIdle)
            {
                Play(random.Next(2) == 0 ? "waving" : "jumping"); nextIdle = DateTime.UtcNow.AddSeconds(random.Next(22, 40));
            }
        }

        private void OnPress(object sender, MouseButtonEventArgs args)
        {
            pressing = true; dragged = false; pressPoint = PointToScreen(args.GetPosition(this)); originalPosition = new Point(Left, Top);
            image.CaptureMouse(); args.Handled = true;
        }

        private void OnMove(object sender, MouseEventArgs args)
        {
            if (!pressing) return;
            Point current = PointToScreen(args.GetPosition(this));
            var source = PresentationSource.FromVisual(this);
            var delta = source.CompositionTarget.TransformFromDevice.Transform(current - pressPoint);
            if (!dragged && delta.Length < 5) return;
            dragged = true; Left = originalPosition.X + delta.X; Top = originalPosition.Y + delta.Y;
            string direction = delta.X >= 0 ? "running-right" : "running-left";
            if (animation != direction) Play(direction);
        }

        private void OnRelease(object sender, MouseButtonEventArgs args)
        {
            if (!pressing) return;
            pressing = false; image.ReleaseMouseCapture();
            if (dragged) { ClampPosition(); PersistPosition(); Play(busy ? "running" : "idle"); }
            else talk();
            args.Handled = true;
        }

        internal static Rect Clamp(Rect pet, Rect work)
        {
            pet.X = Math.Max(work.Left, Math.Min(work.Right - pet.Width, pet.X));
            pet.Y = Math.Max(work.Top, Math.Min(work.Bottom - pet.Height, pet.Y));
            return pet;
        }

        private Rect WorkingArea(Forms.Screen screen)
        {
            var source = PresentationSource.FromVisual(this);
            var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
            var area = screen.WorkingArea;
            return new Rect(transform.Transform(new Point(area.Left, area.Top)), transform.Transform(new Point(area.Right, area.Bottom)));
        }

        public void RestorePosition()
        {
            bool valid = !(state.X == -1 && state.Y == -1) && Forms.Screen.AllScreens.Any(screen => WorkingArea(screen).IntersectsWith(new Rect(state.X, state.Y, Width, Height)));
            if (valid) { Left = state.X; Top = state.Y; ClampPosition(); }
            else Reposition();
        }

        public void Reposition()
        {
            var work = WorkingArea(Forms.Screen.PrimaryScreen);
            Left = work.Right - Width - 30; Top = work.Bottom - Height - 18;
            PersistPosition();
        }

        private void ClampPosition()
        {
            var source = PresentationSource.FromVisual(this);
            var transform = source == null ? Matrix.Identity : source.CompositionTarget.TransformToDevice;
            var center = transform.Transform(new Point(Left + Width / 2, Top + Height / 2));
            var screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)center.X, (int)center.Y));
            var pet = Clamp(new Rect(Left, Top, Width, Height), WorkingArea(screen)); Left = pet.X; Top = pet.Y;
        }

        public void PersistPosition() { state.X = Left; state.Y = Top; save(); }

        public void ResizePet(double scale)
        {
            state.Scale = Math.Max(0.65, Math.Min(1.8, scale));
            double bottom = Top + Height;
            Width = assets.FrameWidth * state.Scale; Height = assets.FrameHeight * state.Scale + 44; Top = bottom - Height;
            if (IsLoaded) { ClampPosition(); PersistPosition(); }
        }

        private ContextMenu BuildMenu()
        {
            var menu = new ContextMenu();
            AddMenu(menu, "Conversar com " + assets.DisplayName, talk);
            AddMenu(menu, "Trocar pet...", delegate { if (ChoosePet != null) ChoosePet(); });
            AddMenu(menu, "Abrir arquivo...", delegate { if (OpenFile != null) OpenFile(); });
            AddMenu(menu, "Escolher pasta de trabalho...", delegate { if (ChooseFolder != null) ChooseFolder(); });
            menu.Items.Add(new Separator());
            var top = new MenuItem { Header = "Sempre visivel", IsCheckable = true, IsChecked = state.Topmost };
            top.Click += delegate { state.Topmost = top.IsChecked; Topmost = state.Topmost; save(); }; menu.Items.Add(top);
            AddMenu(menu, "Aumentar", delegate { ResizePet(state.Scale + 0.15); });
            AddMenu(menu, "Diminuir", delegate { ResizePet(state.Scale - 0.15); });
            AddMenu(menu, "Trazer para o canto da tela", Reposition);
            menu.Items.Add(new Separator());
            AddMenu(menu, "Sair do Negotchi", delegate { if (Quit != null) Quit(); });
            return menu;
        }

        private static void AddMenu(ContextMenu menu, string text, Action action)
        {
            var item = new MenuItem { Header = text }; item.Click += delegate { action(); }; menu.Items.Add(item);
        }
    }
}
