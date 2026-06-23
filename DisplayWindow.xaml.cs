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
        private List<Teacher> _currentWheelTeachers = new();

        private readonly System.Windows.Media.Brush[] _sliceColors = new System.Windows.Media.Brush[]
        {
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF007F")),
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00F2FE")),
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00FF87")),
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF000")),
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BD00FF")),
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5E00")),
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF00FF"))
        };

        private readonly System.Windows.Media.Brush[] _sliceTextColors = new System.Windows.Media.Brush[]
        {
            System.Windows.Media.Brushes.White, // for Pink
            System.Windows.Media.Brushes.Black, // for Cyan
            System.Windows.Media.Brushes.Black, // for Green/Lime
            System.Windows.Media.Brushes.Black, // for Yellow
            System.Windows.Media.Brushes.White, // for Purple
            System.Windows.Media.Brushes.Black, // for Orange
            System.Windows.Media.Brushes.White  // for Magenta
        };

        public DisplayWindow()
        {
            InitializeComponent();
            
            foreach (var brush in _sliceColors)
            {
                brush.Freeze();
            }
            
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
                vm.PropertyChanged -= Vm_PropertyChanged;
                vm.PropertyChanged += Vm_PropertyChanged;

                if (vm.CurrentState == GameState.TeacherSelection)
                {
                    _currentWheelTeachers = vm.UnplayedTeachers.ToList();
                    DrawWheel();
                }
            }
        }

        private void DisplayWindow_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is MainViewModel oldVm)
            {
                oldVm.SpinStarted -= OnSpinStarted;
                oldVm.ConfettiTriggered -= OnConfettiTriggered;
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }

            if (e.NewValue is MainViewModel newVm)
            {
                newVm.SpinStarted -= OnSpinStarted; // Prevent duplicate subscriptions
                newVm.SpinStarted += OnSpinStarted;
                newVm.ConfettiTriggered -= OnConfettiTriggered;
                newVm.ConfettiTriggered += OnConfettiTriggered;
                newVm.PropertyChanged -= Vm_PropertyChanged;
                newVm.PropertyChanged += Vm_PropertyChanged;

                if (newVm.CurrentState == GameState.TeacherSelection)
                {
                    _currentWheelTeachers = newVm.UnplayedTeachers.ToList();
                    DrawWheel();
                }
            }
        }

        private void DisplayWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SpinStarted -= OnSpinStarted;
                vm.ConfettiTriggered -= OnConfettiTriggered;
                vm.PropertyChanged -= Vm_PropertyChanged;
            }
            StopConfetti();
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentState) || e.PropertyName == nameof(MainViewModel.UnplayedTeachers))
            {
                if (DataContext is MainViewModel vm && vm.CurrentState == GameState.TeacherSelection)
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Update the list of teachers on the wheel:
                        // 1. When we just entered the TeacherSelection state (CurrentState changed)
                        // 2. When the list of unplayed teachers changed and we are NOT in the middle of a spin.
                        if (e.PropertyName == nameof(MainViewModel.CurrentState) || vm.IsSpinCompleted)
                        {
                            _currentWheelTeachers = vm.UnplayedTeachers.ToList();
                        }
                        DrawWheel();
                    });
                }
            }
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

                vm.IsSpinCompleted = false;

                // Stop any running animations on rotation
                WheelRotation.BeginAnimation(RotateTransform.AngleProperty, null);

                var teachersList = _currentWheelTeachers;
                if (teachersList.Count == 0 || !teachersList.Any(t => t.Name == selectedTeacher.Name))
                {
                    teachersList = vm.UnplayedTeachers.ToList();
                    if (!teachersList.Any(t => t.Name == selectedTeacher.Name))
                    {
                        teachersList.Add(selectedTeacher);
                    }
                    _currentWheelTeachers = teachersList;
                }

                // Make sure the wheel displays the current list (including the winner)
                DrawWheel();

                int N = teachersList.Count;
                if (N == 0) return;

                int winnerIndex = teachersList.FindIndex(t => t.Name == selectedTeacher.Name);
                if (winnerIndex < 0) winnerIndex = 0;

                double sliceWidth = 360.0 / N;
                
                // Align center of selected slice with the top pointer (270 degrees)
                double winnerMidAngle = winnerIndex * sliceWidth + sliceWidth / 2.0;
                double baseTargetAngle = 270.0 - winnerMidAngle;

                // Add random offset inside slice (e.g. up to 25% of slice width left or right) for natural physics feel
                var rand = new Random();
                double randomOffset = (rand.NextDouble() - 0.5) * (sliceWidth * 0.5);

                double currentAngle = WheelRotation.Angle;
                // Add 6 full spins
                double targetAngle = currentAngle + 360.0 * 6 + (baseTargetAngle - (currentAngle % 360.0) + randomOffset);

                var doubleAnimation = new DoubleAnimation
                {
                    From = currentAngle,
                    To = targetAngle,
                    Duration = TimeSpan.FromSeconds(5.5),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                // Play tick sound when each slice boundary crosses the pointer (270 degrees)
                double startRotAngle = WheelRotation.Angle;
                double startRelativeAngle = (270.0 - startRotAngle) % 360.0;
                if (startRelativeAngle < 0) startRelativeAngle += 360.0;
                int lastLoggedSliceIndex = (int)(startRelativeAngle / sliceWidth);
                double lastTickAngle = startRotAngle;

                doubleAnimation.CurrentTimeInvalidated += (s, ev) =>
                {
                    try
                    {
                        double currentRotAngle = WheelRotation.Angle;
                        // Only play tick if we rotated at least 1.0 degree since last tick (prevents boundary jitter)
                        if (Math.Abs(currentRotAngle - lastTickAngle) >= 1.0)
                        {
                            double relativeAngle = (270.0 - currentRotAngle) % 360.0;
                            if (relativeAngle < 0) relativeAngle += 360.0;

                            int currentSliceIndex = (int)(relativeAngle / sliceWidth);
                            if (currentSliceIndex != lastLoggedSliceIndex)
                            {
                                lastLoggedSliceIndex = currentSliceIndex;
                                lastTickAngle = currentRotAngle;
                                vm.AudioService.PlaySfx("tick");
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                };

                doubleAnimation.Completed += (s, ev) =>
                {
                    try
                    {
                        // Play completion chimes
                        vm.AudioService.PlaySfx("correct");

                        // Highlight flash effect on the wheel container
                        var blinkAnimation = new DoubleAnimation
                        {
                            From = 1.0,
                            To = 0.4,
                            Duration = TimeSpan.FromMilliseconds(200),
                            AutoReverse = true,
                            RepeatBehavior = new RepeatBehavior(3)
                        };
                        WheelContainer.BeginAnimation(OpacityProperty, blinkAnimation);
                    }
                    catch
                    {
                        // Ignore
                    }
                    finally
                    {
                        vm.IsSpinCompleted = true;
                    }
                };

                WheelRotation.BeginAnimation(RotateTransform.AngleProperty, doubleAnimation);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Çark Döndürme Hatası:\n{ex.Message}\n\n{ex.StackTrace}", "Çark Hatası", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void DrawWheel()
        {
            try
            {
                WheelCanvas.Children.Clear();

                if (DataContext is not MainViewModel vm) return;

                var teachersList = _currentWheelTeachers;
                if (teachersList.Count == 0)
                {
                    teachersList = vm.UnplayedTeachers.ToList();
                    if (teachersList.Count == 0)
                    {
                        teachersList = vm.Teachers.ToList();
                    }
                    _currentWheelTeachers = teachersList;
                }
                if (teachersList.Count == 0) return;

                int N = teachersList.Count;
                double sliceWidth = 360.0 / N;
                double radius = 230; // Canvas size is 460x460, center is 230, 230
                double centerX = 230;
                double centerY = 230;

                // 1. Draw all slices first so they don't overlap text labels
                for (int i = 0; i < N; i++)
                {
                    double startAngle = i * sliceWidth;
                    double endAngle = (i + 1) * sliceWidth;

                    var fillBrush = _sliceColors[i % _sliceColors.Length];

                    if (N == 1)
                    {
                        var ellipse = new Ellipse
                        {
                            Width = radius * 2,
                            Height = radius * 2,
                            Fill = fillBrush,
                            Stroke = System.Windows.Media.Brushes.Black,
                            StrokeThickness = 1.5
                        };
                        Canvas.SetLeft(ellipse, centerX - radius);
                        Canvas.SetTop(ellipse, centerY - radius);
                        WheelCanvas.Children.Add(ellipse);
                    }
                    else
                    {
                        var slicePath = CreatePieSlice(centerX, centerY, radius, startAngle, endAngle, fillBrush, System.Windows.Media.Brushes.Black);
                        WheelCanvas.Children.Add(slicePath);
                    }
                }

                // 2. Draw all text labels second on top of all slices
                for (int i = 0; i < N; i++)
                {
                    double startAngle = i * sliceWidth;
                    double midAngle = startAngle + sliceWidth / 2.0;

                    // Normalize angle to [0, 360)
                    midAngle = (midAngle % 360.0 + 360.0) % 360.0;

                    // Determine if the slice is on the left half of the wheel (90 to 270 degrees)
                    // On the left half, we flip the text 180 degrees so it's right-side up.
                    bool isLeftHalf = midAngle > 90.0 && midAngle < 270.0;

                    var textBrush = _sliceTextColors[i % _sliceTextColors.Length];

                    var tb = new TextBlock
                    {
                        Text = teachersList[i].Name,
                        Foreground = textBrush,
                        FontSize = N > 25 ? 9 : (N > 15 ? 11 : 13),
                        FontWeight = FontWeights.Bold,
                        VerticalAlignment = VerticalAlignment.Center,
                        Width = radius - 60, // Maximum text width
                        Height = 20,
                        // Add a drop shadow to make the text pop on any background color
                        // White text gets black shadow, black text gets white/glow shadow
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = textBrush == System.Windows.Media.Brushes.White ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.White,
                            BlurRadius = 4,
                            ShadowDepth = 0,
                            Opacity = 0.95
                        }
                    };

                    if (isLeftHalf)
                    {
                        // Align text to the left (outer edge)
                        tb.TextAlignment = TextAlignment.Left;
                        // Place TextBlock to the left of the center (from x = 15 to x = 185)
                        Canvas.SetLeft(tb, centerX - radius + 15); // radius is 230, so 230 - 230 + 15 = 15
                        Canvas.SetTop(tb, centerY - 10);
                        // Rotate 180 degrees extra around the center of the wheel (local coordinates: x = radius - 15, y = 10)
                        tb.RenderTransform = new RotateTransform(midAngle + 180, radius - 15, 10);
                    }
                    else
                    {
                        // Align text to the right (outer edge)
                        tb.TextAlignment = TextAlignment.Right;
                        // Place TextBlock to the right of the center (from x = 275 to x = 445)
                        Canvas.SetLeft(tb, centerX + 45); // 230 + 45 = 275
                        Canvas.SetTop(tb, centerY - 10);
                        // Rotate around the center of the wheel (local coordinates: x = -45, y = 10)
                        tb.RenderTransform = new RotateTransform(midAngle, -45, 10);
                    }

                    WheelCanvas.Children.Add(tb);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing wheel: {ex.Message}");
            }
        }

        private Path CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double endAngle, System.Windows.Media.Brush fillBrush, System.Windows.Media.Brush strokeBrush)
        {
            var path = new Path
            {
                Fill = fillBrush,
                Stroke = strokeBrush,
                StrokeThickness = 1.5
            };

            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new System.Windows.Point(centerX, centerY),
                IsClosed = true
            };

            double startRad = startAngle * Math.PI / 180.0;
            double startX = centerX + radius * Math.Cos(startRad);
            double startY = centerY + radius * Math.Sin(startRad);
            figure.Segments.Add(new LineSegment(new System.Windows.Point(startX, startY), true));

            double endRad = endAngle * Math.PI / 180.0;
            double endX = centerX + radius * Math.Cos(endRad);
            double endY = centerY + radius * Math.Sin(endRad);

            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;
            var arc = new ArcSegment(
                new System.Windows.Point(endX, endY),
                new System.Windows.Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true
            );
            figure.Segments.Add(arc);

            geometry.Figures.Add(figure);
            path.Data = geometry;

            return path;
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
