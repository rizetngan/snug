using System.IO;
using System.Text.Json;

namespace Snug.Windows.Services;

internal static class UuCaptureAcceptanceAudit
{
    private static readonly object Gate=new();
    private static StreamWriter? _writer;
    private static string? _path;

    internal static void Record(string name,object value)
    {
        var configured=Environment.GetEnvironmentVariable("SNUG_UU_CAPTURE_PILOT_AUDIT");
        if(string.IsNullOrWhiteSpace(configured))return;
        var path=configured+".app.jsonl";
        lock(Gate)
        {
            try
            {
                if(_writer is null)
                {
                    _path=path;
                    _writer=new StreamWriter(new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                    {AutoFlush=true};
                }
                if(_path!=path)return;
                _writer.WriteLine(JsonSerializer.Serialize(new{utc=DateTimeOffset.UtcNow,name,value}));
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    internal static void Close()
    {
        lock(Gate){try{_writer?.Dispose();}catch(IOException){} _writer=null;_path=null;}
    }
}