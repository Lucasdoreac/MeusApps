using System.Text.Json;
using matrix.Models;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;

namespace matrix.Services;

public class LudocSseService : IDisposable
{
    public event Action<SseJournalEvent>? EventReceived;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly LudocApiService _api;
    private readonly ILogger<LudocSseService> _logger;
    private CancellationTokenSource? _cts;

    public LudocSseService(LudocApiService api,
        ILogger<LudocSseService> logger)
    {
        _api = api;
        _logger = logger;
    }

    public void Start()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        Task.Run(() => SseLoopAsync(_cts.Token));
    }

    private async Task SseLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var response = await _api.OpenEventsStreamAsync(ct);
                if (response == null) { await Task.Delay(5000, ct); continue; }
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line == null) break;
                    if (!line.StartsWith("data: ")) continue;
                    var json = line["data: ".Length..];
                    try
                    {
                        var e = JsonSerializer.Deserialize<SseJournalEvent>(json, _json);
                        if (e != null) 
                        {
                            // Map old journal action to new type system if needed
                            if (string.IsNullOrEmpty(e.Type) && !string.IsNullOrEmpty(e.Action))
                                e.Type = "journal_entry";

                            EventReceived?.Invoke(e);

                            // Automatic Voice Narration
                            if (e.Type == "voice_output" && !string.IsNullOrEmpty(e.Detail))
                                _ = SpeakAndPlayAsync(e.Detail);
                            else if (e.Type == "journal_entry" && !string.IsNullOrEmpty(e.Detail))
                                _ = SpeakAndPlayAsync(e.Detail);
                        }
                    }
                    catch { /* skip malformed event */ }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogWarning(ex, "SSE loop error, retrying in 8s"); await Task.Delay(8000, ct); }
        }
    }

    private async Task SpeakAndPlayAsync(string text)
    {
        try
        {
            // Step 1: sintetiza no servidor
            await _api.SpeakAsync(text);

            // Step 2: busca o WAV sintetizado e reproduz via Plugin.Maui.Audio
            var audioStream = await _api.GetVoiceStreamAsync("latest");
            if (audioStream == null) return;

            var ms = new MemoryStream();
            await audioStream.CopyToAsync(ms);
            ms.Position = 0;

            var player = AudioManager.Current.CreatePlayer(ms);
            player.Play();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SpeakAndPlayAsync failed"); }
    }

    public void Stop() => _cts?.Cancel();
    public void Dispose() => Stop();
}
