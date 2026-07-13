using TableCleaner.Models;

namespace TableCleaner.Services;

public static class TableDataValidator
{
    public static IReadOnlyList<string> Validate(TableData? data)
    {
        var errors = new List<string>();
        if (data is null)
        {
            errors.Add("Table data is null.");
            return errors;
        }

        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            if (data.Rows[rowIndex].Count != data.Headers.Count)
            {
                errors.Add(
                    $"Row {rowIndex + 1} has {data.Rows[rowIndex].Count} cells, expected {data.Headers.Count}.");
            }
        }

        return errors;
    }

    public static void EnsureValid(TableData data, string operation)
    {
        var errors = Validate(data);
        if (errors.Count > 0)
            throw new InvalidDataException($"{operation} produced invalid table data: {string.Join(" ", errors.Take(5))}");
    }
}
