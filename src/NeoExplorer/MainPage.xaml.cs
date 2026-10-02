using Microsoft.UI.Xaml.Controls;
using NeoExplorer.ViewModels;

namespace NeoExplorer;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
    }
}
