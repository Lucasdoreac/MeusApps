using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using primeiroApp.Models;
using primeiroApp.Services;

namespace primeiroApp.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly LudocApiService _api;

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty] private string inputText = "";
    [ObservableProperty] private bool isClaudeActive = true;
    [ObservableProperty] private bool isLoading = false;
    [ObservableProperty] private string statusText = "ready";

    public ChatViewModel(LudocApiService api)
    {
        _api = api;
        AddMessage(MessageSender.System, "LUDOC OS interface ready. Select agent and type a command.");
    }

    [RelayCommand]
    public async Task SendAsync()
    {
        var text = InputText.Trim();
        if (string.IsNullOrEmpty(text) || IsLoading) return;

        InputText = "";
        AddMessage(MessageSender.User, text);
        IsLoading = true;

        try
        {
            if (IsClaudeActive)
                await SendClaudeAsync(text);
            else
                await SendGeminiAsync(text);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SendClaudeAsync(string query)
    {
        StatusText = "waiting for Claude…";
        var (answer, elapsed, error) = await _api.AskClaudeAsync(query);

        if (error != null)
        {
            AddMessage(MessageSender.System, $"Error: {error}");
            StatusText = "error";
        }
        else
        {
            AddMessage(MessageSender.Claude, answer ?? "(empty)");
            StatusText = $"Claude replied in {elapsed}ms";
        }
    }

    private async Task SendGeminiAsync(string query)
    {
        StatusText = "dispatching to Gemini…";
        var taskId = await _api.DispatchTaskAsync(query);

        if (taskId == null)
        {
            AddMessage(MessageSender.System, "Failed to dispatch task.");
            StatusText = "error";
            return;
        }

        AddMessage(MessageSender.System, $"task queued → {taskId[..Math.Min(20, taskId.Length)]}");

        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(4000);
            StatusText = $"Gemini processing… {(i + 1) * 4}s";

            var task = await _api.GetTaskAsync(taskId);
            if (task == null) continue;

            if (task.Status == "done")
            {
                AddMessage(MessageSender.Gemini, task.Result ?? "(empty)");
                StatusText = $"Gemini done in {(i + 1) * 4}s";
                return;
            }
            if (task.Status == "failed")
            {
                AddMessage(MessageSender.System, $"task failed: {task.Error}");
                StatusText = "Gemini task failed";
                return;
            }
        }

        AddMessage(MessageSender.System, "Timeout waiting for Gemini (120s).");
        StatusText = "timeout";
    }

    private void AddMessage(MessageSender sender, string text)
    {
        Messages.Add(new ChatMessage { Sender = sender, Text = text });
    }
}
