using System.Text;
using System.Threading;
using DesktopUpdateKit;
using TableCleaner.Forms;
using TableCleaner.Services;

namespace TableCleaner;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\StupidTable.SingleInstance";

    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();

        if (TryNormalizeExecutableName())
        {
            return;
        }

        ConfigService.EnsureDirs();
        TryWriteUpdateHealthMarker(args);
        Application.Run(new MainForm());
    }

    private static bool TryNormalizeExecutableName()
    {
        try
        {
            return new UpdateLauncher()
                .EnsureCanonicalExecutableNameAsync("笨蛋表格.exe")
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // A read-only or protected install location must not prevent the
            // application from starting under its current name.
            return false;
        }
    }

    private static void TryWriteUpdateHealthMarker(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--update-health", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var markerPath = args[i + 1];
            try
            {
                var directory = Path.GetDirectoryName(markerPath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                Directory.CreateDirectory(directory);
                var tempPath = markerPath + $".tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
                File.WriteAllText(tempPath, "ok");
                File.Move(tempPath, markerPath, overwrite: true);
            }
            catch
            {
                // A missing health marker causes UpdaterStub to roll back.
            }

            return;
        }
    }
}
