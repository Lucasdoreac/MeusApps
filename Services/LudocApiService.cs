using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using primeiroApp.Models;

namespace primeiroApp.Services;

public class LudocApiService
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public LudocApiService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
    }

    private string Base => AppConfig.ServerBase;
    private string Token => AppConfig.AuthToken;

    private HttpRequestMessage Req(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, $"{Base}{path}");
        req.Headers.Add("X-Ludoc-Token", Token);
        return req;
    }

    private StringContent Json(object obj) =>
        new(JsonSerializer.Serialize(obj, _json), Encoding.UTF8, "application/json");

    // ── Health ────────────────────────────────────────────────

    public async Task<TelemetryData?> GetHealthAsync() => await FetchTelemetryAsync("/health", isHealth: true);

    // ── System Analyze ────────────────────────────────────────

    public async Task<TelemetryData?> GetSystemAnalysisAsync() => await FetchTelemetryAsync("/system/analyze", isHealth: false);

    private async Task<TelemetryData?> FetchTelemetryAsync(string path, bool isHealth)
    {
        try
        {
            using var req = Req(HttpMethod.Get, path);
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var data = new TelemetryData();

            // /health wraps telemetry inside "telemetry" key; /system/analyze uses "snapshot"
            var t = isHealth
                ? (root.TryGetProperty("telemetry", out var th) ? th : root)
                : (root.TryGetProperty("snapshot", out var ts) ? ts : root);

            if (t.TryGetProperty("node_01", out var n))
            {
                if (n.TryGetProperty("cpu", out var cpu)) data.Node01.Cpu = cpu.GetInt32();
                if (n.TryGetProperty("ram", out var ram)) data.Node01.Ram = ram.GetInt32();
            }
            if (t.TryGetProperty("hako", out var h))
            {
                if (h.TryGetProperty("status", out var st)) data.Hako.Status = st.GetString() ?? "offline";
                if (h.TryGetProperty("ram", out var hr)) data.Hako.Ram = hr.GetInt32();
            }
            if (t.TryGetProperty("disk", out var d))
            {
                if (d.TryGetProperty("used_gb", out var ug)) data.Disk.UsedGb = ug.GetDouble();
                if (d.TryGetProperty("free_gb", out var fg)) data.Disk.FreeGb = fg.GetDouble();
                if (d.TryGetProperty("total_gb", out var tg)) data.Disk.TotalGb = tg.GetDouble();
            }
            if (t.TryGetProperty("pagefile_mb", out var pf)) data.PagefileMb = pf.GetInt64();
            if (t.TryGetProperty("top_processes", out var procs) && procs.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in procs.EnumerateArray())
                {
                    data.TopProcesses.Add(new ProcessInfo
                    {
                        Name    = p.TryGetProperty("name", out var pn) ? pn.GetString() ?? "" : "",
                        Pid     = p.TryGetProperty("pid", out var pi) ? pi.GetInt32() : 0,
                        RamMb   = p.TryGetProperty("ram_mb", out var pr) ? pr.GetInt32() : 0,
                        CpuSecs = p.TryGetProperty("cpu_secs", out var pc) ? pc.GetInt32() : 0
                    });
                }
            }
            if (t.TryGetProperty("alerts", out var alerts) && alerts.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in alerts.EnumerateArray())
                {
                    data.Alerts.Add(new SystemAlert
                    {
                        Level    = a.TryGetProperty("level", out var al) ? al.GetString() ?? "info" : "info",
                        Message  = a.TryGetProperty("message", out var am) ? am.GetString() ?? "" : "",
                        ImpactMb = a.TryGetProperty("impact_mb", out var ai) ? ai.GetInt32() : 0
                    });
                }
            }
            if (root.TryGetProperty("latest_voice_id", out var vid))
                data.LatestVoiceId = vid.GetString() ?? "init";

            return data;
        }
        catch { return null; }
    }

    // ── Claude ────────────────────────────────────────────────

    public async Task<(string? answer, int elapsedMs, string? error)> AskClaudeAsync(string query, int timeout = 60)
    {
        using var req = Req(HttpMethod.Post, "/ask/claude");
        req.Content = Json(new { query, timeout });

        using var res = await _http.SendAsync(req);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var elapsed = root.TryGetProperty("elapsed_ms", out var e) ? e.GetInt32() : 0;
        if (root.TryGetProperty("error", out var err))
            return (null, elapsed, err.GetString());

        return (root.TryGetProperty("answer", out var a) ? a.GetString() : null, elapsed, null);
    }

    // ── Gemini (dispatch + poll) ──────────────────────────────

    public async Task<string?> DispatchTaskAsync(string query, string sender = "ludoc-ui")
    {
        using var req = Req(HttpMethod.Post, "/context/dispatch/raw");
        req.Content = Json(new { query, sender });

        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    public async Task<LudocTask?> GetTaskAsync(string taskId)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"/tasks/{taskId}");
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<LudocTask>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    // ── Tasks List ────────────────────────────────────────────

    public async Task<List<LudocTask>> ListTasksAsync(int limit = 12, string? status = null)
    {
        try
        {
            var path = $"/tasks?limit={limit}" + (status != null ? $"&status={status}" : "");
            using var req = Req(HttpMethod.Get, path);
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return [];
            return JsonSerializer.Deserialize<List<LudocTask>>(
                await res.Content.ReadAsStringAsync(), _json) ?? [];
        }
        catch { return []; }
    }

    // ── Journal ───────────────────────────────────────────────

    public async Task<List<JournalEntry>> GetJournalAsync(int limit = 8, string? since = null)
    {
        try
        {
            var path = $"/journal?limit={limit}" + (since != null ? $"&since={since}" : "");
            using var req = Req(HttpMethod.Get, path);
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return [];
            return JsonSerializer.Deserialize<List<JournalEntry>>(
                await res.Content.ReadAsStringAsync(), _json) ?? [];
        }
        catch { return []; }
    }

    // ── Voice ─────────────────────────────────────────────────

    public async Task SpeakAsync(string text)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/voice/speak");
            req.Content = Json(new { text });
            await _http.SendAsync(req);
        }
        catch { }
    }

    public async Task<Stream?> GetVoiceStreamAsync(string voiceId)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"/voice/teste_voz.wav?v={voiceId}");
            var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
            return res.IsSuccessStatusCode ? await res.Content.ReadAsStreamAsync() : null;
        }
        catch { return null; }
    }

    public async Task<VoiceInputResponse?> SendVoiceInputAsync(string transcription, float confidence = 0.9f)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/voice/input");
            req.Content = Json(new { transcription, confidence });
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;

            return JsonSerializer.Deserialize<VoiceInputResponse>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }
}
