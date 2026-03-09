namespace primeiroApp.Models;

// â”€â”€ Chat â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public enum MessageSender { User, Claude, Gemini, System }

public class ChatMessage
{
    public MessageSender Sender { get; init; }
    public string Text { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string SenderLabel => Sender switch
    {
        MessageSender.User   => "YOU",
        MessageSender.Claude => "CLAUDE",
        MessageSender.Gemini => "GEMINI",
        _                    => "SYS"
    };
}

// â”€â”€ Task Queue â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public class LudocTask
{
    public string Id { get; set; } = "";
    public string Query { get; set; } = "";
    public string Status { get; set; } = "queued";
    public string? Agent { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public long CreatedAt { get; set; }
    public long? StartedAt { get; set; }
    public long? CompletedAt { get; set; }

    public string ShortQuery => Query.Length > 55 ? Query[..55] + "â€¦" : Query;
    public string ShortId    => Id.Length > 16 ? Id[..16] : Id;
}

// â”€â”€ Journal â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public class JournalEntry
{
    public string Id { get; set; } = "";
    public string Agent { get; set; } = "";
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Detail { get; set; }
    public long Timestamp { get; set; }

    public string ShortAgent  => Agent.Length > 18 ? Agent[..18] : Agent;
    public string ShortTarget => Target.Length > 28 ? Target[..28] : Target;
}

// â”€â”€ Telemetry â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

public class NodeTelemetry
{
    public int Cpu { get; set; }
    public int Ram { get; set; }
}

public class HakoTelemetry
{
    public string Status { get; set; } = "offline";
    public int Ram { get; set; }
}

public class DiskInfo
{
    public double UsedGb { get; set; }
    public double FreeGb { get; set; }
    public double TotalGb { get; set; }
    public double UsedPercent => TotalGb > 0 ? UsedGb / TotalGb * 100 : 0;
}

public class ProcessInfo
{
    public string Name { get; set; } = "";
    public int Pid { get; set; }
    public int RamMb { get; set; }
    public int CpuSecs { get; set; }
    public string ShortName => Name.Length > 22 ? Name[..22] : Name;
}

public class SystemAlert
{
    public string Level { get; set; } = "info";   // info | warn | critical
    public string Message { get; set; } = "";
    public int ImpactMb { get; set; }

    public Color LevelColor => Level switch
    {
        "critical" => Color.FromArgb("#FF3355"),
        "warn"     => Color.FromArgb("#FFB800"),
        _          => Color.FromArgb("#4488FF")
    };

    public string LevelIcon => Level switch
    {
        "critical" => "âœ–",
        "warn"     => "âš ",
        _          => "â„¹"
    };
}

public class VoiceInputResponse
{
    public string RoutedTo { get; set; } = "";
    public string? TaskId { get; set; }
    public string Transcription { get; set; } = "";
    public string? Response { get; set; }
}

public class TelemetryData
{
    public NodeTelemetry Node01 { get; set; } = new();
    public HakoTelemetry Hako { get; set; } = new();
    public string LatestVoiceId { get; set; } = "init";
    // System metrics
    public DiskInfo Disk { get; set; } = new();
    public long PagefileMb { get; set; }
    public List<ProcessInfo> TopProcesses { get; set; } = [];
    public List<SystemAlert> Alerts { get; set; } = [];
}
