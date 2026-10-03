using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class FileNameValidatorTests
{
    [Theory]
    [InlineData("notes.txt")]
    [InlineData("Photos 2026")]
    [InlineData(".gitignore")]
    [InlineData("console.txt")]
    [InlineData("COM10")]
    public void Validate_AcceptsOrdinaryNames(string name)
    {
        Assert.Null(FileNameValidator.Validate(name));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("what?")]
    [InlineData("star*")]
    [InlineData("\"quoted\"")]
    [InlineData("<tag>")]
    [InlineData("pipe|")]
    [InlineData("c:")]
    [InlineData("tab\there")]
    public void Validate_RejectsInvalidCharacters(string name)
    {
        Assert.Equal(FileNameValidator.InvalidCharactersMessage, FileNameValidator.Validate(name));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("Com1")]
    [InlineData("LPT9.tar.gz")]
    public void Validate_RejectsDeviceNames(string name)
    {
        Assert.Equal("The specified device name is invalid.", FileNameValidator.Validate(name));
    }

    [Fact]
    public void Validate_RejectsEmptyAndTooLongNames()
    {
        Assert.Equal("You must type a file name.", FileNameValidator.Validate(""));
        Assert.Equal("The file name is too long.", FileNameValidator.Validate(new string('a', 256)));
        Assert.Null(FileNameValidator.Validate(new string('a', 255)));
    }

    [Theory]
    [InlineData("  notes.txt  ", "notes.txt")]
    [InlineData("notes...", "notes")]
    [InlineData("notes. . ", "notes")]
    [InlineData("...", "")]
    public void Normalize_TrimsSpacesAndTrailingDots(string name, string expected)
    {
        Assert.Equal(expected, FileNameValidator.Normalize(name));
    }

    [Theory]
    [InlineData("notes.txt", false, 5)]
    [InlineData("archive.tar.gz", false, 11)]
    [InlineData("README", false, 6)]
    [InlineData(".gitignore", false, 10)]
    [InlineData("Version 1.2", true, 11)]
    public void RenameSelectionLength_LeavesOutTheExtension(string name, bool isFolder, int expected)
    {
        Assert.Equal(expected, FileNameValidator.RenameSelectionLength(name, isFolder));
    }

    [Theory]
    [InlineData("a.txt", "b.txt", false)]
    [InlineData("a.txt", "a.TXT", false)]
    [InlineData("a.txt", "a.md", true)]
    [InlineData("a.txt", "a", true)]
    [InlineData("a", "b", false)]
    public void ChangesExtension(string oldName, string newName, bool expected)
    {
        Assert.Equal(expected, FileNameValidator.ChangesExtension(oldName, newName));
    }
}
