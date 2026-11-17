using System.Diagnostics;
using System.Management;
using Snug.Windows.Core;

namespace Snug.Windows.Services;

/// <summary>Read-only process identity. WMI exposes creation time on UU's
/// service process even when a standard user cannot open its process handle.
/// DMTF creation time has microsecond precision; normalize both sides.</summary>
public static class UuServerIdentity
{
    public static long NormalizeTicks(long ticks)=>ticks-ticks%10;

    public static UuServerGeneration? ReadSingle()
    {
        var processes=Process.GetProcessesByName("GameViewerServer");
        try
        {
            if(processes.Length!=1)return null;
            int pid=processes[0].Id;
            try{return new(pid,NormalizeTicks(processes[0].StartTime.ToUniversalTime().Ticks));}
            catch(System.ComponentModel.Win32Exception){ }
            catch(InvalidOperationException){return null;}
            using var search=new ManagementObjectSearcher("root\\CIMV2",
                "SELECT ProcessId,CreationDate FROM Win32_Process WHERE Name='GameViewerServer.exe'");
            using var rows=search.Get();
            UuServerGeneration? result=null;
            foreach(ManagementObject row in rows)
            {
                using(row)
                {
                    if(result is not null||Convert.ToInt32(row["ProcessId"])!=pid||
                        row["CreationDate"] is not string created)return null;
                    result=new(pid,NormalizeTicks(ManagementDateTimeConverter.ToDateTime(created).ToUniversalTime().Ticks));
                }
            }
            return result;
        }
        catch(Exception e) when(e is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception or
            InvalidOperationException or ArgumentException){return null;}
        finally{foreach(var process in processes)process.Dispose();}
    }
}