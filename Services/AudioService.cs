using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;

namespace SahneSenin.Services
{
    public class AudioService
    {
        private readonly MediaPlayer _musicPlayer;
        private readonly MediaPlayer _sfxPlayer;
        private readonly string _musicPoolPath;
        private readonly DispatcherTimer _timer;
        private readonly System.Collections.Generic.Dictionary<string, MediaPlayer> _cachedSfx = new(StringComparer.OrdinalIgnoreCase);
        
        private double _playStartTimeSeconds;
        private double _playDurationMs = 10000; // 10 seconds total
        private double _fadeDurationMs = 1500;  // 1.5 seconds fade-out
        private double _elapsedMs = 0;

        public event Action<double>? Tick; // Notifies remaining time/progress (0 to 10s)
        public event Action? PlaybackFinished; // Notifies when the 10 seconds are up

        public AudioService(DataService dataService)
        {
            _musicPlayer = new MediaPlayer();
            _sfxPlayer = new MediaPlayer();
            _musicPoolPath = dataService.GetMusicPoolDirectory();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _timer.Tick += Timer_Tick;

            _musicPlayer.MediaOpened += MusicPlayer_MediaOpened;
            _musicPlayer.MediaFailed += MusicPlayer_MediaFailed;

            // Preload common sound effects to prevent file-opening latency
            PreloadSfx("tick");
            PreloadSfx("correct");
            PreloadSfx("wrong");
            PreloadSfx("confetti");
            PreloadSfx("applause");
        }

        private void PreloadSfx(string sfxName)
        {
            try
            {
                string sfxKey = sfxName.ToLower();
                string mp3Path = Path.Combine(_musicPoolPath, $"{sfxName}.mp3");
                string wavPath = Path.Combine(_musicPoolPath, $"{sfxName}.wav");
                
                string? targetPath = null;
                if (File.Exists(mp3Path)) targetPath = mp3Path;
                else if (File.Exists(wavPath)) targetPath = wavPath;

                if (targetPath != null)
                {
                    var player = new MediaPlayer();
                    player.Open(new Uri(targetPath));
                    player.Volume = 1.0;
                    // Play and immediately stop/pause to buffer the file
                    player.Play();
                    player.Stop();
                    _cachedSfx[sfxKey] = player;
                }
            }
            catch
            {
                // Ignore preloading failures
            }
        }

        public void PlaySong(string fileName)
        {
            Stop();

            string filePath = Path.Combine(_musicPoolPath, fileName);
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"Audio file not found: {filePath}");
                return;
            }

            _elapsedMs = 0;
            _musicPlayer.Volume = 1.0;
            
            // Open the file. The actual playing start position will be calculated in MediaOpened event.
            _musicPlayer.Open(new Uri(filePath));
        }

        private void MusicPlayer_MediaOpened(object? sender, EventArgs e)
        {
            double duration = 0;
            if (_musicPlayer.NaturalDuration.HasTimeSpan)
            {
                duration = _musicPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            }

            // Pick a random starting point. Avoid picking too close to the end (leave at least play duration)
            double maxStart = Math.Max(0.0, duration - (_playDurationMs / 1000.0) - 2.0);
            var rand = new Random();
            _playStartTimeSeconds = rand.NextDouble() * maxStart;

            _musicPlayer.Position = TimeSpan.FromSeconds(_playStartTimeSeconds);
            _musicPlayer.Play();
            _timer.Start();
        }

        private void MusicPlayer_MediaFailed(object? sender, ExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Music playback failed: {e.ErrorException.Message}");
            _timer.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _elapsedMs += 50;
            
            // Invoke progression tick
            Tick?.Invoke(_elapsedMs / 1000.0);

            // Handle fade out in the last 1.5 seconds
            double fadeStartMs = _playDurationMs - _fadeDurationMs;
            if (_elapsedMs >= fadeStartMs)
            {
                double fadeProgress = (_elapsedMs - fadeStartMs) / _fadeDurationMs;
                _musicPlayer.Volume = Math.Max(0.0, 1.0 - fadeProgress);
            }

            // Stop at 10 seconds
            if (_elapsedMs >= _playDurationMs)
            {
                Stop();
                PlaybackFinished?.Invoke();
            }
        }

        public void SetPlayDuration(double seconds)
        {
            _playDurationMs = seconds * 1000.0;
        }

        public void Stop()
        {
            _timer.Stop();
            _musicPlayer.Stop();
            _musicPlayer.Close();
        }

        public void PlaySfx(string sfxName)
        {
            string sfxKey = sfxName.ToLower();

            if (_cachedSfx.TryGetValue(sfxKey, out var player))
            {
                try
                {
                    player.Stop();
                    player.Position = TimeSpan.Zero;
                    player.Play();
                    return;
                }
                catch
                {
                    // Fallback to reload
                }
            }

            // Look for custom sfxName.mp3 or sfxName.wav in MusicPool
            string mp3Path = Path.Combine(_musicPoolPath, $"{sfxName}.mp3");
            string wavPath = Path.Combine(_musicPoolPath, $"{sfxName}.wav");
            
            string? targetPath = null;
            if (File.Exists(mp3Path)) targetPath = mp3Path;
            else if (File.Exists(wavPath)) targetPath = wavPath;

            if (targetPath != null)
            {
                try
                {
                    var newPlayer = new MediaPlayer();
                    newPlayer.Open(new Uri(targetPath));
                    newPlayer.Volume = 1.0;
                    
                    _cachedSfx[sfxKey] = newPlayer;
                    newPlayer.Play();
                    return;
                }
                catch
                {
                    // Fallback to system sounds
                }
            }

            // System sound fallbacks if file not downloaded yet
            switch (sfxKey)
            {
                case "correct":
                case "applause":
                    System.Media.SystemSounds.Asterisk.Play();
                    break;
                case "wrong":
                    System.Media.SystemSounds.Hand.Play();
                    break;
                case "confetti":
                    System.Media.SystemSounds.Question.Play();
                    break;
                case "tick":
                    System.Media.SystemSounds.Beep.Play();
                    break;
            }
        }
    }
}
