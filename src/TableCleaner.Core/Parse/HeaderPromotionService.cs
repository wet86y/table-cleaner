using TableCleaner.Models;

namespace TableCleaner.Services;

public static class HeaderPromotionService
{
    /// <summary>把第一条数据行提升为表头；保留列 ID，返回 false 表示没有数据行。</summary>
    public static bool PromoteFirstRow(TableData table)
    {
        ArgumentNullException.ThrowIfNull(table);
        TableDataValidator.EnsureValid(table, "Header promotion input");
        if (table.Rows.Count == 0)
            return false;

        var firstRow = table.Rows[0];
        var headers = HeaderNormalizationService.Normalize(
            Enumerable.Range(0, table.ColumnCount)
                .Select(index => index < firstRow.Count ? firstRow[index] : ""));

        for (var index = 0; index < table.ColumnCount; index++)
            table.Columns[index].Header = headers[index];
        table.Rows.RemoveAt(0);

        TableDataValidator.EnsureValid(table, "Header promotion");
        return true;
    }
}
