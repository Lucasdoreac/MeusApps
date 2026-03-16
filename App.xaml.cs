using System.Diagnostics;

namespace matrix;

public partial class App : Application
{
	private static readonly string LogFile =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "matrix-crash.log");

	private static readonly string DebugLogFile =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "matrix-debug.log");

	public App()
	{
		InitializeComponent();
		var debugWriter = new StreamWriter(DebugLogFile, append: true) { AutoFlush = true };
		Trace.Listeners.Add(new TextWriterTraceListener(debugWriter));
		Trace.AutoFlush = true;
		WireExceptionHandlers();
	}

	private void WireExceptionHandlers()
	{
		// Exceções não tratadas em qualquer thread
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
			Log("AppDomain", e.ExceptionObject as Exception);

		// Tasks async com exceção nunca observada
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			Log("UnobservedTask", e.Exception);
			e.SetObserved(); // impede crash
		};

#if WINDOWS
		// Exceções no UI thread WinUI 3
		Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, e) =>
		{
			Log("WinUI_UI", new Exception(e.Message, e.Exception));
			e.Handled = true;
		};
#endif
	}

	private static void Log(string source, Exception? ex)
	{
		var msg = $"[{DateTime.Now:HH:mm:ss.fff}] [{source}] {ex?.GetType().Name}: {ex?.Message}\n{ex?.StackTrace}";
		Debug.WriteLine(msg);
		try { File.AppendAllText(LogFile, msg + "\n\n"); } catch { /* nunca silenciar o handler */ }
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
