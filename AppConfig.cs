namespace matrix;

public static class AppConfig
{
    private const string ServerUrlKey = "ludoc_server_url";
    private const string DefaultServerUrl = "http://localhost:9000";

    public static string ServerBase
    {
        get => Preferences.Default.Get(ServerUrlKey, DefaultServerUrl);
        set => Preferences.Default.Set(ServerUrlKey, value);
    }

    public const string AuthToken = "894fb65c-4118-4b4e-b90d-7c44f425703a";
}
