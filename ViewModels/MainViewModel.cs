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

        private bool _isLeaderboardVisible;
        private bool _useAllArtistsPool;
        private bool _isRiskActive;
        private int _currentStreak = 0;
        private bool _isTimerCritical = false;
        private int _lastTickSeconds = -1;

        // Events for UI animation triggers
        public event EventHandler<Teacher>? SpinStarted;
        public event Action<double>? ConfettiTriggered;

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
        public ICommand ToggleLeaderboardCommand { get; }
        public ICommand UseJokerCommand { get; }
        public ICommand EndGameCommand { get; }
        public ICommand CloseCelebrationCommand { get; }

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
                    OnPropertyChanged(nameof(AvailableExtraArtists));
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

        public bool IsLeaderboardVisible
        {
            get => _isLeaderboardVisible;
            set
            {
                if (SetProperty(ref _isLeaderboardVisible, value))
                {
                    OnPropertyChanged(nameof(LeaderboardButtonText));
                    OnPropertyChanged(nameof(LeaderboardTeachers));
                }
            }
        }

        public string LeaderboardButtonText => IsLeaderboardVisible ? "Skor Tablosunu Gizle" : "Skor Tablosunu Göster";

        public List<Teacher> LeaderboardTeachers => Teachers
            .Where(t => t.HasPlayed)
            .OrderByDescending(t => t.Score)
            .ToList();

        public bool UseAllArtistsPool
        {
            get => _useAllArtistsPool;
            set => SetProperty(ref _useAllArtistsPool, value);
        }

        public bool IsRiskActive
        {
            get => _isRiskActive;
            set => SetProperty(ref _isRiskActive, value);
        }

        public int CurrentStreak
        {
            get => _currentStreak;
            set
            {
                if (SetProperty(ref _currentStreak, value))
                {
                    OnPropertyChanged(nameof(IsStreakActive));
                }
            }
        }

        public bool IsStreakActive => CurrentStreak >= 2;

        public bool IsTimerCritical
        {
            get => _isTimerCritical;
            private set => SetProperty(ref _isTimerCritical, value);
        }

        private double _maxCountdown = 10.0;
        public double MaxCountdown
        {
            get => _maxCountdown;
            set => SetProperty(ref _maxCountdown, value);
        }

        private string? _extraSelectedArtist;
        public string? ExtraSelectedArtist
        {
            get => _extraSelectedArtist;
            set => SetProperty(ref _extraSelectedArtist, value);
        }

        private bool _isWinnerCelebrationVisible;
        public bool IsWinnerCelebrationVisible
        {
            get => _isWinnerCelebrationVisible;
            set => SetProperty(ref _isWinnerCelebrationVisible, value);
        }

        private Teacher? _finalWinnerTeacher;
        public Teacher? FinalWinnerTeacher
        {
            get => _finalWinnerTeacher;
            set => SetProperty(ref _finalWinnerTeacher, value);
        }

        public List<string> AvailableExtraArtists
        {
            get
            {
                if (CurrentTeacher == null) return new List<string>();
                var data = _dataService.LoadData();
                return data.Artists.Keys
                    .Where(a => !CurrentTeacher.SelectedArtists.Contains(a))
                    .OrderBy(a => a)
                    .ToList();
            }
        }

        public bool CanUseJoker => CurrentTeacher != null && !CurrentTeacher.IsJokerUsed && CurrentState == GameState.GuessPhase;

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
            ToggleLeaderboardCommand = new RelayCommand(ExecuteToggleLeaderboard);
            UseJokerCommand = new RelayCommand(ExecuteUseJoker, CanUseJokerCommandExecution);
            EndGameCommand = new RelayCommand(ExecuteEndGame, CanEndGame);
            CloseCelebrationCommand = new RelayCommand(ExecuteCloseCelebration);

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            var data = _dataService.LoadData();
            
            // Resolve photo paths for each teacher
            string photosDir = _dataService.GetTeacherPhotosDirectory();
            foreach (var teacher in data.Teachers)
            {
                teacher.PhotoPath = ResolveTeacherPhoto(photosDir, teacher.Name);
            }

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

        private string? ResolveTeacherPhoto(string photosDir, string teacherName)
        {
            if (!Directory.Exists(photosDir)) return null;

            // Look for teacherName.png, teacherName.jpg, teacherName.jpeg
            string[] extensions = { ".png", ".jpg", ".jpeg" };
            foreach (var ext in extensions)
            {
                // Replace invalid path characters just in case
                string safeName = string.Join("_", teacherName.Split(Path.GetInvalidFileNameChars()));
                string path = Path.Combine(photosDir, safeName + ext);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
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
                    teacher.IsJokerUsed = false;
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
            
            // Reset gameplay options
            IsRiskActive = false;
            UseAllArtistsPool = false;
            CurrentStreak = 0;
            ExtraSelectedArtist = null;
            OnPropertyChanged(nameof(AvailableExtraArtists));

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

            // Load settings and apply dynamically
            var settings = AppSettings.Load();
            _audioService.SetPlayDuration(settings.ListeningDuration);
            MaxCountdown = settings.ListeningDuration;
            CountdownProgress = settings.ListeningDuration;
            CountdownText = settings.ListeningDuration.ToString();
            IsConfettiActive = false;

            // Find songs from SelectedArtists or entire pool depending on choice
            var data = _dataService.LoadData();
            var availableSongs = new List<string>();

            if (UseAllArtistsPool)
            {
                var allScan = _dataService.ScanMusicPool();
                foreach (var artistSongs in allScan.Values)
                {
                    availableSongs.AddRange(artistSongs);
                }
            }
            else
            {
                bool hasGeneral = CurrentTeacher.SelectedArtists.Any(a => string.Equals(a, "Genel", StringComparison.OrdinalIgnoreCase));
                if (hasGeneral)
                {
                    var allScan = _dataService.ScanMusicPool();
                    foreach (var artistSongs in allScan.Values)
                    {
                        availableSongs.AddRange(artistSongs);
                    }
                }
                else
                {
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
                }

                // Add extra selected artist songs if set
                if (!string.IsNullOrEmpty(ExtraSelectedArtist))
                {
                    if (data.Artists.TryGetValue(ExtraSelectedArtist, out var extraSongs))
                    {
                        foreach (var song in extraSongs)
                        {
                            availableSongs.Add(song);
                        }
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
            var settings = AppSettings.Load();
            double remaining = Math.Max(0.0, settings.ListeningDuration - elapsedSeconds);
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
            IsTimerCritical = false;
            _speechService.StopListening();

            if (gradeType == "Correct")
            {
                int baseCorrect = UseAllArtistsPool ? 20 : 10;
                if (!string.IsNullOrEmpty(ExtraSelectedArtist))
                {
                    baseCorrect = Math.Max(1, baseCorrect - 3); // Penalty for choosing 4th (extra) artist
                }
                pointsToAdd = IsRiskActive ? baseCorrect * 2 : baseCorrect;
                ScoreStatusText = IsRiskActive ? $"RİSK TUTTU! Bildiniz (+{pointsToAdd})." : $"Tebrikler! Bildiniz (+{pointsToAdd}).";
                _audioService.PlaySfx("correct");
                CurrentStreak++;
            }
            else if (gradeType == "Bonus")
            {
                int baseBonus = UseAllArtistsPool ? 40 : 15;
                if (!string.IsNullOrEmpty(ExtraSelectedArtist))
                {
                    baseBonus = Math.Max(1, baseBonus - 3); // Penalty for choosing 4th (extra) artist
                }
                pointsToAdd = baseBonus;
                ScoreStatusText = $"Harika Söyledi! Bonus Puan (+{pointsToAdd}).";
                IsConfettiActive = true;
                _audioService.PlaySfx("confetti");
                ConfettiTriggered?.Invoke(7.0); // 7 seconds for standard bonus confetti
                CurrentStreak++;
            }
            else // Wrong
            {
                pointsToAdd = IsRiskActive ? -5 : 0;
                ScoreStatusText = IsRiskActive ? $"RİSK KAYBEDİLDİ! Bilemedi ({pointsToAdd})." : "Bilemedi!";
                _audioService.PlaySfx("wrong");
                CurrentStreak = 0;
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
            IsRiskActive = false;
            UseAllArtistsPool = false;
            CurrentStreak = 0;
            ExtraSelectedArtist = null;
            _guessTimer?.Stop();
            IsTimerCritical = false;
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

            var settings = AppSettings.Load();
            SelectedAnswer = null;
            _guessRemainingSeconds = settings.GuessingDuration;
            MaxCountdown = settings.GuessingDuration;
            CountdownProgress = settings.GuessingDuration;
            CountdownText = settings.GuessingDuration.ToString();
            _lastTickSeconds = -1;
            IsTimerCritical = false;

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
            
            // Determine active artists for the current teacher's round
            var activeArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool isGeneralPool = UseAllArtistsPool || (CurrentTeacher != null && CurrentTeacher.SelectedArtists.Any(a => string.Equals(a, "Genel", StringComparison.OrdinalIgnoreCase)));
            
            if (CurrentTeacher != null && !isGeneralPool)
            {
                foreach (var a in CurrentTeacher.SelectedArtists)
                {
                    activeArtists.Add(a);
                }
                if (!string.IsNullOrEmpty(ExtraSelectedArtist))
                {
                    activeArtists.Add(ExtraSelectedArtist);
                }
            }

            var candidateSongs = new List<string>();
            var fallbackSongs = new List<string>();

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

                    if (isGeneralPool || activeArtists.Contains(artistName))
                    {
                        candidateSongs.Add(title);
                    }
                    else
                    {
                        fallbackSongs.Add(title);
                    }
                }
            }

            var rand = new Random();
            candidateSongs = candidateSongs.OrderBy(x => rand.Next()).ToList();
            fallbackSongs = fallbackSongs.OrderBy(x => rand.Next()).ToList();

            var wrongChoices = new List<string>();
            wrongChoices.AddRange(candidateSongs.Take(3));

            // If we don't have enough songs from their active artists, use other artists' songs
            if (wrongChoices.Count < 3)
            {
                int needed = 3 - wrongChoices.Count;
                wrongChoices.AddRange(fallbackSongs.Take(needed));
            }

            // Fallback placeholder if archive is extremely small
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
            
            IsTimerCritical = _guessRemainingSeconds <= 3.0 && _guessRemainingSeconds > 0.0;

            int secondsVal = (int)Math.Ceiling(_guessRemainingSeconds);
            if (secondsVal != _lastTickSeconds)
            {
                _lastTickSeconds = secondsVal;
                CountdownText = secondsVal.ToString();
                
                if (secondsVal <= 3 && secondsVal > 0)
                {
                    _audioService.PlaySfx("tick");
                }
            }

            if (_guessRemainingSeconds <= 0.0)
            {
                _guessTimer?.Stop();
                IsTimerCritical = false;
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

        private void ExecuteToggleLeaderboard()
        {
            IsLeaderboardVisible = !IsLeaderboardVisible;
        }

        private bool CanUseJokerCommandExecution()
        {
            return CanUseJoker;
        }

        private void ExecuteUseJoker()
        {
            if (CurrentTeacher == null || CurrentTeacher.IsJokerUsed || CurrentState != GameState.GuessPhase) return;

            CurrentTeacher.IsJokerUsed = true;
            SaveGameData();

            var wrongChoices = AnswerChoices.Where(c => !c.IsCorrect).ToList();
            if (wrongChoices.Count >= 2)
            {
                var rand = new Random();
                var toHide = wrongChoices.OrderBy(x => rand.Next()).Take(2).ToList();
                foreach (var choice in toHide)
                {
                    choice.IsHidden = true;
                }
            }

            // Exclude the hidden choices from speech recognition
            var remainingChoices = AnswerChoices.Where(c => !c.IsHidden).Select(c => c.Text).ToList();
            _speechService.UpdateChoices(remainingChoices);

            OnPropertyChanged(nameof(CanUseJoker));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private bool CanEndGame()
        {
            return CurrentState != GameState.Setup && Teachers.Any(t => t.HasPlayed);
        }

        private void ExecuteEndGame()
        {
            var result = System.Windows.MessageBox.Show(
                "Yarışmayı şimdi sonlandırmak ve kazananı ilan etmek istiyor musunuz?", 
                "Yarışmayı Bitir", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _audioService.Stop();
                _guessTimer?.Stop();
                IsTimerCritical = false;
                _speechService.StopListening();

                var winner = Teachers.Where(t => t.HasPlayed).OrderByDescending(t => t.Score).FirstOrDefault();
                if (winner != null)
                {
                    FinalWinnerTeacher = winner;
                    IsWinnerCelebrationVisible = true;
                    IsLeaderboardVisible = false;

                    IsConfettiActive = true;
                    _audioService.PlaySfx("applause");
                    ConfettiTriggered?.Invoke(25.0); // 25 seconds confetti for the final winner!

                    ScoreStatusText = $"Yarışma Bitti! Şampiyon: {winner.Name} ({winner.Score} Puan)!";
                }
                else
                {
                    IsLeaderboardVisible = true;
                }
            }
        }

        private void ExecuteCloseCelebration()
        {
            IsWinnerCelebrationVisible = false;
            IsLeaderboardVisible = true;
        }
    }
}
