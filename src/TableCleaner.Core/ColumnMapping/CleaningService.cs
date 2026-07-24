using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>按保留列清洗</summary>
public static class CleaningService
{
    /// <summary>只保留指定列</summary>
    public static TableData KeepColumns(TableData source, IReadOnlyList<ColumnReference> kept)
    {
        TableDataValidator.EnsureValid(source, "Column selection input");
        var indices = source.ResolveColumnIndexes(kept);

        var result = new TableData
        {
            Columns = indices.Select(i => source.Columns[i].Clone()).ToList()
        };

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
        return source.Clone();
    }
}
