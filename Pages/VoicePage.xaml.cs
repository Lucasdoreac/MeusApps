using primeiroApp.ViewModels;

namespace primeiroApp.Pages;

public partial class VoicePage : ContentPage
{
    public VoicePage(VoiceViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((VoiceViewModel)BindingContext).StartPolling();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ((VoiceViewModel)BindingContext).StopPolling();
    }
}
