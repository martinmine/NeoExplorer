using NeoExplorer.Core;

namespace NeoExplorer.Tests;

public class DropEffectsTests
{
    [Fact]
    public void Choose_MovesWithinADrive()
    {
        Assert.Equal(DropOperation.Move, DropEffects.Choose([@"C:\A\notes.txt"], @"C:\B", control: false, shift: false));
    }

    [Fact]
    public void Choose_CopiesToAnotherDrive()
    {
        Assert.Equal(DropOperation.Copy, DropEffects.Choose([@"C:\A\notes.txt"], @"D:\B", control: false, shift: false));
    }

    [Fact]
    public void Choose_CopiesIfAnyItemIsOnAnotherDrive()
    {
        Assert.Equal(DropOperation.Copy, DropEffects.Choose([@"C:\A\one.txt", @"D:\A\two.txt"], @"C:\B", control: false, shift: false));
    }

    [Fact]
    public void Choose_ControlCopiesAndShiftMoves()
    {
        Assert.Equal(DropOperation.Copy, DropEffects.Choose([@"C:\A\notes.txt"], @"C:\B", control: true, shift: false));
        Assert.Equal(DropOperation.Move, DropEffects.Choose([@"C:\A\notes.txt"], @"D:\B", control: false, shift: true));
    }

    [Fact]
    public void Choose_DoesNothingWhenMovingIntoTheSameFolder()
    {
        Assert.Equal(DropOperation.None, DropEffects.Choose([@"C:\A\notes.txt"], @"C:\A", control: false, shift: false));
        Assert.Equal(DropOperation.None, DropEffects.Choose([@"C:\A\notes.txt"], @"c:\a\", control: false, shift: false));
    }

    [Fact]
    public void Choose_CopiesIntoTheSameFolderWithControl()
    {
        Assert.Equal(DropOperation.Copy, DropEffects.Choose([@"C:\A\notes.txt"], @"C:\A", control: true, shift: false));
    }

    [Theory]
    [InlineData(@"C:\A\Photos")]
    [InlineData(@"C:\A\Photos\2026")]
    [InlineData(@"c:\a\photos\")]
    public void Choose_RejectsAFolderIntoItselfOrItsSubfolders(string target)
    {
        Assert.Equal(DropOperation.None, DropEffects.Choose([@"C:\A\Photos"], target, control: true, shift: false));
        Assert.Equal(DropOperation.None, DropEffects.Choose([@"C:\A\Photos"], target, control: false, shift: true));
    }

    [Fact]
    public void Choose_AllowsASiblingWithASimilarName()
    {
        Assert.Equal(DropOperation.Move, DropEffects.Choose([@"C:\A\Photos"], @"C:\A\Photos 2026", control: false, shift: false));
    }

    [Fact]
    public void Choose_RejectsNoItems()
    {
        Assert.Equal(DropOperation.None, DropEffects.Choose([], @"C:\B", control: false, shift: false));
    }

    [Fact]
    public void CanMove_AllowsItemsFromAnotherFolder()
    {
        Assert.True(DropEffects.CanMove([@"C:\A\notes.txt"], @"C:\B"));
        Assert.True(DropEffects.CanMove([@"C:\A\notes.txt", @"C:\B\other.txt"], @"C:\B"));
        Assert.False(DropEffects.CanMove([@"C:\B\notes.txt"], @"C:\B"));
    }

    [Theory]
    [InlineData(@"C:\", @"C:\Windows", true)]
    [InlineData(@"C:\Windows", @"C:\", false)]
    [InlineData(@"C:\", @"C:\", true)]
    [InlineData(@"C:\Users", @"C:\Users\Public", true)]
    [InlineData(@"C:\UsersX", @"C:\Users", false)]
    public void IsSameOrInside(string folder, string path, bool expected)
    {
        Assert.Equal(expected, DropEffects.IsSameOrInside(path, folder));
    }
}
