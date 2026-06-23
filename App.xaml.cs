using System;
using System.Linq;
using System.Windows;
using SahneSenin.Services;
using SahneSenin.ViewModels;

namespace SahneSenin
{
    public partial class App : System.Windows.Application
    {
        private DataService? _dataService;
        private AudioService? _audioService;
        private MainViewModel? _mainViewModel;
        private DisplayWindow? _displayWindow;

        public void ToggleProjection()
        {
            if (_displayWindow == null)
            {
                _displayWindow = new DisplayWindow
                {
                    DataContext = _mainViewModel
                };
                _displayWindow.Closed += (s, args) => _displayWindow = null;
                _displayWindow.ShowOnTargetMonitor();
            }
            else
            {
                _displayWindow.Close();
            }
        }

        public void CloseProjection()
        {
            if (_displayWindow != null)
            {
                _displayWindow.Close();
                _displayWindow = null;
            }
        }

        public DisplayWindow? GetProjectionWindow() => _displayWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize Services
            _dataService = new DataService();
            _audioService = new AudioService(_dataService);

            // Initialize Shared ViewModel
            _mainViewModel = new MainViewModel(_dataService, _audioService);

            // Detect screens
            var screens = System.Windows.Forms.Screen.AllScreens;
            var primaryScreen = screens.FirstOrDefault(s => s.Primary) ?? screens.FirstOrDefault();

            // Create Host Panel (MainWindow)
            var mainWindow = new MainWindow
            {
                DataContext = _mainViewModel
            };

            if (primaryScreen != null)
            {
                mainWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                mainWindow.Left = primaryScreen.WorkingArea.Left + 50;
                mainWindow.Top = primaryScreen.WorkingArea.Top + 50;
                mainWindow.Width = 1024;
                mainWindow.Height = 700;
            }

            mainWindow.Show();
        }
    }
}
