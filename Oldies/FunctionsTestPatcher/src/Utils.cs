// File: Utils.cs

using System;
using System.Diagnostics;

public static class Utils
{
    public static void PrintBanner(string message)
    {
        Console.Write($"\n{new string('*', message.Length + 10)}\n");
        Console.Write($"{new string(' ', 5)}{message}{new string(' ', 5)}\n");
        Console.Write($"{new string('*', message.Length + 10)}\n");
    }

    // Naming it "System()" because it reminds me of Ruby's "system()" Kernel call.
    public static int System(string cmd, string args, string cwd = "")
    {
        int exitCode = -1;

        using Process sysCall = new Process();

        sysCall.StartInfo.FileName = cmd;
        sysCall.StartInfo.Arguments = args;
        sysCall.StartInfo.CreateNoWindow = true;
        sysCall.StartInfo.RedirectStandardOutput = true;
        sysCall.StartInfo.UseShellExecute = false;

        if (!string.IsNullOrEmpty(cwd))
            sysCall.StartInfo.WorkingDirectory = cwd;

        sysCall.Start();
        while (!sysCall.StandardOutput.EndOfStream)
        {
            Console.WriteLine(sysCall.StandardOutput.ReadLine());
        }
        sysCall.WaitForExit();

        exitCode = sysCall.ExitCode;
        return exitCode;
    }
}
