using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using matrix.Models;
using matrix.Services;

namespace matrix.ViewModels;

public partial class ControlViewModel : ObservableObject, IDisposable
{
    private readonly LudocApiService _api;
    private readonly IDispatcher _dispatcher;
    private readonly LudocSseService _sse;

    public ObservableCollection<string> Workflows { get; } = [];
    public ObservableCollection<WorkflowStepResult> LastSteps { get; } = [];
    public ObservableCollection<CoordinatorLock> ActiveLocks { get; } = [];
    public ObservableCollection<SseJournalEvent> LiveFeed { get; } = [];

    [ObservableProperty] private string _selectedWorkflow;
    [ObservableProperty] private string _workflowStatus;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _recentTaskCount;

    public ControlViewModel(LudocApiService api, IDispatcher dispatcher, LudocSseService sse)
    {
        _api = api;
        _dispatcher = dispatcher;
        _sse = sse;
        _selectedWorkflow = "daily-health-check";
        _workflowStatus = "";
        _isRunning = false;
        _recentTaskCount = 0;
        _sse.EventReceived += OnSseEvent;
        _ = LoadAsync();
    }

    private void OnSseEvent(SseJournalEvent e)
    {
        _dispatcher.Dispatch(() =>
        {
            LiveFeed.Insert(0, e);
            if (LiveFeed.Count > 20) LiveFeed.RemoveAt(LiveFeed.Count - 1);
        });
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        var workflows = await _api.ListWorkflowsAsync();
        var coordinator = await _api.GetCoordinatorStatusAsync();

        _dispatcher.Dispatch(() =>
        {
            Workflows.Clear();
            foreach (var w in workflows) Workflows.Add(w);
            if (Workflows.Count > 0 && !Workflows.Contains(SelectedWorkflow))
                SelectedWorkflow = Workflows[0];

            ActiveLocks.Clear();
            RecentTaskCount = 0;
            if (coordinator != null)
            {
                foreach (var l in coordinator.ActiveLocks) ActiveLocks.Add(l);
                RecentTaskCount = coordinator.RecentTasks.Count;
            }
        });
    }

    [RelayCommand]
    public async Task RunWorkflowAsync()
    {
        if (IsRunning || string.IsNullOrEmpty(SelectedWorkflow)) return;
        IsRunning = true;
        WorkflowStatus = "executando...";
        LastSteps.Clear();

        var result = await _api.RunWorkflowAsync(SelectedWorkflow);
        _dispatcher.Dispatch(() =>
        {
            if (result != null)
            {
                WorkflowStatus = result.Summary;
                foreach (var s in result.Results) LastSteps.Add(s);
            }
            else
            {
                WorkflowStatus = "falhou ou sem resposta";
            }
            IsRunning = false;
        });
    }

    [RelayCommand]
    public async Task DispatchOptimizeAsync()
    {
        await _api.McpDispatchAsync("system.optimize");
        WorkflowStatus = "system.optimize disparado";
    }

    [RelayCommand]
    public async Task DispatchResumeAsync()
    {
        await _api.McpDispatchAsync("context.resume");
        WorkflowStatus = "context.resume disparado";
    }

    [RelayCommand]
    public async Task DispatchCleanupAsync()
    {
        await _api.McpDispatchAsync("docs.cleanup");
        WorkflowStatus = "docs.cleanup disparado";
    }

    [RelayCommand]
    public async Task OpenVoiceAsync()
    {
        await Shell.Current.GoToAsync("//VoicePage");
    }

    [RelayCommand]
    public async Task RefreshCoordinatorAsync()
    {
        var coordinator = await _api.GetCoordinatorStatusAsync();
        _dispatcher.Dispatch(() =>
        {
            ActiveLocks.Clear();
            RecentTaskCount = 0;
            if (coordinator != null)
            {
                foreach (var l in coordinator.ActiveLocks) ActiveLocks.Add(l);
                RecentTaskCount = coordinator.RecentTasks.Count;
            }
        });
    }

    public void Dispose()
    {
        _sse.EventReceived -= OnSseEvent;
    }
}
