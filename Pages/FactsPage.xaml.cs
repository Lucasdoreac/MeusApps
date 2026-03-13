using matrix.ViewModels;

namespace matrix.Pages;

public partial class FactsPage : ContentPage
{
    public FactsPage(FactsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
