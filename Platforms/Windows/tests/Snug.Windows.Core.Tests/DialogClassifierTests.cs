using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class DialogClassifierTests
{
    [Theory]
    [InlineData("#32770", "Save Changes", true)]
    [InlineData("#32770", "", true)]
    [InlineData("Chrome_WidgetWin_1", "Do you want to save this file?", true)]
    [InlineData("HwndWrapper", "保存更改", true)]
    [InlineData("CustomDialog", "确认退出", true)]
    [InlineData("CustomDialog", "Replace existing file?", true)]
    public void OwnedConfirmationDialogsAreDetected(string kind, string name, bool owned) =>
        Assert.True(DialogClassifier.IsAskingWindow(kind, name, owned));

    [Theory]
    [InlineData("#32770", "Save Changes", false)]
    [InlineData("Chrome_WidgetWin_1", "Save Changes", false)]
    [InlineData("Chrome_WidgetWin_1", "Google Chrome", true)]
    [InlineData("Notepad", "Document.txt - Notepad", true)]
    [InlineData("HwndWrapper", "", true)]
    [InlineData("HwndWrapper", "Opening a New Window", true)]
    public void OrdinaryWindowsAndUnownedPromptsAreNotTreatedAsAnswers(
        string kind, string name, bool owned) =>
        Assert.False(DialogClassifier.IsAskingWindow(kind, name, owned));
}
