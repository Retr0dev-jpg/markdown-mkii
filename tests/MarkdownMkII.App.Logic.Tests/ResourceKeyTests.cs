using MarkdownMkII.Services.Localization;

namespace MarkdownMkII.App.Logic.Tests;

public class ResourceKeyTests
{
    [Theory]
    [InlineData("WorkspaceNotes", "WorkspaceNotes")]
    [InlineData("CmdNew.Label", "CmdNew/Label")]
    [InlineData("CmdNew/Label", "CmdNew/Label")]
    [InlineData("WorkspaceSettings.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name",
        "WorkspaceSettings/[using:Microsoft.UI.Xaml.Automation]AutomationProperties/Name")]
    [InlineData("WorkspaceSettings.[using:Microsoft.UI.Xaml.Controls]ToolTipService.ToolTip",
        "WorkspaceSettings/[using:Microsoft.UI.Xaml.Controls]ToolTipService/ToolTip")]
    public void Converts_Resw_Keys_To_Compiled_Pri_Paths(string key, string expected)
    {
        Assert.Equal(expected, ResourceKey.ToPath(key));
    }
}
