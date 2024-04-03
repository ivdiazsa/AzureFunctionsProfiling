using System;
using System.Diagnostics;

public class AppsContainer
{
    // ARGS[0]: Path of the App's dll
    // ARGS[1]: App's Entry Point Method
    // ARGS[2]: App's Entry Point Namespace
    // ARGS[3]: App's Entry Point Class
    // ARGS[4]: Path to the Native Host
    static int Main(string[] args)
    {
        string appPath = args[0];
        string appEntryPoint = args[1];
        string appNamespace = args[2];
        string appClass = args[3];
        string nativeHost = args[4];

        int guestAppExitCode = 0;
        using (Process launcher = new Process())
        {
            var lPsi = new ProcessStartInfo
            {
                ArgumentList = {
                    appName,
                    appEntryPoint,
                    appNamespace,
                    appClass
                },

                CreateNoWindow = true,
                FileName = nativeHost,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }

            launcher.StartInfo = lPsi;
            launcher.Start();

            Console.WriteLine(launcher.StandardOutput.ReadToEnd());
            launcher.WaitForExit();
            guestAppExitCode = launcher.ExitCode;
        }

        Console.WriteLine("\nThe Apps Container Launcher received an exit code of"
                          + $" {guestAppExitCode} from our guest app :)");
        Console.WriteLine("Exiting container now...");
        return 0;
    }
}
