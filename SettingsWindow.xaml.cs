using System;
using System.Windows;

namespace SahneSenin
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var settings = AppSettings.Load();

                // Normal Mode
                TxtNormalCorrect.Text = settings.NormalCorrectPoints.ToString();
                TxtNormalBonus.Text = settings.NormalBonusPoints.ToString();
                TxtNormalWrong.Text = settings.NormalWrongPoints.ToString();

                // Pool Mode
                TxtPoolCorrect.Text = settings.PoolCorrectPoints.ToString();
                TxtPoolBonus.Text = settings.PoolBonusPoints.ToString();
                TxtPoolWrong.Text = settings.PoolWrongPoints.ToString();

                // Extra Mode
                TxtExtraCorrect.Text = settings.ExtraCorrectPoints.ToString();
                TxtExtraBonus.Text = settings.ExtraBonusPoints.ToString();
                TxtExtraWrong.Text = settings.ExtraWrongPoints.ToString();

                // Risk Mode
                TxtRiskMultiplier.Text = settings.RiskMultiplier.ToString();
                TxtRiskWrong.Text = settings.RiskWrongPoints.ToString();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ayarlar yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate inputs
                if (!int.TryParse(TxtNormalCorrect.Text, out int normalCorrect) ||
                    !int.TryParse(TxtNormalBonus.Text, out int normalBonus) ||
                    !int.TryParse(TxtNormalWrong.Text, out int normalWrong) ||
                    !int.TryParse(TxtPoolCorrect.Text, out int poolCorrect) ||
                    !int.TryParse(TxtPoolBonus.Text, out int poolBonus) ||
                    !int.TryParse(TxtPoolWrong.Text, out int poolWrong) ||
                    !int.TryParse(TxtExtraCorrect.Text, out int extraCorrect) ||
                    !int.TryParse(TxtExtraBonus.Text, out int extraBonus) ||
                    !int.TryParse(TxtExtraWrong.Text, out int extraWrong) ||
                    !int.TryParse(TxtRiskMultiplier.Text, out int riskMultiplier) ||
                    !int.TryParse(TxtRiskWrong.Text, out int riskWrong))
                {
                    System.Windows.MessageBox.Show("Lütfen tüm alanlara geçerli tam sayılar girin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Load, modify and save
                var settings = AppSettings.Load();

                settings.NormalCorrectPoints = normalCorrect;
                settings.NormalBonusPoints = normalBonus;
                settings.NormalWrongPoints = normalWrong;

                settings.PoolCorrectPoints = poolCorrect;
                settings.PoolBonusPoints = poolBonus;
                settings.PoolWrongPoints = poolWrong;

                settings.ExtraCorrectPoints = extraCorrect;
                settings.ExtraBonusPoints = extraBonus;
                settings.ExtraWrongPoints = extraWrong;

                settings.RiskMultiplier = Math.Max(1, riskMultiplier);
                settings.RiskWrongPoints = riskWrong;

                settings.Save();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ayarlar kaydedilirken hata oluştu: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
