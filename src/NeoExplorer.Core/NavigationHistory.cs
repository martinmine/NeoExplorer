namespace NeoExplorer.Core;

/// <summary>
/// Back/forward history of visited locations, like a web browser.
/// </summary>
public class NavigationHistory
{
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();

    public NavigationHistory(string start)
    {
        Current = start;
    }

    public string Current { get; private set; }

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    /// <summary>
    /// Moves to a new location and clears the forward history.
    /// Navigating to the current location does nothing.
    /// </summary>
    public void Navigate(string location)
    {
        if (string.Equals(location, Current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _back.Push(Current);
        _forward.Clear();
        Current = location;
    }

    public string GoBack()
    {
        if (!CanGoBack)
        {
            throw new InvalidOperationException("There is no location to go back to.");
        }

        _forward.Push(Current);
        Current = _back.Pop();
        return Current;
    }

    public string GoForward()
    {
        if (!CanGoForward)
        {
            throw new InvalidOperationException("There is no location to go forward to.");
        }

        _back.Push(Current);
        Current = _forward.Pop();
        return Current;
    }
}
