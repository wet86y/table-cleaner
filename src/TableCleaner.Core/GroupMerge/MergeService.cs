using TableCleaner.Models;
using System.Globalization;
using System.Text;

namespace TableCleaner.Services;

/// <summary>合并同类行：按分组列去重，求和列数值累加，其他列去重后 + 连接</summary>
public static class MergeService
{
    public static TableData Merge(TableData source, List<string> groupColumns, List<string> sumColumns)
    {
        TableDataValidator.EnsureValid(source, "Group merge input");
        var groupIndices = groupColumns
            .Select(g => source.GetColIndex(g))
            .Where(i => i >= 0)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        var sumIndices = sumColumns
            .Select(s => source.GetColIndex(s))
            .Where(i => i >= 0)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        if (groupIndices.Count == 0)
            return CleaningService.Clone(source);

        var overlappingIndex = groupIndices.Intersect(sumIndices).FirstOrDefault(-1);
        if (overlappingIndex >= 0)
            throw new InvalidDataException($"列“{source.Headers[overlappingIndex]}”不能同时作为分组列和求和列。");

        var groups = new Dictionary<string, List<DataRow>>();
        // Assign row IDs for grouping
        for (int ri = 0; ri < source.RowCount; ri++)
        {
            var key = BuildGroupKey(groupIndices.Select(i => GetCell(source.Rows[ri], i)));
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
                decimal sum = 0;
                bool any = false;
                foreach (var r in rows)
                {
                    var raw = GetCell(r.Values, i).Trim();
                    if (string.IsNullOrEmpty(raw)) continue;
                    if (!TryParseNumber(raw, out var value))
                        throw new InvalidDataException($"列“{source.Headers[i]}”包含无法求和的值“{raw}”。合并已取消，原数据未修改。");
                    sum += value;
                    any = true;
                }
                row[i] = any ? sum.ToString(CultureInfo.InvariantCulture) : "";
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

        TableDataValidator.EnsureValid(result, "Group merge");
        return result;
    }

    private record DataRow(TableData Table, int Index)
    {
        public List<string> Values => Table.Rows[Index];
    }

    private static string GetCell(List<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index] ?? "" : "";

    private static string BuildGroupKey(IEnumerable<string> values)
    {
        var builder = new StringBuilder();
        foreach (var value in values)
            builder.Append(value.Length).Append(':').Append(value);
        return builder.ToString();
    }

    private static bool TryParseNumber(string value, out decimal number)
    {
        const NumberStyles styles = NumberStyles.Number;
        return decimal.TryParse(value, styles, CultureInfo.CurrentCulture, out number) ||
               decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out number);
    }
}
