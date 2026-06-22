using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using SahneSenin.ViewModels;

namespace SahneSenin.Services
{
    public class SpeechService : BaseViewModel
    {
        private SpeechRecognitionEngine? _recognitionEngine;
        private string _micStatus = "Mikrofon Pasif";
        private bool _isEngineAvailable;
        private bool _isMicAvailable;
        private bool _isListening;
        
        // Maps the dynamic choice texts to their index (0=A, 1=B, 2=C, 3=D)
        private readonly List<string> _currentSongs = new();

        // Event to notify MainViewModel that a letter option was recognized (A, B, C, or D)
        public event Action<string>? OptionRecognized;

        public string MicStatus
        {
            get => _micStatus;
            set => SetProperty(ref _micStatus, value);
        }

        public bool IsListening
        {
            get => _isListening;
            set => SetProperty(ref _isListening, value);
        }

        public SpeechService()
        {
            InitializeSpeechEngine();
        }

        private void InitializeSpeechEngine()
        {
            try
            {
                // Try creating engine with Turkish culture
                var culture = new CultureInfo("tr-TR");
                _recognitionEngine = new SpeechRecognitionEngine(culture);
                _isEngineAvailable = true;
                MicStatus = "Mikrofon Hazır (TR)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not load Turkish recognizer: {ex.Message}");
                try
                {
                    // Fallback to system default speech recognizer
                    _recognitionEngine = new SpeechRecognitionEngine();
                    _isEngineAvailable = true;
                    MicStatus = $"Mikrofon Hazır (Sistem Dili: {_recognitionEngine.RecognizerInfo.Culture.Name})";
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not load default recognizer: {fallbackEx.Message}");
                    _recognitionEngine = null;
                    _isEngineAvailable = false;
                    MicStatus = "Hata: Windows Ses Tanıma yüklü değil. (Ayarlar'dan Konuşma Tanıma paketi yükleyin)";
                    return;
                }
            }

            if (_recognitionEngine != null)
            {
                _recognitionEngine.SpeechRecognized += RecognitionEngine_SpeechRecognized;
                
                // Configure default audio device input
                try
                {
                    _recognitionEngine.SetInputToDefaultAudioDevice();
                    _isMicAvailable = true;
                }
                catch (Exception micEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Microphone input initialization failed: {micEx.Message}");
                    _isMicAvailable = false;
                    MicStatus = "Hata: Mikrofon bağlanamadı.";
                }
            }
        }

        public void UpdateChoices(List<string> songTitles)
        {
            if (!_isEngineAvailable || _recognitionEngine == null) return;

            try
            {
                _currentSongs.Clear();
                _currentSongs.AddRange(songTitles);

                // Stop active recognition before modifying grammars
                bool wasListening = IsListening;
                if (wasListening)
                {
                    StopListening();
                }

                _recognitionEngine.UnloadAllGrammars();

                // Create a choices set
                var choices = new Choices();

                // 1. Add standard letters
                choices.Add(new string[] { "A", "B", "C", "D" });
                choices.Add(new string[] { "a", "b", "c", "d" });

                // 2. Add Turkish phonetic spelling words
                choices.Add(new string[] { "Adana", "Bursa", "Ceyhan", "Denizli" });
                choices.Add(new string[] { "adana", "bursa", "ceyhan", "denizli" });

                // 3. Add dynamic song titles (only if valid)
                foreach (var title in songTitles)
                {
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        choices.Add(title.Trim());
                    }
                }

                // Construct Grammar
                var gb = new GrammarBuilder();
                gb.Append(choices);
                
                // Set grammar culture to match engine
                gb.Culture = _recognitionEngine.RecognizerInfo.Culture;

                var grammar = new Grammar(gb);
                _recognitionEngine.LoadGrammar(grammar);

                if (wasListening)
                {
                    StartListening();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update speech grammars: {ex.Message}");
            }
        }

        public void StartListening()
        {
            if (!_isEngineAvailable || !_isMicAvailable || _recognitionEngine == null)
            {
                // Re-attempt to connect microphone just in case it was plugged in later
                if (!_isMicAvailable)
                {
                    try
                    {
                        _recognitionEngine?.SetInputToDefaultAudioDevice();
                        _isMicAvailable = true;
                        MicStatus = "Mikrofon Aktif";
                    }
                    catch
                    {
                        MicStatus = "Hata: Mikrofon bulunamadı.";
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (_recognitionEngine == null) return;
            if (IsListening) return;

            try
            {
                _recognitionEngine.RecognizeAsync(RecognizeMode.Multiple);
                IsListening = true;
                MicStatus = "Mikrofon Aktif (Dinliyor...)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start recognition failed: {ex.Message}");
                MicStatus = "Hata: Ses dinleme başlatılamadı.";
            }
        }

        public void StopListening()
        {
            if (!_isEngineAvailable || !_isMicAvailable || _recognitionEngine == null || !IsListening) return;

            try
            {
                _recognitionEngine.RecognizeAsyncStop();
                IsListening = false;
                MicStatus = "Mikrofon Pasif";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop recognition failed: {ex.Message}");
            }
        }

        private void RecognitionEngine_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result == null || e.Result.Confidence < 0.4) return;

            string recognizedText = e.Result.Text.Trim();
            System.Diagnostics.Debug.WriteLine($"Speech recognized: {recognizedText} (Confidence: {e.Result.Confidence})");

            // Evaluate the recognized text and map to choice index (A, B, C, D)
            string matchedLetter = string.Empty;

            // Direct letter matches
            if (string.Equals(recognizedText, "A", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(recognizedText, "Adana", StringComparison.OrdinalIgnoreCase))
            {
                matchedLetter = "A";
            }
            else if (string.Equals(recognizedText, "B", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(recognizedText, "Bursa", StringComparison.OrdinalIgnoreCase))
            {
                matchedLetter = "B";
            }
            else if (string.Equals(recognizedText, "C", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(recognizedText, "Ceyhan", StringComparison.OrdinalIgnoreCase))
            {
                matchedLetter = "C";
            }
            else if (string.Equals(recognizedText, "D", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(recognizedText, "Denizli", StringComparison.OrdinalIgnoreCase))
            {
                matchedLetter = "D";
            }
            else
            {
                // Dynamic song title match
                for (int i = 0; i < _currentSongs.Count; i++)
                {
                    if (string.Equals(_currentSongs[i].Trim(), recognizedText, StringComparison.OrdinalIgnoreCase))
                    {
                        string[] letters = { "A", "B", "C", "D" };
                        matchedLetter = letters[i];
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(matchedLetter))
            {
                // Notify the ViewModel on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OptionRecognized?.Invoke(matchedLetter);
                });
            }
        }
    }
}
