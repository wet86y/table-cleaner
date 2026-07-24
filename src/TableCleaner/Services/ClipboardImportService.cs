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
        return ImportText(text);
    }

    public static TableData? ImportText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var records = ParseRecords(text);
        if (records.Count == 0) return null;

        // A single copied row is data, not both a header and a duplicated data row.
        return TabularDataBuilder.FromRecords(records, firstRecordIsHeader: records.Count > 1);
    }

    public static List<List<string>> ParseRecords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<List<string>>();

        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        var delimiter = DetectDelimiter(text);
        return DelimitedTextParser.Parse(text, delimiter);
    }

    private static char DetectDelimiter(string text)
    {
        var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
        var firstLine = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
        var tabColumns = DelimitedTextParser.ParseLine(firstLine, '\t').Count;
        var commaColumns = DelimitedTextParser.ParseLine(firstLine, ',').Count;

        return tabColumns >= commaColumns && tabColumns > 1 ? '\t' : ',';
    }
}
