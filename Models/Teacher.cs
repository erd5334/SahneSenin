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
            set => SetProperty(ref _selectedArtists, value);
        }

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
    }
}
