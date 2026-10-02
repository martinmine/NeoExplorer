using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class NavigationHistoryTests
{
    [Fact]
    public void NewHistory_HasNoBackOrForward()
    {
        var history = new NavigationHistory(@"C:\");

        Assert.Equal(@"C:\", history.Current);
        Assert.False(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void GoBack_ReturnsPreviousLocation_AndEnablesForward()
    {
        var history = new NavigationHistory(@"C:\");
        history.Navigate(@"C:\Users");

        Assert.Equal(@"C:\", history.GoBack());
        Assert.Equal(@"C:\", history.Current);
        Assert.False(history.CanGoBack);
        Assert.True(history.CanGoForward);
    }

    [Fact]
    public void GoForward_ReturnsToLocationLeftByGoBack()
    {
        var history = new NavigationHistory(@"C:\");
        history.Navigate(@"C:\Users");
        history.GoBack();

        Assert.Equal(@"C:\Users", history.GoForward());
        Assert.True(history.CanGoBack);
        Assert.False(history.CanGoForward);
    }

    [Fact]
    public void Navigate_ClearsForwardHistory()
    {
        var history = new NavigationHistory(@"C:\");
        history.Navigate(@"C:\Users");
        history.GoBack();

        history.Navigate(@"C:\Windows");

        Assert.False(history.CanGoForward);
        Assert.Equal(@"C:\", history.GoBack());
    }

    [Fact]
    public void Navigate_ToCurrentLocation_IgnoringCase_DoesNotAddHistory()
    {
        var history = new NavigationHistory(@"C:\Users");

        history.Navigate(@"c:\users");

        Assert.False(history.CanGoBack);
    }

    [Fact]
    public void GoBack_And_GoForward_Throw_WhenEmpty()
    {
        var history = new NavigationHistory(@"C:\");

        Assert.Throws<InvalidOperationException>(() => history.GoBack());
        Assert.Throws<InvalidOperationException>(() => history.GoForward());
    }
}
