using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;

namespace Negotchi
{
    // Read only the desktop Spotify playback flag. No account, track metadata or
    // playback commands are needed for the pet's ambient animation.
    internal sealed class SpotifyPlaybackMonitor : IDisposable
    {
        private readonly DispatcherTimer timer;
        private readonly Func<Task<bool>> readPlayback;
        private readonly Action<bool> changed;
        private GlobalSystemMediaTransportControlsSessionManager manager;
        private volatile bool disposed;
        private bool refreshing;
        private bool? playing;
        internal string LastError { get; private set; }

        public SpotifyPlaybackMonitor(Action<bool> onChanged, Func<Task<bool>> read = null)
        {
            changed = onChanged; readPlayback = read ?? ReadWindowsPlayback;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += async delegate { await RefreshAsync(); };
            timer.Start();
        }

        internal static bool IsSpotifyPlaying(string sourceAppId, GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
        {
            if (status != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing || string.IsNullOrEmpty(sourceAppId)) return false;
            // Spotify's unpackaged executable and Microsoft Store app identity.
            // A browser tab/title containing "Spotify" is deliberately insufficient.
            string name = Path.GetFileName(sourceAppId);
            return string.Equals(name, "Spotify.exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Spotify", StringComparison.OrdinalIgnoreCase)
                || (sourceAppId.StartsWith("SpotifyAB.SpotifyMusic_", StringComparison.OrdinalIgnoreCase)
                    && sourceAppId.EndsWith("!Spotify", StringComparison.OrdinalIgnoreCase));
        }

        private Task<bool> ReadWindowsPlayback()
        {
            // WinRT/RPC calls stay off the dispatcher so a slow media session
            // cannot stall the pet, mouse interaction or chat.
            return Task.Run(async delegate
            {
                if (manager == null)
                {
                    var request = GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                    try
                    {
                        var timeout = Stopwatch.StartNew();
                        while (request.Status == AsyncStatus.Started)
                        {
                            if (disposed || timeout.Elapsed > TimeSpan.FromSeconds(3))
                            {
                                request.Cancel();
                                if (disposed) return false;
                                throw new TimeoutException("O Windows nao respondeu a consulta de sessoes de midia.");
                            }
                            await Task.Delay(25).ConfigureAwait(false);
                        }
                        manager = request.GetResults();
                    }
                    finally { request.Close(); }
                }
                if (disposed) return false;
                foreach (var session in manager.GetSessions())
                {
                    try
                    {
                        // Check every session, not only the system's current one:
                        // another media app can own the media keys while Spotify plays.
                        if (IsSpotifyPlaying(session.SourceAppUserModelId, session.GetPlaybackInfo().PlaybackStatus)) return true;
                    }
                    catch (COMException) { /* A session can disappear during enumeration. */ }
                }
                return false;
            });
        }

        internal async Task RefreshAsync()
        {
            if (disposed || refreshing) return;
            refreshing = true;
            bool next;
            try { next = await readPlayback(); LastError = null; }
            catch (Exception error)
            {
                manager = null; next = false; LastError = error.Message;
                Trace.TraceWarning("Spotify playback unavailable: {0}", error.Message);
            }
            finally { refreshing = false; }
            if (disposed || playing == next) return;
            playing = next; changed(next);
        }

        public void Dispose() { disposed = true; timer.Stop(); }
    }
}
