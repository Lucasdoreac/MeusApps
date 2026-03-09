using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using primeiroApp.Models;
using primeiroApp.Services;

namespace primeiroApp.ViewModels;

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly LudocApiService _api;
    private readonly IDispatcher _dispatcher;
    private System.Timers.Timer? _timer;
    private int _tickCount = 0;

    public ObservableCollection<LudocTask> Tasks { get; } = [];
    public ObservableCollection<JournalEntry> Journal { get; } = [];
    public ObservableCollection<ProcessInfo> TopProcesses { get; } = [];
    public ObservableCollection<SystemAlert> Alerts { get; } = [];

    [ObservableProperty] private TelemetryData telemetry = new();
    [ObservableProperty] private bool serverOnline = false;
    [ObservableProperty] private int activeTaskCount = 0;

    // Computed disk display
    public string DiskLabel => $"{Telemetry.Disk.UsedGb:F0}/{Telemetry.Disk.TotalGb:F0} GB";
    public double DiskProgress => Telemetry.Disk.TotalGb > 0
        ? Math.Min(1.0, Telemetry.Disk.UsedGb / Telemetry.Disk.TotalGb) : 0;
    public string PagefileLabel => Telemetry.PagefileMb > 0 ? $"{Telemetry.PagefileMb} MB" : "--";

    public DashboardViewModel(LudocApiService api, IDispatcher dispatcher)
    {
        _api = api;
        _dispatcher = dispatcher;
        StartPolling();
    }

    private void StartPolling()
    {
        _timer = new System.Timers.Timer(5000);
        _timer.Elapsed += async (_, _) =>
        {
            _tickCount++;
            await RefreshAsync();
        };
        _timer.AutoReset = true;
        _timer.Start();
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        // Every 6th tick (~30s) use /system/analyze for enriched data; otherwise /health
        TelemetryData? health;
        if (_tickCount % 6 == 0)
            health = await _api.GetSystemAnalysisAsync() ?? await _api.GetHealthAsync();
        else
            health = await _api.GetHealthAsync();

        var tasks   = await _api.ListTasksAsync(limit: 15);
        var journal = await _api.GetJournalAsync(limit: 8);

        _dispatcher.Dispatch(() =>
        {
            ServerOnline = health != null;
            if (health != null)
            {
                Telemetry = health;
                OnPropertyChanged(nameof(DiskLabel));
                OnPropertyChanged(nameof(DiskProgress));
                OnPropertyChanged(nameof(PagefileLabel));

                TopProcesses.Clear();
                foreach (var p in health.TopProcesses) TopProcesses.Add(p);

                Alerts.Clear();
                foreach (var a in health.Alerts) Alerts.Add(a);
            }

            Tasks.Clear();
            foreach (var t in tasks) Tasks.Add(t);
            ActiveTaskCount = tasks.Count(t => t.Status is "queued" or "processing");

            Journal.Clear();
            foreach (var j in journal) Journal.Add(j);
        });
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
