using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SahneSenin.Models;
using SahneSenin.ViewModels;

namespace SahneSenin
{
    public partial class DisplayWindow : Window
    {
        private class ConfettiParticle
        {
            public Shape Element { get; set; } = null!;
            public double X { get; set; }
            public double Y { get; set; }
            public double VelocityX { get; set; }
            public double VelocityY { get; set; }
            public double Rotation { get; set; }
            public double RotationSpeed { get; set; }
        }

        private readonly List<ConfettiParticle> _confettiParticles = new();
        private readonly Random _random = new();
        private bool _isAnimatingConfetti = false;
        private double _screenWidth = 1920;
        private double _screenHeight = 1080;

        public DisplayWindow()
        {
            InitializeComponent();
            
            Loaded += DisplayWindow_Loaded;
            Unloaded += DisplayWindow_Unloaded;
            DataContextChanged += DisplayWindow_DataContextChanged;
        }

        public void ShowOnTargetMonitor()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                if (screens.Length == 0)
                {
                    this.Show();
                    return;
                }

                var appSettings = AppSettings.Load();
                System.Windows.Forms.Screen target = screens[0];

                if (appSettings.ProjectionScreenIndex >= 0 && appSettings.ProjectionScreenIndex < screens.Length)
                {
                    target = screens[appSettings.ProjectionScreenIndex];
                }
                else if (screens.Length > 1)
                {
                    // Auto: pick first non-primary screen
                    foreach (var screen in screens)
                    {
                        if (!screen.Primary)
                        {
                            target = screen;
                            break;
                        }
                    }
                }

                var workingArea = target.WorkingArea;

                // Get current DPI scaling factors from the main window (which is already loaded)
                double dpiScaleX = 1.0;
                double dpiScaleY = 1.0;

                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var dpi = VisualTreeHelper.GetDpi(mainWindow);
                    dpiScaleX = dpi.DpiScaleX;
                    dpiScaleY = dpi.DpiScaleY;
                }

                // Set coordinates in logical (DIP) units by dividing physical pixels by DPI scale
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Left = workingArea.Left / dpiScaleX;
                this.Top = workingArea.Top / dpiScaleY;
                this.Width = workingArea.Width / dpiScaleX;
                this.Height = workingArea.Height / dpiScaleY;

                this.WindowStyle = WindowStyle.None;
                this.Topmost = true;

