using System.Reflection;

namespace TableCleaner;

/// <summary>Provides the shared visual identity for application chrome.</summary>
internal static class AppVisuals
{
    private const string StatusIconResourceName = "TableCleaner.Resources.app-icon-16.png";

    private static readonly Lazy<Icon> WindowIconValue = new(() =>
        Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        ?? throw new InvalidOperationException("The application icon could not be loaded."));

    private static readonly Lazy<Image> StatusIconValue = new(LoadStatusIcon);

    public static string DisplayVersion { get; } = LoadDisplayVersion();

    public static Icon WindowIcon => WindowIconValue.Value;

    public static Image StatusIcon => StatusIconValue.Value;

    private static string LoadDisplayVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var informational = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            return informational.Split('+', 2)[0];
        }

        return assembly?.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static Image LoadStatusIcon()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(StatusIconResourceName)
            ?? throw new InvalidOperationException($"Missing embedded icon resource: {StatusIconResourceName}");
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }
}
