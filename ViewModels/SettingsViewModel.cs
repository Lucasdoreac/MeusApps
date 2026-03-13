using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using matrix.Services;

namespace matrix.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly LudocApiService _api;

    [ObservableProperty] private string _serverUrl;
    [ObservableProperty] private string _connectionStatus;
    [ObservableProperty] private bool _connectionOnline;
    [ObservableProperty] private bool _isTesting;

    public SettingsViewModel(LudocApiService api)
    {
        _api = api;
        _serverUrl = AppConfig.ServerBase;
        _connectionStatus = "—";
        _connectionOnline = false;
        _isTesting = false;
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
