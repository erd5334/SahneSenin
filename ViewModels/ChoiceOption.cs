using System;

namespace SahneSenin.ViewModels
{
    public class ChoiceOption : BaseViewModel
    {
        private string _text = string.Empty;
        private string _letter = string.Empty; // A, B, C, D
        private bool _isSelected;
        private bool _isCorrect;

        public string Text
        {
            get => _text;
            set => SetProperty(ref _text, value);
        }

        public string Letter
        {
            get => _letter;
            set => SetProperty(ref _letter, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsCorrect
        {
            get => _isCorrect;
            set => SetProperty(ref _isCorrect, value);
        }

        private bool _isHidden;
        public bool IsHidden
        {
            get => _isHidden;
            set => SetProperty(ref _isHidden, value);
        }
    }
}
