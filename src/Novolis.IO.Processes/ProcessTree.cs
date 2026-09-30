using System.Collections.Concurrent;
using System.Diagnostics;

namespace Novolis.IO.Processes;

/// <summary>Kills a process and its children.</summary>
public static class ProcessTree
{
    /// <summary>Kills the process tree for <paramref name="pid"/> (Windows: taskkill /T /F).</summary>
    public static void Kill(int pid)
    {
        if (pid <= 0)
            return;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {pid} /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });
                killer?.WaitForExit(5000);
            }
            else
            {
                try
                {
                    Process.GetProcessById(pid).Kill(entireProcessTree: true);
                }
                catch
                {
                    /* ignore */
                }
            }
        }
        catch
        {
            /* ignore */
        }
    }
}
