using System.Text;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>剪切板 TSV/CSV 导入</summary>
public static class ClipboardImportService
{
    /// <summary>读取剪切板文本并解析为 TableData</summary>
    public static TableData? Import()
    {
        if (!Clipboard.ContainsText()) return null;

        var text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return null;

        char delimiter = DetectDelimiter(lines[0]);
        return ParseDelimitedText(lines, delimiter);
    }

    private static char DetectDelimiter(string firstLine)
    {
        int tabCount = firstLine.Count(c => c == '\t');
        int commaCount = firstLine.Count(c => c == ',');

        if (tabCount >= commaCount && tabCount > 0) return '\t';
        if (commaCount > 0) return ',';
        return '\t';
    }

    private static TableData ParseDelimitedText(string[] lines, char delimiter)
    {
        var result = new TableData();
        // Safe parse for empty fields
        var rawHeaders = SplitLine(lines[0], delimiter);
        result.Headers = HeaderNormalizationService.Normalize(rawHeaders);

        // If only 1 line, it's both header and data
        int startRow = lines.Length == 1 ? 0 : 1;
        for (int i = startRow; i < lines.Length; i++)
        {
            var fields = SplitLine(lines[i], delimiter);
            var row = new List<string>(new string[result.ColumnCount]);
            for (int j = 0; j < fields.Length && j < result.ColumnCount; j++)
                row[j] = fields[j].Trim();
            result.Rows.Add(row);
        }
        return result;
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
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}
