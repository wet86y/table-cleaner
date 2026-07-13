using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>按保留列清洗</summary>
public static class CleaningService
{
    /// <summary>只保留指定列</summary>
    public static TableData KeepColumns(TableData source, List<string> kept)
    {
        TableDataValidator.EnsureValid(source, "Column selection input");
        if (kept.Count == source.ColumnCount &&
            source.Headers.All(h => kept.Contains(h, StringComparer.OrdinalIgnoreCase)))
            return Clone(source);

        var indices = kept
            .Select(k => source.GetColIndex(k))
            .Where(i => i >= 0)
            .ToList();

        var result = new TableData();
        result.Headers = indices.Select(i => source.Headers[i]).ToList();

        foreach (var row in source.Rows)
        {
            var newRow = indices.Select(i => i < row.Count ? row[i] ?? "" : "").ToList();
            result.Rows.Add(newRow);
        }
        TableDataValidator.EnsureValid(result, "Column selection");
        return result;
    }

    public static TableData Clone(TableData source)
    {
        var result = new TableData();
        result.Headers = new List<string>(source.Headers);
        foreach (var row in source.Rows)
            result.Rows.Add(new List<string>(row));
        return result;
    }
}
