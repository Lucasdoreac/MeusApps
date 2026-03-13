using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using matrix.Models;
using matrix.Services;

#if WINDOWS
using Windows.Media.SpeechRecognition;
#endif

namespace matrix.ViewModels;

public partial class VoiceViewModel : ObservableObject
{
    private readonly LudocApiService _api;
    private readonly IDispatcher _dispatcher;

    public ObservableCollection<LudocTask> ActiveTasks { get; } = [];
    public ObservableCollection<JournalEntry> JournalEntries { get; } = [];

    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private string _statusText;
    [ObservableProperty] private string _liveTranscription;

    private CancellationTokenSource? _pollCts;

#if WINDOWS
    private SpeechRecognizer? _speechRecognizer;
#endif

    public VoiceViewModel(LudocApiService api, IDispatcher dispatcher)
    {
        _api = api;
        _dispatcher = dispatcher;
        _isRecording = false;
        _isProcessing = false;
        _statusText = "Aguardando...";
        _liveTranscription = "";
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
#if WINDOWS
        try
        {
            _speechRecognizer = new SpeechRecognizer();
            _speechRecognizer.ContinuousRecognitionSession.ResultGenerated += OnSpeechResult;
            await _speechRecognizer.ContinuousRecognitionSession.StartAsync();
            LiveTranscription = "";
            IsRecording = true;
            StatusText = "Ouvindo...";
        }
        catch (Exception ex)
        {
            StatusText = $"Erro mic: {ex.Message[..Math.Min(30, ex.Message.Length)]}";
        }
#else
        // Fallback simulado em outras plataformas
        IsRecording = true;
        StatusText = "Ouvindo... (simulado)";
        await Task.Delay(2000);
#endif
    }

    private async Task StopRecordingAsync()
    {
#if WINDOWS
        string finalText = LiveTranscription;
        if (_speechRecognizer != null)
        {
            _speechRecognizer.ContinuousRecognitionSession.ResultGenerated -= OnSpeechResult;
            await _speechRecognizer.ContinuousRecognitionSession.StopAsync();
            _speechRecognizer.Dispose();
            _speechRecognizer = null;
        }
        IsRecording = false;
        IsProcessing = true;
        StatusText = "Enviando...";
        await SendToAgentAsync(finalText);
#else
        IsRecording = false;
        IsProcessing = true;
        StatusText = "Enviando...";
        await SendToAgentAsync("Qual o status do sistema?");
#endif
    }

#if WINDOWS
    private void OnSpeechResult(
        SpeechContinuousRecognitionSession sender,
        SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (args.Result.Confidence == SpeechRecognitionConfidence.Rejected) return;
        _dispatcher.Dispatch(() =>
        {
            LiveTranscription = args.Result.Text;
            StatusText = LiveTranscription;
        });
    }
#endif

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

        var response = await _api.SendVoiceInputAsync(transcription);

        _dispatcher.Dispatch(() =>
        {
            if (response == null)
            {
                StatusText = "Erro na comunicação.";
            }
            else if (!string.IsNullOrEmpty(response.Response))
            {
                StatusText = "Pronto.";
            }
            else if (!string.IsNullOrEmpty(response.TaskId))
            {
                var shortId = response.TaskId[..Math.Min(8, response.TaskId.Length)];
                StatusText = $"Task: {shortId}…";
            }
            else
            {
                StatusText = $"→ {response.RoutedTo}";
            }
            IsProcessing = false;
        });
    }
}
