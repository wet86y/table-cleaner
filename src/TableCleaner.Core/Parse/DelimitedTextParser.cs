using System.Text;

namespace TableCleaner.Services;

/// <summary>RFC 4180-style delimited text parser that preserves empty fields and quoted line breaks.</summary>
public static class DelimitedTextParser
{
    public static List<List<string>> Parse(string text, char delimiter, bool skipBlankRecords = true)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void FinishField()
        {
            record.Add(field.ToString());
            field.Clear();
        }

        void FinishRecord()
        {
            FinishField();
            if (!skipBlankRecords || record.Any(value => !string.IsNullOrWhiteSpace(value)))
                records.Add(record);
            record = new List<string>();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                FinishField();
            }
            else if ((ch == '\r' || ch == '\n') && !inQuotes)
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                FinishRecord();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (inQuotes)
            throw new InvalidDataException("Delimited text contains an unterminated quoted field.");

        if (field.Length > 0 || record.Count > 0)
            FinishRecord();

        return records;
    }

    public static List<string> ParseLine(string line, char delimiter)
    {
        var records = Parse(line, delimiter, skipBlankRecords: false);
        return records.Count == 0 ? new List<string> { "" } : records[0];
    }

    public static int CountDelimiterOutsideQuotes(string line, char delimiter)
    {
        var count = 0;
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    i++;
                else
                    inQuotes = !inQuotes;
            }
            else if (!inQuotes && line[i] == delimiter)
            {
                count++;
            }
        }
        return count;
    }
}
