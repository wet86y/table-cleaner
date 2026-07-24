using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>Builds rectangular table data from parsed delimited records without dropping cells.</summary>
public static class TabularDataBuilder
{
    public static TableData? FromRecords(
        IReadOnlyList<List<string>> records,
        bool firstRecordIsHeader,
        string generatedHeaderPrefix = "Column")
    {
        if (records.Count == 0)
            return null;

        var maxWidth = records.Max(record => record.Count);
        if (maxWidth == 0)
            return null;

        var result = new TableData();
        var firstDataIndex = 0;

        if (firstRecordIsHeader)
        {
            var headers = HeaderNormalizationService.Normalize(
                Enumerable.Range(0, maxWidth)
                    .Select(index => index < records[0].Count
                        ? records[0][index]
                        : $"{generatedHeaderPrefix}{index + 1}"));
            result.Columns = TableData.CreateColumns(headers);
            firstDataIndex = 1;
        }
        else
        {
            result.Columns = TableData.CreateColumns(
                Enumerable.Range(1, maxWidth)
                    .Select(index => $"{generatedHeaderPrefix}{index}"));
        }

        for (var recordIndex = firstDataIndex; recordIndex < records.Count; recordIndex++)
        {
            var record = records[recordIndex];
            var row = new List<string>(maxWidth);
            for (var columnIndex = 0; columnIndex < maxWidth; columnIndex++)
            {
                row.Add(columnIndex < record.Count ? record[columnIndex]?.Trim() ?? "" : "");
            }
            result.Rows.Add(row);
        }

        TableDataValidator.EnsureValid(result, "Delimited import");
        return result;
    }
}
