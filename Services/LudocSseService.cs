using System.Text.Json;
using matrix.Models;

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
    private CancellationTokenSource? _cts;

    public LudocSseService(LudocApiService api) => _api = api;

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
                        if (e != null) EventReceived?.Invoke(e);
                        // Narração Automática: Tudo o que chega no Journal é narrado
                        if (!string.IsNullOrEmpty(e?.Detail)) _ = _api?.SpeakAsync(e.Detail);
                    }
                    catch { /* skip malformed event */ }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(8000, ct); }
        }
    }

    public void Stop() => _cts?.Cancel();
    public void Dispose() => Stop();
}
