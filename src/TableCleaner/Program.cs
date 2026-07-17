using System.Text;
using System.Threading;
using System.Reflection;
using DesktopUpdateKit;
using TableCleaner.Forms;
using TableCleaner.Services;

namespace TableCleaner;

static class Program
{
    private const string SingleInstanceMutexName = @"Local\StupidTable.SingleInstance";
    private const string VerifyReleaseArgument = "--verify-release";
    private const string VerifyUiLayoutArgument = "--verify-ui-layout";
    private const string UpdaterStubResourceName = "DesktopUpdateKit.Resources.UpdaterStub.exe";

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Any(arg => string.Equals(arg, VerifyReleaseArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return VerifyReleaseBundle();
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        if (args.Any(arg => string.Equals(arg, VerifyUiLayoutArgument, StringComparison.OrdinalIgnoreCase)))
        {
            return AboutForm.VerifyUpdateLayouts() ? 0 : 20;
        }

        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            return 0;
        }

        if (TryNormalizeExecutableName())
        {
            return 0;
        }

        ConfigService.EnsureDirs();
        TryWriteUpdateHealthMarker(args);
        Application.Run(new MainForm());
        return AboutForm.ShutdownUpdateSessionAsync(TimeSpan.FromSeconds(30))
            .GetAwaiter()
            .GetResult()
            ? 0
            : 30;
    }

    private static int VerifyReleaseBundle()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly();
            using var resource = assembly?.GetManifestResourceStream(UpdaterStubResourceName);
            if (resource is null || resource.Length < 2)
            {
                return 10;
            }

            return resource.ReadByte() == 'M' && resource.ReadByte() == 'Z' ? 0 : 11;
        }
        catch
        {
            return 12;
        }
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
