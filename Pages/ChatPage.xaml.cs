using primeiroApp.ViewModels;

namespace primeiroApp.Pages;

public partial class ChatPage : ContentPage
{
    private readonly ChatViewModel _vm;

    public ChatPage(ChatViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    private void OnClaudeSelected(object sender, EventArgs e)
    {
        _vm.IsClaudeActive = true;
        ClaudeBtn.BackgroundColor = Color.FromArgb("#00FF88");
        ClaudeBtn.TextColor = Colors.Black;
        GeminiBtn.BackgroundColor = Color.FromArgb("#07080A");
        GeminiBtn.TextColor = Color.FromArgb("#3A4455");
    }

    private void OnGeminiSelected(object sender, EventArgs e)
    {
        _vm.IsClaudeActive = false;
        GeminiBtn.BackgroundColor = Color.FromArgb("#FFB800");
        GeminiBtn.TextColor = Colors.Black;
        ClaudeBtn.BackgroundColor = Color.FromArgb("#07080A");
        ClaudeBtn.TextColor = Color.FromArgb("#3A4455");
    }
}
