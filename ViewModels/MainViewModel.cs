using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using SahneSenin.Models;
using SahneSenin.Services;

namespace SahneSenin.ViewModels
{
    public enum GameState
    {
        Setup,
        TeacherSelection,
        Game,
        GuessPhase,
        Score
    }

    public class MainViewModel : BaseViewModel
    {
        private readonly DataService _dataService;
        private readonly AudioService _audioService;

        private GameState _currentState = GameState.Setup;
        private ObservableCollection<Teacher> _teachers = new();
        private ObservableCollection<Teacher> _unplayedTeachers = new();
        private Teacher? _currentTeacher;
        private string _secretSongName = string.Empty;
        private string _currentArtist = string.Empty;
        private double _countdownProgress = 10.0;
        private string _countdownText = "10";
        private bool _isConfettiActive = false;
        private bool _isAudioPlaying = false;
        private string _scoreStatusText = string.Empty;
        private bool _isSpinCompleted = false;

        private string _currentSongFile = string.Empty;
        private DispatcherTimer? _guessTimer;
        private double _guessRemainingSeconds = 10.0;
        private string _correctAnswer = string.Empty;
        private string? _selectedAnswer;
        private ObservableCollection<ChoiceOption> _answerChoices = new();

        private int _currentAttempt = 1;
        private readonly List<string> _playedSongsInTurn = new();
        private readonly SpeechService _speechService;

        // Events for UI animation triggers
        public event EventHandler<Teacher>? SpinStarted;
        public event Action? ConfettiTriggered;

        // Commands
        public ICommand LoadCsvCommand { get; }
        public ICommand ResetGameCommand { get; }
        public ICommand SpinCommand { get; }
        public ICommand StartGameCommand { get; }
        public ICommand GradeCommand { get; }
        public ICommand BackToSelectionCommand { get; }
        public ICommand ReplayAudioCommand { get; }
        public ICommand SelectChoiceCommand { get; }
        public ICommand ConfirmAnswerCommand { get; }
        public ICommand GoToGuessPhaseCommand { get; }
        public ICommand NextAttemptCommand { get; }

        public GameState CurrentState
        {
            get => _currentState;
            set => SetProperty(ref _currentState, value);
        }

        public ObservableCollection<Teacher> Teachers
        {
            get => _teachers;
            set => SetProperty(ref _teachers, value);
        }

        public ObservableCollection<Teacher> UnplayedTeachers
        {
            get => _unplayedTeachers;
            set => SetProperty(ref _unplayedTeachers, value);
        }

        public Teacher? CurrentTeacher
        {
            get => _currentTeacher;
            set
            {
                if (SetProperty(ref _currentTeacher, value))
                {
                    OnPropertyChanged(nameof(IsTeacherSelected));
                }
            }
        }

        public bool IsTeacherSelected => CurrentTeacher != null;

        public bool IsSpinCompleted
        {
            get => _isSpinCompleted;
            set => SetProperty(ref _isSpinCompleted, value);
        }

        public string SecretSongName
        {
            get => _secretSongName;
            set => SetProperty(ref _secretSongName, value);
        }

        public string CurrentArtist
        {
            get => _currentArtist;
            set => SetProperty(ref _currentArtist, value);
        }

        public double CountdownProgress
        {
            get => _countdownProgress;
            set => SetProperty(ref _countdownProgress, value);
        }

        public string CountdownText
        {
            get => _countdownText;
            set => SetProperty(ref _countdownText, value);
        }

        public bool IsConfettiActive
        {
            get => _isConfettiActive;
            set => SetProperty(ref _isConfettiActive, value);
        }

        public bool IsAudioPlaying
        {
            get => _isAudioPlaying;
            set => SetProperty(ref _isAudioPlaying, value);
        }

        public string ScoreStatusText
        {
            get => _scoreStatusText;
            set => SetProperty(ref _scoreStatusText, value);
        }

        public string CorrectAnswer
        {
            get => _correctAnswer;
            set => SetProperty(ref _correctAnswer, value);
        }

        public string? SelectedAnswer
        {
            get => _selectedAnswer;
            set => SetProperty(ref _selectedAnswer, value);
        }

