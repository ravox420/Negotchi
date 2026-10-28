using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Windows.Media.Control;

namespace Negotchi
{
    internal static partial class Tests
    {
        private static async Task TestSpotifyPlayback()
        {
            var playing = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            var paused = GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;
            Check(SpotifyPlaybackMonitor.IsSpotifyPlaying("Spotify.exe", playing), "reconhece play do Spotify desktop");
            Check(SpotifyPlaybackMonitor.IsSpotifyPlaying(@"C:\Apps\Spotify\Spotify.exe", playing), "reconhece identidade do executavel por caminho");
            Check(SpotifyPlaybackMonitor.IsSpotifyPlaying("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", playing), "reconhece Spotify da Microsoft Store");
            Check(!SpotifyPlaybackMonitor.IsSpotifyPlaying("Spotify.exe", paused), "Spotify aberto e pausado nao aciona danca");
            Check(!SpotifyPlaybackMonitor.IsSpotifyPlaying("chrome.exe", playing) && !SpotifyPlaybackMonitor.IsSpotifyPlaying("Spotify - Web Player", playing), "outros players e titulos de abas nao acionam danca");
            Check(!SpotifyPlaybackMonitor.IsSpotifyPlaying(null, playing), "sessao sem origem nao aciona danca");
            Check(new[] { "chrome.exe", "Spotify.exe" }.Any(id => SpotifyPlaybackMonitor.IsSpotifyPlaying(id, playing)), "considera Spotify mesmo quando outra sessao e a primeira");

            var dancingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bsod", "codex", "dewey", "fireball", "Guts", "null-signal", "rocky", "seedy", "stacky" };
            foreach (var definition in new PetCatalog().Pets.Where(p => dancingIds.Contains(p.Id)))
            {
                var builtIn = definition.Load();
                AnimationTrack dance;
                Check(builtIn.Animations.TryGetValue("dancing", out dance) && dance.Frames.Length == 8 && dance.Frames.Distinct().Count() == 8 && dance.Loop && dance.Fallback == "idle", "faixa de danca incorporada valida: " + definition.DisplayName);
            }

            bool sourcePlaying = false, fail = false;
            var changes = new List<bool>();
            using (var monitor = new SpotifyPlaybackMonitor(changes.Add, delegate
            {
                if (fail) throw new InvalidOperationException("Sessao indisponivel");
                return Task.FromResult(sourcePlaying);
            }))
            {
                await monitor.RefreshAsync(); sourcePlaying = true; await monitor.RefreshAsync(); await monitor.RefreshAsync();
                Check(changes.SequenceEqual(new[] { false, true }), "monitor publica transicoes sem reiniciar a danca em cada consulta");
                fail = true; await monitor.RefreshAsync();
                Check(!changes.Last() && monitor.LastError != null, "falha de consulta desativa danca sem encerrar aplicativo");
                fail = false; await monitor.RefreshAsync();
                Check(changes.Last() && monitor.LastError == null, "monitor recupera apos indisponibilidade de midia");
                sourcePlaying = false; await monitor.RefreshAsync();
                Check(!changes.Last(), "pausa e fechamento de sessao desativam danca");
            }
            int reads = 0, notifications = 0;
            var pending = new TaskCompletionSource<bool>();
            var pendingMonitor = new SpotifyPlaybackMonitor(value => notifications++, delegate { reads++; return pending.Task; });
            var pendingRefresh = pendingMonitor.RefreshAsync(); await pendingMonitor.RefreshAsync();
            Check(reads == 1, "monitor nao sobrepoe consultas lentas");
            pendingMonitor.Dispose(); pending.SetResult(true); await pendingRefresh;
            Check(notifications == 0, "encerrar monitor descarta consulta ainda pendente");

            var assets = new PetAssets();
            assets.Animations["dancing"] = new AnimationTrack { Frames = assets.Animations["idle"].Frames, Fps = 6, Loop = true, Fallback = "idle" };
            assets.Animations["jumping"] = new AnimationTrack { Frames = new[] { 0 }, Fps = 60, Loop = false, Fallback = "idle" };
            assets.Animations["waving"] = new AnimationTrack { Frames = new[] { 0 }, Fps = 60, Loop = false, Fallback = "idle" };
            var pet = new PetWindow(assets, new AppState(), delegate { }, delegate { }) { Left = 100, Top = 100 };
            try
            {
                pet.Play("idle"); pet.SetSpotifyPlaying(true);
                Check(pet.CurrentAnimation == "dancing", "play assume somente o repouso do pet com faixa dancing");
                pet.SetSpotifyPlaying(false); Check(pet.CurrentAnimation == "idle", "pausa restaura repouso imediatamente");
                pet.SetSpotifyPlaying(true); SetPetField(pet, "pressing", true); SetPetField(pet, "dragged", false); pet.SetSpotifyPlaying(false);
                PetCall(pet, "OnRelease", PetField(pet, "image"), new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Check(pet.CurrentAnimation == "idle", "pausa durante clique sem arraste encerra a danca ao soltar");
                pet.SetSpotifyPlaying(true); pet.Mood("Aguardando", "waiting", true);
                Check(pet.CurrentAnimation == "waiting", "espera do agente tem prioridade sobre musica");
                pet.SetSpotifyPlaying(false); pet.SetSpotifyPlaying(true);
                Check(pet.CurrentAnimation == "waiting", "alterar musica nao sobrepoe trabalho em andamento");
                pet.Mood("Pronto", "jumping", false);
                Check(pet.CurrentAnimation == "jumping", "comemoracao termina antes de retomar danca");
                Thread.Sleep(30); PetCall(pet, "Tick");
                Check(pet.CurrentAnimation == "dancing", "conclusao do salto retoma danca se Spotify continua tocando");
                pet.Mood("Falhou", "failed", false); pet.SetSpotifyPlaying(false);
                Check(pet.CurrentAnimation == "failed", "pausar musica preserva reacao de falha");
                pet.Mood("Pronto", "idle", false);
                SetPetField(pet, "pressing", true); SetPetField(pet, "dragged", true); pet.Play("running-right");
                pet.SetSpotifyPlaying(true);
                Check(pet.CurrentAnimation == "running-right", "arraste tem prioridade sobre play");
                PetCall(pet, "OnRelease", PetField(pet, "image"), new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Check(pet.CurrentAnimation == "dancing", "soltar arraste restaura danca sem perder contexto de repouso");
                SetPetField(pet, "pressing", true); SetPetField(pet, "dragged", true); pet.Play("running-left"); pet.SetSpotifyPlaying(false);
                PetCall(pet, "OnRelease", PetField(pet, "image"), new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Check(pet.CurrentAnimation == "idle", "pausa durante arraste impede retomar danca ao soltar");
                var withoutDance = new PetAssets(); withoutDance.Animations.Remove("dancing");
                pet.SetSpotifyPlaying(true); pet.SetPet(withoutDance); pet.Mood("Pronto", "idle", false);
                Check(pet.CurrentAnimation == "idle", "trocar para pet sem dancing preserva comportamento anterior");
                pet.SetPet(assets); Thread.Sleep(30); PetCall(pet, "Tick");
                Check(pet.CurrentAnimation == "dancing", "trocar para pet com dancing considera play ja ativo apos saudacao");
            }
            finally { pet.Close(); }
        }

        private static async Task<int> SpotifyStatus()
        {
            bool playing = false;
            using (var monitor = new SpotifyPlaybackMonitor(value => playing = value))
            {
                await monitor.RefreshAsync();
                var manager = (GlobalSystemMediaTransportControlsSessionManager)PetField(monitor, "manager");
                var sessions = manager == null ? new object[0] : manager.GetSessions().Select(s => (object)Json.Map("sourceAppId", s.SourceAppUserModelId, "status", s.GetPlaybackInfo().PlaybackStatus.ToString())).ToArray();
                Console.WriteLine(Json.Encode(Json.Map("spotifyPlaying", playing, "error", monitor.LastError, "sessions", sessions)));
                return monitor.LastError == null ? 0 : 1;
            }
        }
    }
}
