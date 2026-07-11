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

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        var first = lines[0];
        char delimiter = first.Contains('\t') ? '\t' : ',';

        var result = new TableData();
        var rawHeaders = SplitLine(lines[0], delimiter);
        foreach (var h in rawHeaders)
            result.Headers.Add(string.IsNullOrWhiteSpace(h) ? $"Col{result.Headers.Count + 1}" : h.Trim());

        for (int i = 1; i < lines.Length; i++)
        {
            var fields = SplitLine(lines[i], delimiter);
            var row = new List<string>(new string[result.ColumnCount]);
            for (int j = 0; j < fields.Length && j < result.ColumnCount; j++)
                row[j] = fields[j].Trim();
            result.Rows.Add(row);
        }
        return result;
    }

    public static bool Export(TableData data, string filePath)
    {
        try
        {
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

    private static string[] SplitLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString()); current.Clear();
            }
            else { current.Append(c); }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
