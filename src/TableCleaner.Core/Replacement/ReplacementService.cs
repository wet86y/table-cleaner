using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>替换服务：全表或指定列查找替换，支持普通替换和拓展替换（覆盖/插值）</summary>
public static class ReplacementService
{
    /// <summary>应用替换规则到数据</summary>
    /// <param name="source">源数据</param>
    /// <param name="rules">替换规则列表</param>
    /// <param name="scopeColumns">如果非 null，仅替换这些列（多列）</param>
    public static TableData Apply(
        TableData source,
        List<ReplacementRule> rules,
        IReadOnlyList<ColumnReference>? scopeColumns = null)
    {
        TableDataValidator.EnsureValid(source, "Replacement input");
        var result = CleaningService.Clone(source);

        var colIndices = scopeColumns is { Count: > 0 }
            ? result.ResolveColumnIndexes(scopeColumns)
            : Enumerable.Range(0, result.ColumnCount).ToList();

        if (colIndices.Count == 0) return result;

        foreach (var rule in rules)
        {
            if (!rule.Enabled || string.IsNullOrEmpty(rule.Before)) continue;

            var effectiveCols = rule.Scope is not null
                ? new List<int> { result.ResolveColumnIndex(rule.Scope) }.Where(i => i >= 0).ToList()
                : colIndices;

            foreach (int ci in effectiveCols)
            {
                for (int ri = 0; ri < result.RowCount; ri++)
                {
                    var val = result.Rows[ri][ci] ?? "";
                    if (val.Contains(rule.Before))
                        result.Rows[ri][ci] = val.Replace(rule.Before, rule.After);
                }
            }
        }

        TableDataValidator.EnsureValid(result, "Replacement");
        return result;
    }

    /// <summary>应用分组级替换配置（作用列 + 匹配模式 + 替换类型）</summary>
    /// <param name="source">源数据</param>
    /// <param name="group">替换分组</param>
    public static TableData ApplyGroup(TableData source, ReplacementGroup group)
    {
        TableDataValidator.EnsureValid(source, "Replacement input");
        var result = CleaningService.Clone(source);

        // 1. 从 group 读取作用列
        var colIndices = group.ScopeColumns is { Count: > 0 }
            ? result.ResolveColumnIndexes(group.ScopeColumns)
            : Enumerable.Range(0, result.ColumnCount).ToList();

        if (colIndices.Count == 0) return result;

        // 2. 读取匹配模式
        bool exactMatch = group.MatchMode == ReplacementMatchMode.Exact;

        // 3. 遍历启用的规则
        foreach (var rule in group.Rules)
        {
            if (!rule.Enabled || string.IsNullOrEmpty(rule.Before)) continue;

            // 区分普通替换和拓展替换（按分组级 Type 决定，非规则级）
            if (group.Type == ReplacementType.Extended && rule.ExtraValues.Count > 0)
            {
                // 拓展替换：按 group 写入模式执行
                ApplyExtendedRule(result, colIndices, exactMatch, group, rule);
            }
            else
            {
                // 普通替换（兼容旧行为）
                ApplyNormalRule(result, colIndices, exactMatch, rule);
            }
        }

        TableDataValidator.EnsureValid(result, "Replacement");
        return result;
    }

    /// <summary>普通替换：Contains + Replace 或 Exact 匹配</summary>
    private static void ApplyNormalRule(TableData data, List<int> colIndices, bool exactMatch, ReplacementRule rule)
    {
        foreach (int ci in colIndices)
        {
            for (int ri = 0; ri < data.RowCount; ri++)
            {
                var val = data.Rows[ri][ci] ?? "";

                if (exactMatch)
                {
                    if (val == rule.Before)
                        data.Rows[ri][ci] = rule.After;
                }
                else
                {
                    if (val.Contains(rule.Before))
                        data.Rows[ri][ci] = val.Replace(rule.Before, rule.After);
                }
            }
        }
    }

    /// <summary>拓展替换：写入模式（覆盖 / 插值）</summary>
    private static void ApplyExtendedRule(TableData data, List<int> colIndices, bool exactMatch,
        ReplacementGroup group, ReplacementRule rule)
    {
        var before = rule.Before;
        var after = rule.After;
        var extraValues = rule.ExtraValues;
        int exCount = extraValues.Count;

        if (group.ExtendWriteMode == ExtendWriteMode.Overwrite)
        {
            // === 覆盖模式：命中单元格写替换后，右侧连续单元格被扩展值覆盖 ===
            foreach (int ci in colIndices)
            {
                for (int ri = 0; ri < data.RowCount; ri++)
                {
                    var val = data.Rows[ri][ci] ?? "";
                    bool matched = exactMatch ? val == before : val.Contains(before);
                    if (!matched) continue;

                    // 替换命中单元格
                    data.Rows[ri][ci] = exactMatch ? after : val.Replace(before, after);

                    // 右侧连续单元格被扩展值覆盖
                    for (int ei = 0; ei < exCount && ci + 1 + ei < data.ColumnCount; ei++)
                    {
                        data.Rows[ri][ci + 1 + ei] = extraValues[ei];
                    }
                }
            }
        }
        else
        {
            // === 插值模式：在命中列右侧插入空列，再写入扩展值，保护原右侧数据 ===
            // 第一步：收集所有命中位置，按列分组
            var hitsByCol = new Dictionary<int, List<int>>();
            foreach (int ci in colIndices)
            {
                var hitRows = new List<int>();
                for (int ri = 0; ri < data.RowCount; ri++)
                {
                    var val = data.Rows[ri][ci] ?? "";
                    bool matched = exactMatch ? val == before : val.Contains(before);
                    if (matched) hitRows.Add(ri);
                }
                if (hitRows.Count > 0)
                    hitsByCol[ci] = hitRows;
            }

            if (hitsByCol.Count == 0) return;

            // 第二步：按列索引降序排列，从右向左插入，避免索引错乱
            // 同一原始列多行命中只插入一次扩展列
            var sortedCols = hitsByCol.Keys.OrderByDescending(c => c).ToList();

            foreach (int ci in sortedCols)
            {
                var hitRows = hitsByCol[ci];

                // 在 ci 右侧插入 exCount 个空列
                for (int ei = 0; ei < exCount; ei++)
                {
                    int insertAt = ci + 1;
                    if (insertAt <= data.ColumnCount)
                    {
                        var colName = group.ExtraColumnNames is { Count: > 0 } && ei < group.ExtraColumnNames.Count
                            ? group.ExtraColumnNames[ei]
                            : $"扩展{ei + 1}";
                        insertAt += ei;
                        data.Columns.Insert(insertAt, TableColumn.Create(colName));
                        for (int ri = 0; ri < data.RowCount; ri++)
                        {
                            if (insertAt <= data.Rows[ri].Count)
                                data.Rows[ri].Insert(insertAt, "");
                        }
                    }
                }

                // Keep the original target-column identities stable for subsequent rules.
                for (var index = 0; index < colIndices.Count; index++)
                {
                    if (colIndices[index] > ci)
                        colIndices[index] += exCount;
                }

                // 第三步：写入替换值和扩展值
                // 注意：插入后原始列 ci 仍在原位，扩展列在 ci+1..ci+exCount
                foreach (int ri in hitRows)
                {
                    var val = data.Rows[ri][ci] ?? "";
                    data.Rows[ri][ci] = exactMatch ? after : val.Replace(before, after);

                    for (int ei = 0; ei < exCount; ei++)
                    {
                        if (ci + 1 + ei < data.Rows[ri].Count)
                            data.Rows[ri][ci + 1 + ei] = extraValues[ei];
                    }
                }
            }
        }
    }
}
