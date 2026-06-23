using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SahneSenin
{
    public partial class MainWindow : Window
    {
        private bool _isWaitingForProjectionKey = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BuildProjectionMenu();
            BuildTimeMenus();
            UpdateSettingsMenuHeader();
        }

        private void BuildProjectionMenu()
        {
            try
            {
                ProjectionMenu.Items.Clear();
                var screens = System.Windows.Forms.Screen.AllScreens;

                var autoItem = new MenuItem() { Header = "Otomatik (ilk non-primary)", Tag = -1, IsCheckable = true };
                autoItem.Click += ProjectionMenuItem_Click;
                ProjectionMenu.Items.Add(autoItem);

                for (int i = 0; i < screens.Length; i++)
                {
                    var s = screens[i];
                    var header = $"Ekran {i} - {s.Bounds.Width}x{s.Bounds.Height} ({(s.Primary ? "Primary" : "Secondary")})";
                    var mi = new MenuItem() { Header = header, Tag = i, IsCheckable = true };
                    mi.Click += ProjectionMenuItem_Click;
                    ProjectionMenu.Items.Add(mi);
                }

                var settings = AppSettings.Load();
                foreach (MenuItem mi in ProjectionMenu.Items)
                {
                    if (mi.Tag is int idx && idx == settings.ProjectionScreenIndex)
                    {
                        mi.IsChecked = true;
                    }
                }
            }
            catch { }
        }

        private void ProjectionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem mi && mi.Tag is int idx)
                {
                    var settings = AppSettings.Load();
                    settings.ProjectionScreenIndex = idx;
                    settings.Save();

                    foreach (MenuItem item in ProjectionMenu.Items)
                    {
                        item.IsChecked = false;
                    }
                    mi.IsChecked = true;

                    // Apply the new position to current projection window if open
                    var app = (App)System.Windows.Application.Current;
                    var displayWindow = app.GetProjectionWindow();
                    if (displayWindow != null)
                    {
                        displayWindow.ShowOnTargetMonitor();
                    }
                }
            }
            catch { }
        }

        private void MenuToggleProj_Click(object sender, RoutedEventArgs e)
        {
            ((App)System.Windows.Application.Current).ToggleProjection();
        }

        private void MenuProjShortcut_Click(object sender, RoutedEventArgs e)
        {
            _isWaitingForProjectionKey = true;
            MenuProjShortcut.Header = "Yansıtma Kısayolu: Tuşa Basın...";
        }

        private void UpdateSettingsMenuHeader()
        {
            try
            {
                var settings = AppSettings.Load();
                string keyText = settings.ProjectionShortcutKey == Key.None ? "Yok" : settings.ProjectionShortcutKey.ToString();
                MenuProjShortcut.Header = $"Kısayol Tuşu Belirle: {keyText}";
            }
            catch { }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var settings = AppSettings.Load();

            if (_isWaitingForProjectionKey)
            {
                settings.ProjectionShortcutKey = e.Key;
                settings.Save();
                _isWaitingForProjectionKey = false;
                UpdateSettingsMenuHeader();
                e.Handled = true;
                return;
            }

            if (e.Key == settings.ProjectionShortcutKey && settings.ProjectionShortcutKey != Key.None)
            {
                ((App)System.Windows.Application.Current).ToggleProjection();
                e.Handled = true;
            }
        }

        private void BuildTimeMenus()
        {
            try
            {
                MenuListeningDuration.Items.Clear();
                MenuGuessingDuration.Items.Clear();

                int[] durations = { 5, 10, 15, 20, 30 };
                var settings = AppSettings.Load();

                foreach (int sec in durations)
                {
                    var listeningItem = new MenuItem { Header = $"{sec} Saniye", Tag = sec, IsCheckable = true };
                    listeningItem.Click += ListeningTimeItem_Click;
                    if (settings.ListeningDuration == sec)
                    {
                        listeningItem.IsChecked = true;
                    }
                    MenuListeningDuration.Items.Add(listeningItem);

                    var guessingItem = new MenuItem { Header = $"{sec} Saniye", Tag = sec, IsCheckable = true };
                    guessingItem.Click += GuessingTimeItem_Click;
                    if (settings.GuessingDuration == sec)
                    {
                        guessingItem.IsChecked = true;
                    }
                    MenuGuessingDuration.Items.Add(guessingItem);
                }
            }
            catch { }
        }

        private void ListeningTimeItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem mi && mi.Tag is int sec)
                {
                    var settings = AppSettings.Load();
                    settings.ListeningDuration = sec;
                    settings.Save();

                    foreach (MenuItem item in MenuListeningDuration.Items)
                    {
                        item.IsChecked = false;
                    }
                    mi.IsChecked = true;
                }
            }
            catch { }
        }

        private void GuessingTimeItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem mi && mi.Tag is int sec)
                {
                    var settings = AppSettings.Load();
                    settings.GuessingDuration = sec;
                    settings.Save();

                    foreach (MenuItem item in MenuGuessingDuration.Items)
                    {
                        item.IsChecked = false;
                    }
                    mi.IsChecked = true;
                }
            }
            catch { }
        }
    }
}