namespace TableCleaner.Models;

/// <summary>行列结构化数据，程序内部统一使用此模型</summary>
public class TableData
{
    public List<TableColumn> Columns { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();

    public int ColumnCount => Columns.Count;
    public int RowCount => Rows.Count;

    public string? this[int row, int col]
    {
        get
        {
            if (row < 0 || row >= Rows.Count) return null;
            if (col < 0 || col >= Rows[row].Count) return null;
            return Rows[row][col];
        }
    }

    public int GetColumnIndexById(string columnId) =>
        Columns.FindIndex(column => string.Equals(column.Id, columnId, StringComparison.Ordinal));

    public int ResolveColumnIndex(ColumnReference? reference)
    {
        if (reference is null || reference.Occurrence < 1)
            return -1;

        var occurrence = 0;
        for (var index = 0; index < Columns.Count; index++)
        {
            if (!string.Equals(Columns[index].Header, reference.Header, StringComparison.OrdinalIgnoreCase))
                continue;

            occurrence++;
            if (occurrence == reference.Occurrence)
                return index;
        }

        return -1;
    }

    public List<int> ResolveColumnIndexes(IEnumerable<ColumnReference>? references) =>
        references?
            .Select(ResolveColumnIndex)
            .Where(index => index >= 0)
            .Distinct()
            .ToList()
        ?? new List<int>();

    public ColumnReference GetColumnReference(int index)
    {
        if (index < 0 || index >= Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var header = Columns[index].Header;
        var occurrence = Columns
            .Take(index + 1)
            .Count(column => string.Equals(column.Header, header, StringComparison.OrdinalIgnoreCase));

        return new ColumnReference { Header = header, Occurrence = occurrence };
    }

    public string GetColumnDisplayName(int index)
    {
        if (index < 0 || index >= Columns.Count)
            return "";

        var header = Columns[index].Header;
        var duplicateCount = Columns.Count(column =>
            string.Equals(column.Header, header, StringComparison.OrdinalIgnoreCase));

        if (duplicateCount <= 1)
            return header;

        var reference = GetColumnReference(index);
        return $"{header}（第{reference.Occurrence}个同名列）";
    }

    public static List<TableColumn> CreateColumns(IEnumerable<string?> headers) =>
        headers.Select(TableColumn.Create).ToList();

    public TableData Clone()
    {
        return new TableData
        {
            Columns = Columns.Select(column => column.Clone()).ToList(),
            Rows = Rows.Select(r => new List<string>(r)).ToList()
        };
    }

    /// <summary>
    /// 清空指定 (row, col) 位置的单元格内容。忽略无效索引和序号列。
    /// 不删除行/列，仅清空值。
    /// </summary>
    public static void ClearCells(TableData data, List<(int row, int col)> cells)
    {
        if (data == null || cells == null || cells.Count == 0)
            return;

        foreach (var (ri, ci) in cells)
        {
            if (ri >= 0 && ri < data.RowCount && ci >= 0 && ci < data.ColumnCount)
            {
                if (ci < data.Rows[ri].Count)
                    data.Rows[ri][ci] = "";
            }
        }
    }
}
