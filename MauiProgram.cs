using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Media;
using Microsoft.Extensions.Logging;
using Plugin.Maui.Audio;
using matrix.Services;
using matrix.ViewModels;

namespace matrix;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseMauiCommunityToolkitMediaElement()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Audio + STT
		builder.Services.AddSingleton(SpeechToText.Default);
		builder.Services.AddSingleton(AudioManager.Current);

		// App services
		builder.Services.AddSingleton<LudocApiService>();
		builder.Services.AddSingleton<LudocSseService>();

		// ViewModels
		builder.Services.AddTransient<VoiceViewModel>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<ChatViewModel>();
		builder.Services.AddTransient<ControlViewModel>();
		builder.Services.AddTransient<FactsViewModel>();
		builder.Services.AddTransient<SettingsViewModel>();

		// Pages (necessário para Shell + DI resolver construtores com ViewModel)
		builder.Services.AddTransient<Pages.DashboardPage>();
		builder.Services.AddTransient<Pages.VoicePage>();
		builder.Services.AddTransient<Pages.ChatPage>();
		builder.Services.AddTransient<Pages.ControlPage>();
		builder.Services.AddTransient<Pages.FactsPage>();
		builder.Services.AddTransient<Pages.SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
