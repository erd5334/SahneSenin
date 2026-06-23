using System.Collections.Generic;
using SahneSenin.ViewModels;

namespace SahneSenin.Models
{
    public class Teacher : BaseViewModel
    {
        private string _name = string.Empty;
        private List<string> _selectedArtists = new();
        private int _score;
        private bool _hasPlayed;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public List<string> SelectedArtists
        {
            get => _selectedArtists;
            set
            {
                if (SetProperty(ref _selectedArtists, value))
                {
                    OnPropertyChanged(nameof(SelectedArtistsText));
                }
            }
        }

        public string SelectedArtistsText => string.Join(", ", SelectedArtists);

        public int Score
        {
            get => _score;
            set => SetProperty(ref _score, value);
        }

        public bool HasPlayed
        {
            get => _hasPlayed;
            set => SetProperty(ref _hasPlayed, value);
        }

        private bool _isJokerUsed;
        public bool IsJokerUsed
        {
            get => _isJokerUsed;
            set => SetProperty(ref _isJokerUsed, value);
        }

        private string? _photoPath;
        public string? PhotoPath
        {
            get => _photoPath;
            set
            {
                if (SetProperty(ref _photoPath, value))
                {
                    OnPropertyChanged(nameof(HasPhoto));
                }
            }
        }

        public bool HasPhoto => !string.IsNullOrEmpty(PhotoPath) && System.IO.File.Exists(PhotoPath);

        public string Initials
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name)) return "?";
                var parts = Name.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                if (parts.Length == 1) return parts[0].Substring(0, System.Math.Min(2, parts[0].Length)).ToUpper();
                return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
            }
        }
    }
}
