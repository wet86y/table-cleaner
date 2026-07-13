namespace TableCleaner.Models;

/// <summary>行列结构化数据，程序内部统一使用此模型</summary>
public class TableData
{
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();

    public int ColumnCount => Headers.Count;
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

    public int GetColIndex(string columnName) =>
        Headers.FindIndex(h => string.Equals(h, columnName, StringComparison.OrdinalIgnoreCase));

    public TableData Clone()
    {
        return new TableData
        {
            Headers = new List<string>(Headers),
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
