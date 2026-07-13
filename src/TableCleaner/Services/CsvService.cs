using System.Text;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>CSV 导入/导出</summary>
public static class CsvService
{
    public static TableData? Import(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        string text;
        try { text = File.ReadAllText(filePath, Encoding.UTF8); }
        catch { return null; }

        var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
        var first = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
        char delimiter = first.Contains('\t') ? '\t' : ',';

        List<List<string>> records;
        try { records = DelimitedTextParser.Parse(text, delimiter); }
        catch (InvalidDataException) { return null; }
        if (records.Count == 0) return null;

        var result = new TableData();
        var rawHeaders = records[0];
        foreach (var h in rawHeaders)
            result.Headers.Add(string.IsNullOrWhiteSpace(h) ? $"Col{result.Headers.Count + 1}" : h.Trim());

        for (int i = 1; i < records.Count; i++)
        {
            var fields = records[i];
            var row = new List<string>(new string[result.ColumnCount]);
            for (int j = 0; j < fields.Count && j < result.ColumnCount; j++)
                row[j] = fields[j].Trim();
            result.Rows.Add(row);
        }
        TableDataValidator.EnsureValid(result, "CSV import");
        return result;
    }

    public static bool Export(TableData data, string filePath)
    {
        try
        {
            TableDataValidator.EnsureValid(data, "CSV export input");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", data.Headers.Select(Escape)));

            foreach (var row in data.Rows)
            {
                var vals = row.Select(v => Escape(v ?? "")).ToList();
                // Pad if short
                while (vals.Count < data.ColumnCount) vals.Add("");
                sb.AppendLine(string.Join(",", vals.Take(data.ColumnCount)));
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            return true;
        }
        catch { return false; }
    }

    private static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }

}
