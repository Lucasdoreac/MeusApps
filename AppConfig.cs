namespace matrix;

public static class AppConfig
{
    private const string ServerUrlKey = "ludoc_server_url";
    private const string DefaultServerUrl = "http://localhost:9001";

    public static string ServerBase
    {
        get => Preferences.Default.Get(ServerUrlKey, DefaultServerUrl);
        set => Preferences.Default.Set(ServerUrlKey, value);
    }

    // Tailscale VPN is trusted — no token required
    public const string AuthToken = "";
}
