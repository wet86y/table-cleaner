namespace TableCleaner.Services;

/// <summary>导入表头规范化：合并单元格拆分后的空表头向前填充，并保留重复表头。</summary>
public static class HeaderNormalizationService
{
    /// <summary>
    /// 规范化表头。
    /// - 空表头继承左侧最近的非空表头；
    /// - 第一列表头为空时使用 ColumnN；
    /// - 保留重复表头，不自动追加 _2/_3；
    /// - 可将 ExcelDataReader 生成的 ColumnN 视为空表头参与前向填充。
    /// </summary>
    public static List<string> Normalize(IEnumerable<string?> rawHeaders, bool treatGeneratedColumnNamesAsEmpty = false)
    {
        var result = new List<string>();
        string lastNonEmpty = "";

        foreach (var raw in rawHeaders)
        {
            var name = (raw ?? "").Trim();
            bool generatedColumnName = treatGeneratedColumnNamesAsEmpty && IsGeneratedColumnName(name);

            if (string.IsNullOrWhiteSpace(name) || generatedColumnName)
            {
                name = !string.IsNullOrWhiteSpace(lastNonEmpty)
                    ? lastNonEmpty
                    : $"Column{result.Count + 1}";
            }
            else
            {
                lastNonEmpty = name;
            }

            result.Add(name);
        }

        return result;
    }

    private static bool IsGeneratedColumnName(string name)
    {
        if (!name.StartsWith("Column", StringComparison.OrdinalIgnoreCase))
            return false;

        if (name.Length == "Column".Length)
            return false;

        return name["Column".Length..].All(char.IsDigit);
    }
}
