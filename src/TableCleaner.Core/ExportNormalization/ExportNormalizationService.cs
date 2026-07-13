using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>Creates a validated, stable snapshot for export adapters.</summary>
public static class ExportNormalizationService
{
    public static TableData Prepare(TableData source)
    {
        TableDataValidator.EnsureValid(source, "Export input");
        return source.Clone();
    }

    public static string EscapeDelimitedField(string? value, char delimiter)
    {
        var field = value ?? "";
        if (field.Contains(delimiter) || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
