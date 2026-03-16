using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Maui.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using matrix.Models;
using matrix.Services;
using Plugin.Maui.Audio;

namespace matrix.ViewModels;

public partial class VoiceViewModel : ObservableObject
{
    private readonly LudocApiService _api;
    private readonly IDispatcher _dispatcher;
    private readonly ISpeechToText _stt;
    private readonly IAudioManager _audioManager;

    public ObservableCollection<LudocTask> ActiveTasks { get; } = [];
    public ObservableCollection<JournalEntry> JournalEntries { get; } = [];

    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private string _liveTranscription;
    [ObservableProperty] private string _agentResponse;
    [ObservableProperty] private bool _isPlayingAudio;
    [ObservableProperty] private string _routedTo;

    private CancellationTokenSource? _pollCts;
    private CancellationTokenSource? _sttCts;
    private string _lastKnownVoiceId = "init";

    public VoiceViewModel(LudocApiService api, IDispatcher dispatcher,
        ISpeechToText stt, IAudioManager audioManager)
    {
        _api = api;
        _dispatcher = dispatcher;
        _stt = stt;
        _audioManager = audioManager;
        _isRecording = false;
        _isProcessing = false;
        _statusText = "Aguardando...";
        _liveTranscription = "";
        _agentResponse = "";
        _routedTo = "";
    }

    // ── Polling ───────────────────────────────────────────────

    public void StartPolling()
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    public void StopPolling() => _pollCts?.Cancel();

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshTasksAndJournalAsync();
                await Task.Delay(3000, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch { /* swallow network errors silently */ }
        }
    }

    private async Task RefreshTasksAndJournalAsync()
    {
        var tasks = await _api.ListTasksAsync(limit: 8);
        var active = tasks.Where(t => t.Status is "queued" or "processing").ToList();

        _dispatcher.Dispatch(() =>
        {
            var toRemove = ActiveTasks.Where(t => !active.Any(a => a.Id == t.Id)).ToList();
            foreach (var r in toRemove) ActiveTasks.Remove(r);
            foreach (var a in active.Where(a => !ActiveTasks.Any(t => t.Id == a.Id)))
                ActiveTasks.Add(a);
        });

        var journal = await _api.GetJournalAsync(limit: 8, since: "2h");
        _dispatcher.Dispatch(() =>
        {
            JournalEntries.Clear();
            foreach (var j in journal) JournalEntries.Add(j);
        });
    }

    // ── STT + Gravação ────────────────────────────────────────

    [RelayCommand]
    public async Task ToggleRecordAsync()
    {
        if (IsRecording)
            await StopRecordingAsync();
        else
            await StartRecordingAsync();
    }

    private async Task StartRecordingAsync()
    {
        var status = await _stt.RequestPermissions();
        if (!status)
        {
            StatusText = "Permissão de microfone negada.";
            return;
        }

        LiveTranscription = "";
        AgentResponse = "";
        RoutedTo = "";
        IsRecording = true;
        StatusText = "Ouvindo...";

        _sttCts = new CancellationTokenSource();
        try
        {
            await _stt.StartListenAsync(
                CultureInfo.GetCultureInfo("pt-BR"),
                _sttCts.Token);

            _stt.RecognitionResultUpdated += OnRecognitionUpdated;
        }
        catch (Exception ex)
        {
            IsRecording = false;
            StatusText = $"Erro mic: {ex.Message[..Math.Min(40, ex.Message.Length)]}";
        }
    }

    private async Task StopRecordingAsync()
    {
        _stt.RecognitionResultUpdated -= OnRecognitionUpdated;
        _sttCts?.Cancel();

        string finalText = LiveTranscription;
        IsRecording = false;
        IsProcessing = true;
        StatusText = "Enviando...";

        await _stt.StopListenAsync(CancellationToken.None);
        await SendToAgentAsync(finalText);
    }

    private void OnRecognitionUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
    {
        _dispatcher.Dispatch(() =>
        {
            LiveTranscription = e.RecognitionResult;
            StatusText = LiveTranscription.Length > 0 ? LiveTranscription : "Ouvindo...";
        });
    }

    // ── Envio ao agente ───────────────────────────────────────

    private async Task SendToAgentAsync(string transcription)
    {
        if (string.IsNullOrWhiteSpace(transcription))
        {
            _dispatcher.Dispatch(() =>
            {
                StatusText = "Aguardando...";
                IsProcessing = false;
            });
            return;
        }

        // Capturar latest_voice_id antes de enviar
        var healthBefore = await _api.GetHealthAsync();
        _lastKnownVoiceId = healthBefore?.LatestVoiceId ?? "init";

        var response = await _api.SendVoiceInputAsync(transcription);

        _dispatcher.Dispatch(() =>
        {
            if (response == null)
            {
                StatusText = "Erro na comunicação.";
                IsProcessing = false;
                return;
            }

            RoutedTo = response.RoutedTo;

            if (!string.IsNullOrEmpty(response.Response))
            {
                AgentResponse = response.Response;
                StatusText = "Reproduzindo...";
                _ = SpeakAndPlayAsync(response.Response);
            }
            else if (!string.IsNullOrEmpty(response.TaskId))
            {
                var shortId = response.TaskId[..Math.Min(8, response.TaskId.Length)];
                StatusText = $"Task: {shortId}…";
                IsProcessing = false;
            }
            else
            {
                StatusText = $"→ {response.RoutedTo}";
                IsProcessing = false;
            }
        });
    }

    // ── TTS playback ──────────────────────────────────────────

    private async Task SpeakAndPlayAsync(string text)
    {
        try
        {
            // Pedir ao servidor para sintetizar
            await _api.SpeakAsync(text);

            // Poll /health até latest_voice_id mudar (max 15s)
            string? newVoiceId = null;
            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(500);
                var h = await _api.GetHealthAsync();
                if (h != null && h.LatestVoiceId != _lastKnownVoiceId)
                {
                    newVoiceId = h.LatestVoiceId;
                    break;
                }
            }

            if (newVoiceId == null)
            {
                _dispatcher.Dispatch(() => { StatusText = "Pronto."; IsProcessing = false; });
                return;
            }

            // Download do WAV
            var stream = await _api.GetVoiceStreamAsync(newVoiceId);
            if (stream == null)
            {
                _dispatcher.Dispatch(() => { StatusText = "Pronto."; IsProcessing = false; });
                return;
            }

            _dispatcher.Dispatch(() => IsPlayingAudio = true);

            var player = _audioManager.CreatePlayer(stream);
            player.PlaybackEnded += (_, _) =>
            {
                _dispatcher.Dispatch(() =>
                {
                    IsPlayingAudio = false;
                    StatusText = "Pronto.";
                    IsProcessing = false;
                });
                player.Dispose();
            };
            player.Play();
        }
        catch
        {
            _dispatcher.Dispatch(() =>
            {
                IsPlayingAudio = false;
                StatusText = "Pronto.";
                IsProcessing = false;
            });
        }
    }
}
