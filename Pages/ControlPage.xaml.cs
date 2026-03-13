using matrix.ViewModels;

namespace matrix.Pages;

public partial class ControlPage : ContentPage
{
    public ControlPage(ControlViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
