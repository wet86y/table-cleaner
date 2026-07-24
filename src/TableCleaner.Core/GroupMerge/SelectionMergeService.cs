using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>
/// 离散数据合并服务：将选中多列/多行中分散的非空值归纳到目标列/行。
/// 支持列合并和行合并，一次只能执行一种操作。
/// </summary>
public static class SelectionMergeService
{
    /// <summary>
    /// 列合并：在选中的多列范围内，把每行中分散的非空值归纳到目标列。
    /// 目标列为选中列中"非空单元格最多"的列；若并列，使用最左列。
    /// 每一行取该行选中列范围内第一个非空值（trim 后），写入目标列；其他选中列对应单元格清空。
    /// 最后删除选中范围内变成全空的列。
    /// </summary>
    /// <param name="source">源数据</param>
    /// <param name="selectedColumnIndices">选中的列索引（0-based 数据列索引，非 DGV 序号后列索引）</param>
    /// <returns>合并后的数据，或 null 如果参数无效</returns>
    public static TableData? MergeColumns(TableData source, List<int> selectedColumnIndices)
    {
        if (source == null || selectedColumnIndices == null || selectedColumnIndices.Count < 2)
            return null;

        TableDataValidator.EnsureValid(source, "Selection column merge input");

        // Validate indices
        var validIndices = selectedColumnIndices
            .Where(i => i >= 0 && i < source.ColumnCount)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        if (validIndices.Count < 2)
            return null;

        var result = source.Clone();

        // Find target column: column with most non-empty cells; if tied, use leftmost
        var nonEmptyCounts = validIndices
            .Select(colIdx => new
            {
                Index = colIdx,
                Count = result.Rows.Count(r => colIdx < r.Count && !string.IsNullOrWhiteSpace(r[colIdx]))
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Index)
            .ToList();

        int targetCol = nonEmptyCounts.First().Index;
        var otherCols = validIndices.Where(i => i != targetCol).ToList();

        // For each row: find first non-empty value (trimmed) in selected range, write to target
        for (int ri = 0; ri < result.RowCount; ri++)
        {
            string? firstValue = null;

            foreach (int ci in validIndices)
            {
                if (ci < result.Rows[ri].Count)
                {
                    var val = result.Rows[ri][ci]?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        firstValue = val;
                        break;
                    }
                }
            }

            // Write value to target column
            if (targetCol < result.Rows[ri].Count)
                result.Rows[ri][targetCol] = firstValue ?? "";

            // Clear other selected columns
            foreach (int ci in otherCols)
            {
                if (ci < result.Rows[ri].Count)
                    result.Rows[ri][ci] = "";
            }
        }

        // v2.4.2: no longer remove columns that became empty; keep structure intact.
        TableDataValidator.EnsureValid(result, "Selection column merge");
        return result;
    }

    /// <summary>
    /// 行合并：在选中的多行范围内，把每列中分散的非空值归纳到目标行。
    /// 目标行为选中行中"非空单元格最多"的行；若并列，使用最上行。
    /// 每一列取选中行范围内第一个非空值（trim 后），写入目标行；其他选中行对应单元格清空。
    /// 最后删除选中范围内变成全空的行。
    /// </summary>
    /// <param name="source">源数据</param>
    /// <param name="selectedRowIndices">选中的行索引（0-based 数据行索引）</param>
    /// <returns>合并后的数据，或 null 如果参数无效</returns>
    public static TableData? MergeRows(TableData source, List<int> selectedRowIndices)
    {
        if (source == null || selectedRowIndices == null || selectedRowIndices.Count < 2)
            return null;

        TableDataValidator.EnsureValid(source, "Selection row merge input");

        // Validate indices
        var validIndices = selectedRowIndices
            .Where(i => i >= 0 && i < source.RowCount)
            .Distinct()
            .OrderBy(i => i)
            .ToList();

        if (validIndices.Count < 2)
            return null;

        var result = source.Clone();

        // Find target row: row with most non-empty cells; if tied, use topmost
        var nonEmptyCounts = validIndices
            .Select(rowIdx => new
            {
                Index = rowIdx,
                Count = Enumerable.Range(0, result.ColumnCount)
                    .Count(c => rowIdx < result.Rows.Count && c < result.Rows[rowIdx].Count &&
                                !string.IsNullOrWhiteSpace(result.Rows[rowIdx][c]))
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Index)
            .ToList();

        int targetRow = nonEmptyCounts.First().Index;
        var otherRows = validIndices.Where(i => i != targetRow).ToList();

        // For each column: find first non-empty value (trimmed) across selected rows
        for (int ci = 0; ci < result.ColumnCount; ci++)
        {
            string? firstValue = null;

            foreach (int ri in validIndices)
            {
                if (ri < result.Rows.Count && ci < result.Rows[ri].Count)
                {
                    var val = result.Rows[ri][ci]?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        firstValue = val;
                        break;
                    }
                }
            }

            // Write value to target row
            if (targetRow < result.Rows.Count && ci < result.Rows[targetRow].Count)
                result.Rows[targetRow][ci] = firstValue ?? "";

            // Clear other selected rows
            foreach (int ri in otherRows)
            {
                if (ri < result.Rows.Count && ci < result.Rows[ri].Count)
                    result.Rows[ri][ci] = "";
            }
        }

        // v2.4.2: no longer remove rows that became empty; keep structure intact.
        TableDataValidator.EnsureValid(result, "Selection row merge");
        return result;
    }

    /// <summary>
    /// 全局清洗空行空列（忽略表头）。
    /// - 空行：一行中所有数据列都为空（不含序号列），则删除该行。表头行本身不检查。
    /// - 空列：一列中所有数据行都为空，则删除该列。即使表头非空也会被删。
    /// - 清空列判断忽略表头行。
    /// - 若全部行/列被删除则保留空 TableData（仅 heades 和 0 行）。
    /// </summary>
    public static TableData RemoveEmptyRowsAndColumns(TableData source)
    {
        if (source == null) return new TableData();

        TableDataValidator.EnsureValid(source, "Empty row/column cleanup input");

        var result = source.Clone();

        if (result.RowCount == 0 || result.ColumnCount == 0)
            return result;

        // Phase 1: remove empty rows (skip header row, check all data columns)
        var rowsToKeep = new List<List<string>>();
        foreach (var row in result.Rows)
        {
            bool allEmpty = row.All(cell => string.IsNullOrWhiteSpace(cell));
            if (!allEmpty)
                rowsToKeep.Add(row);
        }
        result.Rows = rowsToKeep;

        // Phase 2: remove empty columns (check all data rows, header is ignored)
        if (result.Rows.Count == 0)
            return result;

        var colsToRemove = new List<int>();
        for (int c = result.ColumnCount - 1; c >= 0; c--)
        {
            bool allDataEmpty = result.Rows.All(row => c >= row.Count || string.IsNullOrWhiteSpace(row[c]));
            if (allDataEmpty)
                colsToRemove.Add(c);
        }

        foreach (int ci in colsToRemove)
        {
            result.Columns.RemoveAt(ci);
            foreach (var row in result.Rows)
            {
                if (ci < row.Count)
                    row.RemoveAt(ci);
            }
        }

        TableDataValidator.EnsureValid(result, "Empty row/column cleanup");
        return result;
    }
}
