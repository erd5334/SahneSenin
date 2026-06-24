using System;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace SahneSenin
{
    public class AppSettings
    {
        private const string SettingsFile = "app_settings.json";

        public int ProjectionScreenIndex { get; set; } = -1; // -1 = auto (first non-primary)
        public Key ProjectionShortcutKey { get; set; } = Key.F12; // Default to F12
        public int ListeningDuration { get; set; } = 10;
        public int GuessingDuration { get; set; } = 10;
        public string CustomMusicPoolPath { get; set; } = string.Empty;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
