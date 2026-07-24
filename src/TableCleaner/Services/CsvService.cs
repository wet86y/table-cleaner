using System.Text;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>CSV 导入/导出</summary>
public static class CsvService
{
    static CsvService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static TableData? Import(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var text = ReadText(filePath);
        if (text == null) return null;

        var firstLineEnd = text.IndexOfAny(new[] { '\r', '\n' });
        var first = firstLineEnd >= 0 ? text[..firstLineEnd] : text;
        char delimiter = first.Contains('\t') ? '\t' : ',';

        List<List<string>> records;
        try { records = DelimitedTextParser.Parse(text, delimiter); }
        catch (InvalidDataException) { return null; }
        if (records.Count == 0) return null;

        return TabularDataBuilder.FromRecords(records, firstRecordIsHeader: true, generatedHeaderPrefix: "Col");
    }

    public static bool Export(TableData data, string filePath)
    {
        try
        {
            var normalized = ExportNormalizationService.Prepare(data);
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", normalized.Columns.Select(column =>
                column.Header).Select(value =>
                ExportNormalizationService.EscapeDelimitedField(value, ','))));

            foreach (var row in normalized.Rows)
            {
                var vals = row.Select(value =>
                    ExportNormalizationService.EscapeDelimitedField(value, ','));
                sb.AppendLine(string.Join(",", vals));
            }

            File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return true;
        }
        catch { return false; }
    }

    public static string? ReadText(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(
                    936,
                    EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback).GetString(bytes);
            }
        }
        catch
        {
            return null;
        }
    }

}
