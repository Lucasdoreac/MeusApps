using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using primeiroApp.Services;

namespace primeiroApp.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LudocApiService _api;

    [ObservableProperty] private string serverUrl = AppConfig.ServerBase;
    [ObservableProperty] private string connectionStatus = "—";
    [ObservableProperty] private bool connectionOnline = false;
    [ObservableProperty] private bool isTesting = false;

    public SettingsViewModel(LudocApiService api)
    {
        _api = api;
    }

    partial void OnServerUrlChanged(string value)
    {
        AppConfig.ServerBase = value;
        ConnectionStatus = "—";
        ConnectionOnline = false;
    }

    [RelayCommand]
    public async Task TestConnectionAsync()
    {
        IsTesting = true;
        ConnectionStatus = "testing…";
        try
        {
            var health = await _api.GetHealthAsync();
            ConnectionOnline = health != null;
            ConnectionStatus = health != null ? "ONLINE" : "OFFLINE — server not responding";
        }
        catch (Exception ex)
        {
            ConnectionOnline = false;
            ConnectionStatus = $"ERROR: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
