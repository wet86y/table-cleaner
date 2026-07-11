using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>合并同类行：按分组列去重，求和列数值累加，其他列去重后 + 连接</summary>
public static class MergeService
{
    public static TableData Merge(TableData source, List<string> groupColumns, List<string> sumColumns)
    {
        var groupIndices = groupColumns
            .Select(g => source.GetColIndex(g))
            .Where(i => i >= 0)
            .ToHashSet();

        var sumIndices = sumColumns
            .Select(s => source.GetColIndex(s))
            .Where(i => i >= 0)
            .ToHashSet();

        if (groupIndices.Count == 0)
            return CleaningService.Clone(source);

        var groups = new Dictionary<string, List<DataRow>>();
        // Assign row IDs for grouping
        for (int ri = 0; ri < source.RowCount; ri++)
        {
            var key = string.Join("|", groupIndices.Select(i => source.Rows[ri][i] ?? ""));
            if (!groups.ContainsKey(key))
                groups[key] = new List<DataRow>();
            groups[key].Add(new DataRow(source, ri));
        }

        var result = new TableData();
        result.Headers = new List<string>(source.Headers);

        foreach (var kvp in groups)
        {
            var rows = kvp.Value;
            var row = new List<string>(new string[source.ColumnCount]);

            // Group columns: use first row value
            foreach (int i in groupIndices)
                row[i] = rows[0].Values[i] ?? "";

            // Sum columns: sum numeric values
            foreach (int i in sumIndices)
            {
                double sum = 0;
                bool any = false;
                foreach (var r in rows)
                {
                    if (double.TryParse(r.Values[i]?.Replace(",", "") ?? "0", out var v))
                    { sum += v; any = true; }
                }
                row[i] = any ? sum.ToString() : "";
            }

            // Other columns: if all same, keep one; if differ, distinct + join with +
            for (int i = 0; i < source.ColumnCount; i++)
            {
                if (groupIndices.Contains(i) || sumIndices.Contains(i)) continue;

                var vals = rows
                    .Select(r => r.Values[i]?.Trim() ?? "")
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .ToList();

                row[i] = vals.Count switch
                {
                    0 => "",
                    1 => vals[0],
                    _ => string.Join("+", vals)
                };
            }

            result.Rows.Add(row);
        }

        return result;
    }

    private record DataRow(TableData Table, int Index)
    {
        public List<string> Values => Table.Rows[Index];
    }
}
