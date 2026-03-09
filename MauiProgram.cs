using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using primeiroApp.Services;
using primeiroApp.ViewModels;
using primeiroApp.Pages;

namespace primeiroApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(true)
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                // TODO: add ShareTechMono-Regular.ttf to Resources/Fonts/ and uncomment:
                // fonts.AddFont("ShareTechMono-Regular.ttf", "Mono");
            });

        // Services
        builder.Services.AddSingleton<LudocApiService>();

        // ViewModels
        builder.Services.AddTransient<ChatViewModel>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<VoiceViewModel>();

        // Pages
        builder.Services.AddTransient<ChatPage>();
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<VoicePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
