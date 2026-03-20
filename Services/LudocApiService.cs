using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using matrix.Models;
using Microsoft.Extensions.Logging;

namespace matrix.Services;

public class LudocApiService
{
    private readonly HttpClient _http;
    private readonly ILogger<LudocApiService> _logger;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public LudocApiService() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(90) },
        Microsoft.Extensions.Logging.Abstractions.NullLogger<LudocApiService>.Instance) { }

    public LudocApiService(HttpClient httpClient, ILogger<LudocApiService> logger)
    {
        _http = httpClient;
        _logger = logger;
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

            // v2.6 Health returns { status: "sovereign", metrics: { cpu, ram, disk, ... } }
            var m = root.TryGetProperty("metrics", out var mm) ? mm : root;

            if (m.TryGetProperty("cpu", out var cpu)) data.Node01.Cpu = cpu.GetInt32();
            if (m.TryGetProperty("ram", out var ram)) data.Node01.Ram = ram.GetInt32();
            
            if (m.TryGetProperty("disk", out var d))
            {
                if (d.TryGetProperty("used_gb", out var ug)) data.Disk.UsedGb = ug.GetDouble();
                if (d.TryGetProperty("free_gb", out var fg)) data.Disk.FreeGb = fg.GetDouble();
                if (d.TryGetProperty("total_gb", out var tg)) data.Disk.TotalGb = tg.GetDouble();
            }

            if (root.TryGetProperty("status", out var st)) 
                data.Hako.Status = st.GetString() ?? "sovereign";

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
        // Sovereign Kernel v2.6 uses Unified MCP Dispatch
        using var req = Req(HttpMethod.Post, "/mcp/dispatch");
        req.Content = Json(new { 
            source = sender, 
            type = "intent.voice", // Default routing for raw query
            payload = new { text = query } 
        });

        using var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("task_id", out var id) ? id.GetString() : null;
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
        catch (Exception ex) { _logger.LogWarning(ex, "SpeakAsync failed"); }
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

    public async Task<JournalInsightsResponse?> GetJournalInsightsAsync(string since = "24h")
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"/journal/insights?since={since}");
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<JournalInsightsResponse>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    public async Task<VoiceAnalysis?> AnalyzeVoiceAsync(string transcript, string? response = null)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/voice/analyze");
            req.Content = Json(new { transcript, response });
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<VoiceAnalysis>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    // ── Facts ──────────────────────────────────────────────────────────────────

    public async Task<List<FactEntry>> GetFactsAsync(int limit = 20)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"/facts?limit={limit}");
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return [];
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            // Response may be array or { facts: [...] }
            var arr = root.ValueKind == JsonValueKind.Array ? root
                : root.TryGetProperty("facts", out var f) ? f : root;
            return JsonSerializer.Deserialize<List<FactEntry>>(arr.GetRawText(), _json) ?? [];
        }
        catch { return []; }
    }

    public async Task<FactSearchResult?> SearchFactsAsync(string q, int limit = 10)
    {
        try
        {
            using var req = Req(HttpMethod.Get, $"/facts/search?q={Uri.EscapeDataString(q)}&limit={limit}");
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<FactSearchResult>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    public async Task<(string? id, bool ok)> RecordFactAsync(string key, string value,
        string source = "ludoc-ui", string[]? tags = null)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/facts");
            req.Content = Json(new { key, value, source, tags = tags ?? [] });
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return (null, false);
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var id = doc.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
            return (id, true);
        }
        catch { return (null, false); }
    }

    // ── MCP Dispatch ───────────────────────────────────────────────────────────

    public async Task<string?> AskLlamaAsync(string query)
    {
        using var req = Req(HttpMethod.Post, "/ask/llama");
        req.Content = Json(new { query });
        using var res = await _http.SendAsync(req);
        return await res.Content.ReadAsStringAsync();
    }

    public async Task<McpDispatchResult?> McpDispatchAsync(string type,
        object? payload = null, string source = "ludoc-ui")
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/mcp/dispatch");
            req.Content = Json(new { source, type, payload });
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<McpDispatchResult>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    // ── Workflows ──────────────────────────────────────────────────────────────

    public async Task<List<string>> ListWorkflowsAsync()
    {
        try
        {
            using var req = Req(HttpMethod.Get, "/mcp/workflows");
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return [];
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("workflows", out var w))
                return JsonSerializer.Deserialize<List<string>>(w.GetRawText(), _json) ?? [];
            return [];
        }
        catch { return []; }
    }

    public async Task<WorkflowResult?> RunWorkflowAsync(string name, object? overrides = null)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/mcp/workflow");
            req.Content = Json(new { workflow = name, overrides });
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<WorkflowResult>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    // ── Coordinator ────────────────────────────────────────────────────────────

    public async Task<CoordinatorStatus?> GetCoordinatorStatusAsync()
    {
        try
        {
            using var req = Req(HttpMethod.Get, "/coordinator/status");
            using var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode) return null;
            return JsonSerializer.Deserialize<CoordinatorStatus>(
                await res.Content.ReadAsStringAsync(), _json);
        }
        catch { return null; }
    }

    // ── Journal Append ─────────────────────────────────────────────────────────

    public async Task AppendJournalAsync(string agent, string action,
        string target, string? detail = null)
    {
        try
        {
            using var req = Req(HttpMethod.Post, "/journal/append");
            req.Content = Json(new { agent, action, target, detail });
            await _http.SendAsync(req);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "AppendJournalAsync failed"); }
    }

    // ── SSE stream ────────────────────────────────────────────────────────────

    public async Task<HttpResponseMessage?> OpenEventsStreamAsync(CancellationToken ct)
    {
        try
        {
            var req = Req(HttpMethod.Get, "/events");
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch { return null; }
    }
}
