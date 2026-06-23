using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SahneSenin.Models;

namespace SahneSenin.Services
{
    public class DataService
    {
        private readonly string _dataFilePath;
        private readonly string _musicPoolPath;
        private readonly string _teacherPhotosPath;

        public DataService()
        {
            _dataFilePath = GetFilePath("data.json");
            _musicPoolPath = GetFilePath("MusicPool");
            _teacherPhotosPath = GetFilePath("TeacherPhotos");

            // Ensure MusicPool exists
            if (!Directory.Exists(_musicPoolPath))
            {
                Directory.CreateDirectory(_musicPoolPath);
            }

            // Ensure TeacherPhotos exists
            if (!Directory.Exists(_teacherPhotosPath))
            {
                Directory.CreateDirectory(_teacherPhotosPath);
            }
        }

        public GameData LoadData()
        {
            GameData? data = null;

            if (File.Exists(_dataFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_dataFilePath);
                    data = JsonSerializer.Deserialize<GameData>(json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading JSON: {ex.Message}");
                }
            }

            if (data == null)
            {
                data = new GameData();
            }

            // Always scan the music pool on load to update the available song mapping
            data.Artists = ScanMusicPool();
            
            return data;
        }

        public void SaveData(GameData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dataFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving JSON: {ex.Message}");
            }
        }

        public void ImportFromCsv(string csvFilePath)
        {
            if (!File.Exists(csvFilePath)) return;

            var newTeachers = new List<Teacher>();
            var lines = File.ReadAllLines(csvFilePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split by semicolon (Turkish Excel format) or fallback to comma
                var parts = line.Split(';');
                if (parts.Length < 2)
                {
                    parts = line.Split(',');
                }

                if (parts.Length < 1) continue;

                var name = parts[0].Trim();
                if (string.IsNullOrEmpty(name) || name.StartsWith("#") || name.Equals("Ad Soyad", StringComparison.OrdinalIgnoreCase) || name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    continue; // Skip comments and headers

                var selectedArtists = new List<string>();
                for (int i = 1; i < parts.Length; i++)
                {
                    var artist = parts[i].Trim();
                    if (!string.IsNullOrEmpty(artist))
                    {
                        selectedArtists.Add(artist);
                    }
                }

                newTeachers.Add(new Teacher
                {
                    Name = name,
                    SelectedArtists = selectedArtists,
                    Score = 0,
                    HasPlayed = false
                });
            }

            if (newTeachers.Count > 0)
            {
                var data = LoadData();
                data.Teachers = newTeachers;
                SaveData(data);
            }
        }

        public Dictionary<string, List<string>> ScanMusicPool()
        {
            var artists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(_musicPoolPath))
            {
                return artists;
            }

            var mp3Files = Directory.GetFiles(_musicPoolPath, "*.mp3", SearchOption.AllDirectories);
            foreach (var filePath in mp3Files)
            {
                string relativePath = Path.GetRelativePath(_musicPoolPath, filePath);
                string[] pathParts = relativePath.Split(Path.DirectorySeparatorChar);

                string artistName = "Genel";

                if (pathParts.Length > 1)
                {
                    // It is in a subfolder. The first level subfolder is the artist name
                    artistName = pathParts[0];
                }
                else
                {
                    // It is in the root. Split filename by underscore or hyphen
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                    string[] parts = nameWithoutExt.Split(new[] { '_', '-' }, 2);
                    if (parts.Length == 2)
                    {
                        artistName = parts[0].Trim();
                    }
                }

                if (!artists.TryGetValue(artistName, out var songs))
                {
                    songs = new List<string>();
                    artists[artistName] = songs;
                }
                // Store the relative path instead of just the file name
                songs.Add(relativePath);
            }

            return artists;
        }

        public string GetMusicPoolDirectory()
        {
            return _musicPoolPath;
        }

        public string GetTeacherPhotosDirectory()
        {
            return _teacherPhotosPath;
        }

        private static string GetFilePath(string fileName)
        {
            // 1. Try executable base directory
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (File.Exists(localPath))
                return localPath;

             if (Directory.Exists(localPath))
            {
                if (fileName.Equals("MusicPool", StringComparison.OrdinalIgnoreCase))
                {
                    // Only return local MusicPool if it actually contains MP3 files
                    if (Directory.GetFiles(localPath, "*.mp3", SearchOption.AllDirectories).Length > 0)
                        return localPath;
                }
                else
                {
                    return localPath;
                }
            }

            // 2. Trailing check upwards for project root (Visual Studio / dotnet run fallback)
            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 4; i++)
            {
                string parentDir = Directory.GetParent(currentDir)?.FullName ?? currentDir;
                string combined = Path.Combine(parentDir, fileName);
                
                if (File.Exists(combined))
                    return combined;

                if (Directory.Exists(combined))
                {
                    if (fileName.Equals("MusicPool", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Directory.GetFiles(combined, "*.mp3", SearchOption.AllDirectories).Length > 0)
                            return combined;
                    }
                    else
                    {
                        return combined;
                    }
                }
                currentDir = parentDir;
            }

            // 3. Default back to executable folder
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }
    }
}
