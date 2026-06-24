using SahneSenin.ViewModels;

namespace SahneSenin.Models
{
    public class ArtistSelectionItem : BaseViewModel
    {
        private string _name = string.Empty;
        private bool _isSelected;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