        public ObservableCollection<ChoiceOption> AnswerChoices
        {
            get => _answerChoices;
            set => SetProperty(ref _answerChoices, value);
        }

        public int CurrentAttempt
        {
            get => _currentAttempt;
            set
            {
                if (SetProperty(ref _currentAttempt, value))
                {
                    OnPropertyChanged(nameof(IsLastAttempt));
                    OnPropertyChanged(nameof(IsNotLastAttempt));
                    OnPropertyChanged(nameof(NextAttemptButtonText));
                    OnPropertyChanged(nameof(DisplayScoreFooterText));
                }
            }
        }

        public bool IsLastAttempt => CurrentAttempt >= 3;
        public bool IsNotLastAttempt => CurrentAttempt < 3;
        public string NextAttemptButtonText => $"Sonraki Şarkıya Geç ({CurrentAttempt + 1}. Hak)";
        public string DisplayScoreFooterText => IsNotLastAttempt 
            ? "Sonraki şarkı için sunucu komutu bekleniyor..." 
            : "Sonraki yarışmacı seçimi için sunucu komutu bekleniyor...";

        public SpeechService SpeechService => _speechService;

        public MainViewModel(DataService dataService, AudioService audioService)
        {
            _dataService = dataService;
            _audioService = audioService;

            _speechService = new SpeechService();
            _speechService.OptionRecognized += SpeechService_OptionRecognized;

            // Wire up services
            _audioService.Tick += AudioService_Tick;
            _audioService.PlaybackFinished += AudioService_PlaybackFinished;

            _guessTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _guessTimer.Tick += GuessTimer_Tick;

            // Wire up commands
            LoadCsvCommand = new RelayCommand(ExecuteLoadCsv);
            ResetGameCommand = new RelayCommand(ExecuteResetGame);
            SpinCommand = new RelayCommand(ExecuteSpin, CanSpin);
            StartGameCommand = new RelayCommand(ExecuteStartGame, CanStartGame);
            GradeCommand = new RelayCommand(ExecuteGrade);
            BackToSelectionCommand = new RelayCommand(ExecuteBackToSelection);
            ReplayAudioCommand = new RelayCommand(ExecuteReplayAudio);
            SelectChoiceCommand = new RelayCommand(ExecuteSelectChoice);
            ConfirmAnswerCommand = new RelayCommand(ExecuteConfirmAnswer, CanConfirmAnswer);
            GoToGuessPhaseCommand = new RelayCommand(ExecuteGoToGuessPhase, CanGoToGuessPhase);
            NextAttemptCommand = new RelayCommand(ExecuteNextAttempt);

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            var data = _dataService.LoadData();
            
            Teachers = new ObservableCollection<Teacher>(data.Teachers);
            UpdateUnplayedTeachers();

            if (Teachers.Count > 0)
            {
                CurrentState = GameState.TeacherSelection;
            }
            else
            {
                CurrentState = GameState.Setup;
            }
        }

        private void UpdateUnplayedTeachers()
        {
            var unplayed = Teachers.Where(t => !t.HasPlayed).ToList();
            UnplayedTeachers = new ObservableCollection<Teacher>(unplayed);
        }

