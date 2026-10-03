using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using NeoExplorer.Core;
using NeoExplorer.Services;

namespace NeoExplorer.ViewModels;

public partial class NetworkItemViewModel(NetworkItem item) : ObservableObject
{
    public string Name => item.Name;

    public string Path => item.Path;

    [ObservableProperty]
    public partial ImageSource? Icon { get; private set; }

    /// <summary>
    /// The size is in physical pixels. Must be called on the UI thread.
    /// </summary>
    public async Task LoadIconAsync(int size) => Icon = await ShellIcons.LoadShellLocationAsync(Path, size);

    // Used by UI Automation (screen readers) as the item's name.
    public override string ToString() => Name;
}

/// <summary>
/// The Network page, listing the computers on the network, and a computer's page, listing its shared folders.
/// The shared folders themselves open in the folder view.
/// </summary>
public partial class NetworkViewModel(Action<string> navigate) : ObservableObject
{
    // Physical pixels for the 48 px icons, sharp at up to 200% display scaling.
    private const int IconPixels = 96;

    private const int ERROR_ACCESS_DENIED = 5;

    private string _location = "";

    /// <summary>
    /// A new collection for each location, so a load that is still running only fills a list nobody sees.
    /// </summary>
    [ObservableProperty]
    public partial ObservableCollection<NetworkItemViewModel> Items { get; private set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string Heading { get; private set; } = "";

    /// <summary>
    /// Explains an empty list, e.g. that no computers were found.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; } = "";

    public bool HasMessage => !IsLoading && Message != "";

    public async Task LoadAsync(string location)
    {
        _location = location;
        bool isNetwork = location == PathParser.Network;
        Heading = isNetwork ? "Computers" : "Shared folders";
        ObservableCollection<NetworkItemViewModel> items = Items = [];
        Message = "";
        IsLoading = true;

        // Progress reports arrive on the UI thread.
        var found = new Progress<NetworkItem>(item => Add(items, item));
        string message;
        try
        {
            if (isNetwork)
            {
                await Services.Network.GetComputersAsync(found);
                message = "No computers found. Check that network discovery is turned on in Windows Settings.";
            }
            else
            {
                foreach (NetworkItem share in await Task.Run(() => Services.Network.GetShares(location)))
                {
                    Add(items, share);
                }

                message = $"{location} has no shared folders.";
            }
        }
        catch (Win32Exception e)
        {
            // Unknown or offline computers give RPC errors like "The binding handle is invalid", which mean nothing to users.
            message = e.NativeErrorCode == ERROR_ACCESS_DENIED
                ? $"You don't have permission to access {location}."
                : $"Can't reach {location}. Check the spelling, and that the computer is turned on and connected to the network.";
        }

        // The user may have moved on while this was loading.
        if (_location != location)
        {
            return;
        }

        IsLoading = false;
        Message = items.Count == 0 ? message : "";
    }

    // Keeps the list sorted by name as computers are found.
    private static void Add(ObservableCollection<NetworkItemViewModel> items, NetworkItem item)
    {
        var viewModel = new NetworkItemViewModel(item);
        int index = 0;
        while (index < items.Count && ItemComparer.CompareNames(items[index].Name, item.Name) < 0)
        {
            index++;
        }

        items.Insert(index, viewModel);
        _ = viewModel.LoadIconAsync(IconPixels);
    }

    public void Open(NetworkItemViewModel item) => navigate(item.Path);
}
