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

        var duplicateIds = data.Columns
            .GroupBy(column => column.Id, StringComparer.Ordinal)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => string.IsNullOrWhiteSpace(group.Key) ? "(empty)" : group.Key)
            .ToList();
        if (duplicateIds.Count > 0)
            errors.Add($"Column IDs must be unique and non-empty: {string.Join(", ", duplicateIds.Take(5))}.");

        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            if (data.Rows[rowIndex].Count != data.Columns.Count)
            {
                errors.Add(
                    $"Row {rowIndex + 1} has {data.Rows[rowIndex].Count} cells, expected {data.Columns.Count}.");
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