                // Show in Normal state first, then maximize for perfect borderless
                this.WindowState = WindowState.Normal;
                this.Show();
                this.WindowState = WindowState.Maximized;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error positioning display window: " + ex.Message);
                this.Show();
            }
        }

        public void ToggleBlackout()
        {
            BlackoutOverlay.Visibility = (BlackoutOverlay.Visibility == Visibility.Visible) 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        public void SetBlackout(bool blackout)
        {
            BlackoutOverlay.Visibility = blackout ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                this.Close();
                e.Handled = true;
                return;
            }

            var appSettings = AppSettings.Load();
            if (e.Key == Key.B || e.Key == appSettings.ProjectionShortcutKey)
            {
                ToggleBlackout();
                e.Handled = true;
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.WindowStyle == WindowStyle.None)
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
            e.Handled = true;
        }

        private void DisplayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _screenWidth = ActualWidth;
            _screenHeight = ActualHeight;

            // Subscribe to VM events
            if (DataContext is MainViewModel vm)
            {
                vm.SpinStarted -= OnSpinStarted; // Prevent duplicate subscriptions
                vm.SpinStarted += OnSpinStarted;
                vm.ConfettiTriggered -= OnConfettiTriggered;
                vm.ConfettiTriggered += OnConfettiTriggered;
            }
        }

        private void DisplayWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.SpinStarted -= OnSpinStarted;
                oldVm.ConfettiTriggered -= OnConfettiTriggered;
            }

            if (e.NewValue is MainViewModel newVm)
            {
                newVm.SpinStarted -= OnSpinStarted; // Prevent duplicate subscriptions
                newVm.SpinStarted += OnSpinStarted;
                newVm.ConfettiTriggered -= OnConfettiTriggered;
                newVm.ConfettiTriggered += OnConfettiTriggered;
            }
        }

        private void DisplayWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SpinStarted -= OnSpinStarted;
                vm.ConfettiTriggered -= OnConfettiTriggered;
            }
            StopConfetti();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            _screenWidth = sizeInfo.NewSize.Width;
            _screenHeight = sizeInfo.NewSize.Height;
        }

        private void OnSpinStarted(object? sender, Teacher selectedTeacher)
        {
            try
            {
                if (DataContext is not MainViewModel vm) return;

                // Build scrolling list of names
                SpinStackPanel.Children.Clear();

                // Set IsSpinCompleted to false when spin starts
                vm.IsSpinCompleted = false;

                var teachersList = vm.UnplayedTeachers.ToList();
                if (!teachersList.Any(t => t.Name == selectedTeacher.Name))
                {
                    teachersList.Add(selectedTeacher);
                }

                // If there's only 1 teacher, make it simple
                if (teachersList.Count == 1)
                {
                    var tb = CreateNameTextBlock(selectedTeacher.Name);
                    SpinStackPanel.Children.Add(tb);
                    vm.IsSpinCompleted = true;
                    return;
                }

                // To make scrolling animation long and interesting, repeat names list
                // We want the scroll animation to land on the selectedTeacher
                var scrollList = new List<string>();
                int repeatCount = 5; // Repeat list multiple times
                
                for (int r = 0; r < repeatCount; r++)
                {
                    foreach (var t in teachersList)
                    {
                        scrollList.Add(t.Name);
                    }
                }

                // Shuffle the intermediate names to make it look random, 
                // but keep the final target name at the very end
                int totalItems = scrollList.Count;
                // Place the target teacher at the end index (e.g. totalItems - 3 to look natural)
                int targetIndex = totalItems - 4;
                scrollList[targetIndex] = selectedTeacher.Name;

                // Populate the visual StackPanel
                foreach (var name in scrollList)
                {
                    SpinStackPanel.Children.Add(CreateNameTextBlock(name));
                }

                // Create animation
                double nameHeight = 160; // Each textblock has 160 height (matches XAML)
                double targetOffset = -targetIndex * nameHeight;

                var translate = new TranslateTransform();
                SpinStackPanel.RenderTransform = translate;

                var doubleAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = targetOffset,
                    Duration = TimeSpan.FromSeconds(4.5),
                    DecelerationRatio = 0.85
                };

                // Play tick sound effects during animation
                double lastTickedOffset = 0;
                doubleAnimation.CurrentTimeInvalidated += (s, ev) =>
                {
                    try
                    {
                        double currentOffset = translate.Y;
                        double delta = Math.Abs(currentOffset - lastTickedOffset);
                        if (delta >= nameHeight)
                        {
                            lastTickedOffset = currentOffset - (currentOffset % nameHeight);
                            // Play tick sound locally
                            System.Media.SystemSounds.Hand.Play(); // Short beep/tick
                        }
                    }
                    catch
                    {
                        // Ignore tick sound exceptions
                    }
                };

                doubleAnimation.Completed += (s, ev) =>
                {
                    try
                    {
                        // Play completion chime
                        System.Media.SystemSounds.Question.Play();

                        // Highlight effect (blink the border)
                        var blinkAnimation = new DoubleAnimation
                        {
                            From = 1.0,
                            To = 0.3,
                            Duration = TimeSpan.FromMilliseconds(200),
                            AutoReverse = true,
                            RepeatBehavior = new RepeatBehavior(3)
                        };
                        SpinClipBorder.BeginAnimation(OpacityProperty, blinkAnimation);
                    }
                    catch
                    {
                        // Ignore secondary visual exceptions
                    }
                    finally
                    {
                        // Spin completed. Wait for host to manually start the round.
                        vm.IsSpinCompleted = true;
                    }
                };

                translate.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Spin Başlatılamadı Hatası:\n{ex.Message}\n\n{ex.StackTrace}", "Çark Hatası", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private TextBlock CreateNameTextBlock(string name)
        {
            var tb = new TextBlock
            {
                Text = name,
                Height = 160,
                FontSize = 60,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            // Safely apply a unique DropShadowEffect instance
            if (System.Windows.Application.Current.Resources["NeonPink"] is System.Windows.Media.Color pinkColor)
            {
                tb.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = pinkColor,
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.75
                };
            }

            return tb;
        }

        private void OnConfettiTriggered(double duration)
        {
            if (_isAnimatingConfetti) return;

            // Spawn particles
            int count = 150;
            System.Windows.Media.Brush[] colors = new System.Windows.Media.Brush[]
            {
                (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["NeonPinkBrush"],
                (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["NeonCyanBrush"],
                (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["NeonGreenBrush"],
                (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["NeonYellowBrush"],
                System.Windows.Media.Brushes.DeepSkyBlue,
                System.Windows.Media.Brushes.Gold,
                System.Windows.Media.Brushes.Magenta
            };

            for (int i = 0; i < count; i++)
            {
                Shape shape;
                if (_random.Next(2) == 0)
                {
                    shape = new System.Windows.Shapes.Rectangle
                    {
                        Width = _random.Next(8, 18),
                        Height = _random.Next(8, 18),
                        Fill = colors[_random.Next(colors.Length)]
                    };
                }
                else
                {
                    shape = new Ellipse
                    {
                        Width = _random.Next(8, 16),
                        Height = _random.Next(8, 16),
                        Fill = colors[_random.Next(colors.Length)]
                    };
                }

                var particle = new ConfettiParticle
                {
                    Element = shape,
                    X = _random.NextDouble() * _screenWidth,
                    Y = -_random.Next(20, 500), // Start above screen at random heights
                    VelocityX = (_random.NextDouble() - 0.5) * 6, // Wind
                    VelocityY = _random.NextDouble() * 8 + 4,      // Speed fall
                    Rotation = _random.NextDouble() * 360,
                    RotationSpeed = (_random.NextDouble() - 0.5) * 15
                };

                Canvas.SetLeft(shape, particle.X);
                Canvas.SetTop(shape, particle.Y);

                // Add rotation transform
                var transformGroup = new TransformGroup();
                var rotate = new RotateTransform(particle.Rotation, shape.Width / 2, shape.Height / 2);
                transformGroup.Children.Add(rotate);
                shape.RenderTransform = transformGroup;

                ConfettiCanvas.Children.Add(shape);
                _confettiParticles.Add(particle);
            }

            _isAnimatingConfetti = true;
            CompositionTarget.Rendering += OnCompositionTargetRendering;

            // Stop confetti animation after specified duration
            var stopTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(duration) };
            stopTimer.Tick += (s, e) =>
            {
                stopTimer.Stop();
                StopConfetti();
            };
            stopTimer.Start();
        }

        private void OnCompositionTargetRendering(object? sender, EventArgs e)
        {
            if (!_isAnimatingConfetti) return;

            for (int i = _confettiParticles.Count - 1; i >= 0; i--)
            {
                var p = _confettiParticles[i];
                p.Y += p.VelocityY;
                p.X += p.VelocityX;
                p.Rotation += p.RotationSpeed;

                // Wind turbulence
                p.VelocityX += (_random.NextDouble() - 0.5) * 0.2;

                // Update WPF layout properties
                Canvas.SetTop(p.Element, p.Y);
                Canvas.SetLeft(p.Element, p.X);

                if (p.Element.RenderTransform is TransformGroup group && group.Children[0] is RotateTransform rotate)
                {
                    rotate.Angle = p.Rotation;
                }

                // If particle goes off bottom, wrap back to top with new speed
                if (p.Y > _screenHeight)
                {
                    p.Y = -20;
                    p.X = _random.NextDouble() * _screenWidth;
                    p.VelocityY = _random.NextDouble() * 8 + 4;
                    p.VelocityX = (_random.NextDouble() - 0.5) * 6;
                }
            }
        }

        private void StopConfetti()
        {
            if (!_isAnimatingConfetti) return;

            _isAnimatingConfetti = false;
            CompositionTarget.Rendering -= OnCompositionTargetRendering;

            ConfettiCanvas.Children.Clear();
            _confettiParticles.Clear();
        }
    }
}