        private void ExecuteLoadCsv()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV Dosyaları (*.csv)|*.csv|Tüm Dosyalar (*.*)|*.*",
                Title = "Öğretmen Listesi Seç"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _dataService.ImportFromCsv(openFileDialog.FileName);
                LoadInitialData();
            }
        }

        private void ExecuteResetGame()
        {
            var result = System.Windows.MessageBox.Show(
                "Tüm oyunu sıfırlamak ve puanları temizlemek istediğinize emin misiniz?", 
                "Oyunu Sıfırla", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _guessTimer?.Stop();
                _speechService.StopListening();
                var data = _dataService.LoadData();
                foreach (var teacher in data.Teachers)
                {
                    teacher.Score = 0;
                    teacher.HasPlayed = false;
                }
                _dataService.SaveData(data);
                LoadInitialData();
            }
        }

        private bool CanSpin()
        {
            return UnplayedTeachers.Count > 0 && CurrentState == GameState.TeacherSelection;
        }

        private void ExecuteSpin()
        {
            if (UnplayedTeachers.Count == 0) return;

            // Pick a random teacher
            var rand = new Random();
            var chosenTeacher = UnplayedTeachers[rand.Next(UnplayedTeachers.Count)];
            
            // Set current teacher
            CurrentTeacher = chosenTeacher;
            IsSpinCompleted = false;
            CurrentAttempt = 1;
            _playedSongsInTurn.Clear();

            // Mark as played immediately in local memory so they won't be spun again
            var match = Teachers.FirstOrDefault(t => t.Name == chosenTeacher.Name);
            if (match != null)
            {
                match.HasPlayed = true;
                // Force UI update by resetting the item at its index
                int idx = Teachers.IndexOf(match);
                if (idx >= 0)
                {
                    Teachers[idx] = match;
                }
            }

            // Save state
            SaveGameData();
            UpdateUnplayedTeachers();

            // Trigger visual spin in DisplayWindow
            SpinStarted?.Invoke(this, chosenTeacher);
        }

        private bool CanStartGame()
        {
            return IsTeacherSelected && IsSpinCompleted;
        }

        private void ExecuteStartGame()
        {
            StartGameRound();
        }

        private void StartGameRound()
        {
            if (CurrentTeacher == null) return;

            // Reset countdown values
            CountdownProgress = 10.0;
            CountdownText = "10";
            IsConfettiActive = false;

            // Find songs from SelectedArtists
            var data = _dataService.LoadData();
            var availableSongs = new List<string>();

            foreach (var artist in CurrentTeacher.SelectedArtists)
            {
                if (data.Artists.TryGetValue(artist, out var songs))
                {
                    foreach (var song in songs)
                    {
                        availableSongs.Add(song);
                    }
                }
            }

            // Fallback: If no songs found for the teacher's selected artists, 
            // check if there are ANY songs in the MusicPool folder at all
            if (availableSongs.Count == 0)
            {
                // Scan all available MP3s
                var allScan = _dataService.ScanMusicPool();
                foreach (var artistSongs in allScan.Values)
                {
                    availableSongs.AddRange(artistSongs);
                }
            }

            // Filter out songs that have already been played during this teacher's turn
            var remainingSongs = availableSongs.Where(s => !_playedSongsInTurn.Contains(s)).ToList();

            // If we ran out of songs for their selected artists, fallback to other songs in the pool (excluding played ones)
            if (remainingSongs.Count == 0)
            {
                var allScan = _dataService.ScanMusicPool();
                var allSongs = new List<string>();
                foreach (var artistSongs in allScan.Values)
                {
                    allSongs.AddRange(artistSongs);
                }
                remainingSongs = allSongs.Where(s => !_playedSongsInTurn.Contains(s)).ToList();
            }

            // Reset played songs list if we've played every song in the pool (safeguard)
            if (remainingSongs.Count == 0)
            {
                _playedSongsInTurn.Clear();
                remainingSongs = availableSongs;
                if (remainingSongs.Count == 0)
                {
                    var allScan = _dataService.ScanMusicPool();
                    foreach (var artistSongs in allScan.Values)
                    {
                        remainingSongs.AddRange(artistSongs);
                    }
                }
            }

            if (remainingSongs.Count > 0)
            {
                var rand = new Random();
                string selectedSongFile = remainingSongs[rand.Next(remainingSongs.Count)];
                _playedSongsInTurn.Add(selectedSongFile);
                
                // Parse artist and secret song name for display to Host
                string artistName = "Bilinmeyen Sanatçı";
                string songTitle = Path.GetFileNameWithoutExtension(selectedSongFile);

                // If in a subfolder, use the subfolder name as the artist name
                string? directoryName = Path.GetDirectoryName(selectedSongFile);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    artistName = directoryName;
                }

                // If filename contains _ or -, split it
                string[] parts = songTitle.Split(new[] { '_', '-' }, 2);
                if (parts.Length == 2)
                {
                    artistName = parts[0].Trim();
                    songTitle = parts[1].Trim();
                }

                CurrentArtist = artistName;
                SecretSongName = $"{artistName} - {songTitle}";
                
                _currentSongFile = selectedSongFile;
                AnswerChoices.Clear();
                SelectedAnswer = null;

                CurrentState = GameState.Game;
                IsAudioPlaying = true;

                // Play music
                _audioService.PlaySong(selectedSongFile);
            }
            else
            {
                // No music files found!
                SecretSongName = "HATA: MusicPool klasöründe çalınacak müzik bulunamadı!";
                CurrentArtist = "";
                CurrentState = GameState.Game;
                IsAudioPlaying = false;
                
                // Jump to score state or show dialog
                System.Windows.MessageBox.Show(
                    "MusicPool klasöründe hiçbir MP3 dosyası bulunamadı. Lütfen şarkı ekleyip tekrar deneyin.", 
                    "Müzik Bulunamadı", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void AudioService_Tick(double elapsedSeconds)
        {
            double remaining = Math.Max(0.0, 10.0 - elapsedSeconds);
            CountdownProgress = remaining;
            
            // Round up for visual timer text, but show 0 if finished
            int secondsVal = (int)Math.Ceiling(remaining);
            CountdownText = secondsVal.ToString();
        }

        private void AudioService_PlaybackFinished()
        {
            IsAudioPlaying = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StartGuessPhase();
            });
        }

        private void ExecuteReplayAudio()
        {
            // Stop and restart current song selection
            _audioService.Stop();
            StartGameRound();
        }

        private void ExecuteGrade(object? parameter)
        {
            if (CurrentTeacher == null || parameter == null) return;

            string gradeType = parameter.ToString() ?? "";
            int pointsToAdd = 0;

            _audioService.Stop();
            _guessTimer?.Stop();
            _speechService.StopListening();

            if (gradeType == "Correct")
            {
                pointsToAdd = 10;
                ScoreStatusText = "Tebrikler! Bildiniz.";
                _audioService.PlaySfx("correct");
            }
            else if (gradeType == "Bonus")
            {
                pointsToAdd = 15;
                ScoreStatusText = "Harika Söyledi! Bonus Puan.";
                IsConfettiActive = true;
                _audioService.PlaySfx("confetti");
                ConfettiTriggered?.Invoke();
            }
            else // Wrong
            {
                pointsToAdd = 0;
                ScoreStatusText = "Bilemedi!";
                _audioService.PlaySfx("wrong");
            }

            // Update score
            var match = Teachers.FirstOrDefault(t => t.Name == CurrentTeacher.Name);
            if (match != null)
            {
                match.Score += pointsToAdd;
                // Force UI update by resetting the item at its index
                int idx = Teachers.IndexOf(match);
                if (idx >= 0)
                {
                    Teachers[idx] = match;
                }
            }

            SaveGameData();
            CurrentState = GameState.Score;
        }

        private void ExecuteBackToSelection()
        {
            IsConfettiActive = false;
            CurrentTeacher = null;
            SecretSongName = string.Empty;
            CurrentArtist = string.Empty;
            IsSpinCompleted = false;
            _guessTimer?.Stop();
            _speechService.StopListening();
            CurrentState = GameState.TeacherSelection;
        }

        private void ExecuteSelectChoice(object? parameter)
        {
            if (parameter is ChoiceOption selectedChoice)
            {
                foreach (var choice in AnswerChoices)
                {
                    choice.IsSelected = (choice == selectedChoice);
                }
                SelectedAnswer = selectedChoice.Text;
            }
        }

        private bool CanConfirmAnswer()
        {
            return SelectedAnswer != null;
        }

        private void ExecuteConfirmAnswer()
        {
            _guessTimer?.Stop();
            var selected = AnswerChoices.FirstOrDefault(c => c.IsSelected);
            if (selected != null)
            {
                if (selected.IsCorrect)
                {
                    ExecuteGrade("Correct");
                }
                else
                {
                    ExecuteGrade("Wrong");
                }
            }
        }

        private bool CanGoToGuessPhase()
        {
            return CurrentState == GameState.Game && IsAudioPlaying;
        }

        private void ExecuteGoToGuessPhase()
        {
            StartGuessPhase();
        }

        public void StartGuessPhase()
        {
            _audioService.Stop();
            IsAudioPlaying = false;

            GenerateChoices(_currentSongFile);

            SelectedAnswer = null;
            _guessRemainingSeconds = 10.0;
            CountdownProgress = 10.0;
            CountdownText = "10";

            // Update speech grammar dynamically for this round
            var songNames = AnswerChoices.Select(c => c.Text).ToList();
            _speechService.UpdateChoices(songNames);
            _speechService.StartListening();

            CurrentState = GameState.GuessPhase;
            _guessTimer?.Start();
        }

        private void GenerateChoices(string correctFile)
        {
            AnswerChoices.Clear();
            CorrectAnswer = string.Empty;

            string correctArtist = CurrentArtist;
            string correctSongTitle = Path.GetFileNameWithoutExtension(correctFile);
            string[] parts = correctSongTitle.Split(new[] { '_', '-' }, 2);
            if (parts.Length == 2)
            {
                correctSongTitle = parts[1].Trim();
            }
            else
            {
                correctSongTitle = correctSongTitle.Trim();
            }

            CorrectAnswer = correctSongTitle;

            var data = _dataService.LoadData();
            var sameArtistSongs = new List<string>();
            var otherArtistSongs = new List<string>();

            foreach (var kvp in data.Artists)
            {
                string artistName = kvp.Key;
                foreach (var file in kvp.Value)
                {
                    string songTitle = Path.GetFileNameWithoutExtension(file);
                    string[] p = songTitle.Split(new[] { '_', '-' }, 2);
                    string title = p.Length == 2 ? p[1].Trim() : songTitle.Trim();

                    if (string.Equals(title, correctSongTitle, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.Equals(artistName, correctArtist, StringComparison.OrdinalIgnoreCase))
                    {
                        sameArtistSongs.Add(title);
                    }
                    else
                    {
                        otherArtistSongs.Add(title);
                    }
                }
            }

            var rand = new Random();
            sameArtistSongs = sameArtistSongs.OrderBy(x => rand.Next()).ToList();
            otherArtistSongs = otherArtistSongs.OrderBy(x => rand.Next()).ToList();

            var wrongChoices = new List<string>();
            wrongChoices.AddRange(sameArtistSongs.Take(3));

            // If we don't have enough same artist songs, fill with placeholder song titles using the correct artist name
            if (wrongChoices.Count < 3)
            {
                int needed = 3 - wrongChoices.Count;
                for (int i = 0; i < needed; i++)
                {
                    wrongChoices.Add($"{correctArtist} - Diğer Şarkı {i + 2}");
                }
            }

            var allChoices = new List<string> { correctSongTitle };
            allChoices.AddRange(wrongChoices.Take(3));
            allChoices = allChoices.OrderBy(x => rand.Next()).ToList();

            string[] letters = { "A", "B", "C", "D" };
            for (int i = 0; i < allChoices.Count; i++)
            {
                var option = new ChoiceOption
                {
                    Letter = letters[i],
                    Text = allChoices[i],
                    IsCorrect = string.Equals(allChoices[i], correctSongTitle, StringComparison.OrdinalIgnoreCase),
                    IsSelected = false
                };
                AnswerChoices.Add(option);
            }
        }

        private void GuessTimer_Tick(object? sender, EventArgs e)
        {
            _guessRemainingSeconds = Math.Max(0.0, _guessRemainingSeconds - 0.1);
            CountdownProgress = _guessRemainingSeconds;
            
            int secondsVal = (int)Math.Ceiling(_guessRemainingSeconds);
            CountdownText = secondsVal.ToString();

            if (_guessRemainingSeconds <= 0.0)
            {
                _guessTimer?.Stop();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var selected = AnswerChoices.FirstOrDefault(c => c.IsSelected);
                    if (selected != null && selected.IsCorrect)
                    {
                        ExecuteGrade("Correct");
                    }
                    else
                    {
                        ExecuteGrade("Wrong");
                    }
                });
            }
        }

        private void ExecuteNextAttempt()
        {
            CurrentAttempt++;
            StartGameRound();
        }

        private void SpeechService_OptionRecognized(string letter)
        {
            var option = AnswerChoices.FirstOrDefault(c => string.Equals(c.Letter, letter, StringComparison.OrdinalIgnoreCase));
            if (option != null)
            {
                ExecuteSelectChoice(option);
            }
        }

        private void SaveGameData()
        {
            var data = new GameData
            {
                Teachers = Teachers.ToList()
            };
            _dataService.SaveData(data);
        }
    }
}
