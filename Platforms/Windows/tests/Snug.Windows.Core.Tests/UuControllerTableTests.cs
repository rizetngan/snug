using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class UuControllerTableTests
{
    private static UuElement Text(string value) => new(UuElementKind.Text, value);
    private static UuElement Other(params UuElement[] children) => new(UuElementKind.Other, Children: children);
    private static UuElement Table(params UuElement[] rows) => new(UuElementKind.Table, Children: rows);
    private static UuElement Row(string name, string mode = "远程控制") =>
        new(UuElementKind.Row, Children: [Text(name), Text(mode), Text("连接时长 00:12")]);

    [Fact]
    public void OnlyLocalTableBesideExactTitleIsTrusted()
    {
        var otherDevice = Other(Text("其他设备"), Table(Row("iPhone 15")));
        var local = Other(Text("本机被控中"), Table(Row("DESKTOP-TEST"), Row("iPhone 13 Pro")));
        var result = UuControllerTable.Read([Other(otherDevice, local)]);
        Assert.Equal(2, result.Count);
        Assert.Equal(ControllerKind.Desktop, result[0].Kind);
        Assert.Equal(ControllerKind.Phone, result[1].Kind);
        Assert.DoesNotContain(result, item => item.Name == "iPhone 15");
    }

    [Fact]
    public void MatchingWordsOnDifferentPageDoNotCount()
    {
        Assert.Empty(UuControllerTable.Read([Other(Text("本机被控中"),
            Other(Table(Row("iPhone"))))]));
        Assert.Empty(UuControllerTable.Read([Other(Text("本机被控中正在查看其他电脑"),
            Table(Row("iPhone")))]));
    }

    [Fact]
    public void IgnoreFileTransferAndDeduplicateRows()
    {
        var local = Other(Text("本机被控中"), Table(Row("iPhone"), Row("iPhone"),
            Row("DESKTOP-TEST", "文件传输")));
        var result = UuControllerTable.Read([local]);
        Assert.Equal("iPhone", Assert.Single(result).Name);
    }

    [Fact]
    public void MissingTableProducesNoPositiveEvidence()
    {
        Assert.Empty(UuControllerTable.Read([]));
        Assert.Empty(UuControllerTable.Read([Other(Text("本机被控中"))]));
    }
}