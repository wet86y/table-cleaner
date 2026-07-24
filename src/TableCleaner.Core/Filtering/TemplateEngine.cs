using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>模板匹配引擎：根据筛选条件匹配数据表，并应用清洗模板</summary>
public static class TemplateEngine
{
    /// <summary>根据 CleanTemplateFilter.MatchItems 匹配数据表</summary>
    public static bool MatchFilter(TableData table, CleanTemplateFilter filter, Action<string>? logger = null)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (filter == null) throw new ArgumentNullException(nameof(filter));
        TableDataValidator.EnsureValid(table, "Template filter input");

        var enabledItems = filter.MatchItems;
        if (enabledItems.Count == 0)
        {
            logger?.Invoke("[MatchFilter] 筛选模板无匹配条件，默认不匹配");
            return false;
        }

        foreach (var item in enabledItems)
        {
            int colIndex = table.ResolveColumnIndex(item.Column);
            if (colIndex < 0)
            {
                logger?.Invoke($"[MatchFilter] 列 '{item.Column}' 不存在 → 不匹配");
                return false;
            }

            if (table.RowCount == 0)
            {
                logger?.Invoke("[MatchFilter] 数据表无数据行 → 不匹配");
                return false;
            }

            var cellValue = table.Rows[0][colIndex] ?? "";
            bool matched = string.Equals(cellValue.Trim(), item.Value.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!matched)
            {
                logger?.Invoke($"[MatchFilter] 列 '{item.Column}' 值 '{cellValue}' 不匹配等值 '{item.Value}'");
                return false;
            }

            logger?.Invoke($"[MatchFilter] 列 '{item.Column}' 值 '{cellValue}' 匹配等值 '{item.Value}'");
        }

        logger?.Invoke("[MatchFilter] 所有条件匹配通过");
        return true;
    }

    /// <summary>
    /// 应用普通模板到数据表。
    /// replacementGroups 是兼容旧调用的保留参数；模板库已与替换库解耦，此参数不再自动执行替换。
    /// </summary>
    public static TableData ApplyTemplate(TableData table, CleanTemplate template, List<ReplacementGroup>? replacementGroups = null, Action<string>? logger = null)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (template == null) throw new ArgumentNullException(nameof(template));
        TableDataValidator.EnsureValid(table, "Template input");

        if (template.TargetHeaders.Count == 0)
        {
            logger?.Invoke("[ApplyTemplate] 模板没有定义目标表头，返回空表");
            return new TableData();
        }

        var mappedRows = MapRows(table, table.Rows, template.TargetHeaders, template.HeaderMatchMode, logger);
        var result = new TableData
        {
            Columns = TableData.CreateColumns(template.TargetHeaders.Select(th => th.Header)),
            Rows = mappedRows
        };

        TableDataValidator.EnsureValid(result, "Template output");

        logger?.Invoke($"[ApplyTemplate] 完成：{result.ColumnCount} 列 x {result.RowCount} 行");
        return result;
    }

    /// <summary>
    /// 应用筛选模板。
    /// 单筛选条件沿用旧逻辑：整表筛选一次再映射。
    /// 多筛选条件按筛选项目分组：先分组筛选，再组内映射，最后横向拼接。
    /// </summary>
    public static TableData ApplyFilterTemplate(TableData table, CleanTemplate template, CleanTemplateFilter? filter, List<ReplacementGroup>? replacementGroups = null, Action<string>? logger = null)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (template == null) throw new ArgumentNullException(nameof(template));
        TableDataValidator.EnsureValid(table, "Filter template input");

        var matchValueColumns = template.TargetHeaders
            .Select((Column, Index) => new { Column, Index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Column.MatchValue))
            .ToList();

        var result = matchValueColumns.Count switch
        {
            0 => ApplyLegacyFilterTemplate(table, template, filter, logger),
            1 => ApplySingleFilterTemplate(table, template, matchValueColumns[0].Column, logger),
            _ => ApplyGroupedFilterTemplate(table, template, matchValueColumns.Select(x => x.Index).ToList(), logger)
        };

        TableDataValidator.EnsureValid(result, "Filter template output");
        return result;
    }

    private static TableData ApplyLegacyFilterTemplate(TableData table, CleanTemplate template, CleanTemplateFilter? filter, Action<string>? logger)
    {
        var items = filter?.MatchItems
            .Where(i => !string.IsNullOrWhiteSpace(i.Column.Header) && !string.IsNullOrWhiteSpace(i.Value))
            .ToList() ?? new List<FilterMatchItem>();

        if (items.Count == 0)
            return ApplyTemplate(table, template, null, logger);

        var conditionColumns = items.Select(i => new TemplateColumn
        {
            Header = i.Column.Header,
            SourceColumn = i.Column,
            MatchValue = i.Value
        }).ToList();

        var filteredRows = FilterRowsByConditions(table, conditionColumns, template.HeaderMatchMode, logger);
        var mappedRows = MapRows(table, filteredRows, template.TargetHeaders, template.HeaderMatchMode, logger);

        var result = new TableData
        {
            Columns = TableData.CreateColumns(template.TargetHeaders.Select(th => th.Header)),
            Rows = mappedRows
        };
        logger?.Invoke($"[ApplyFilterTemplate] 旧版筛选后保留 {result.RowCount}/{table.RowCount} 行");
        return result;
    }

    private static TableData ApplySingleFilterTemplate(TableData table, CleanTemplate template, TemplateColumn conditionColumn, Action<string>? logger)
    {
        var filteredRows = FilterRowsByConditions(table, new[] { conditionColumn }, template.HeaderMatchMode, logger);
        var mappedRows = MapRows(table, filteredRows, template.TargetHeaders, template.HeaderMatchMode, logger);

        var result = new TableData
        {
            Columns = TableData.CreateColumns(template.TargetHeaders.Select(th => th.Header)),
            Rows = mappedRows
        };
        logger?.Invoke($"[ApplyFilterTemplate] 单组筛选后保留 {result.RowCount}/{table.RowCount} 行");
        return result;
    }

    private static TableData ApplyGroupedFilterTemplate(TableData table, CleanTemplate template, List<int> groupStartIndexes, Action<string>? logger)
    {
        var groups = BuildColumnGroups(template.TargetHeaders, groupStartIndexes);
        var mappedGroups = new List<List<List<string>>>();
        int maxRows = 0;

        foreach (var group in groups)
        {
            var conditions = group.Columns.Where(c => !string.IsNullOrWhiteSpace(c.MatchValue)).ToList();
            var filteredRows = conditions.Count == 0
                ? table.Rows.Select(r => r.ToList()).ToList()
                : FilterRowsByConditions(table, conditions, template.HeaderMatchMode, logger);

            var mappedRows = MapRows(table, filteredRows, group.Columns, template.HeaderMatchMode, logger);
            mappedGroups.Add(mappedRows);
            maxRows = Math.Max(maxRows, mappedRows.Count);
        }

        var result = new TableData
        {
            Columns = TableData.CreateColumns(template.TargetHeaders.Select(th => th.Header))
        };

        for (int r = 0; r < maxRows; r++)
        {
            var outRow = new List<string>();
            for (int g = 0; g < groups.Count; g++)
            {
                var width = groups[g].Columns.Count;
                if (r < mappedGroups[g].Count)
                    outRow.AddRange(mappedGroups[g][r]);
                else
                    outRow.AddRange(Enumerable.Repeat("", width));
            }
            result.Rows.Add(outRow);
        }

        logger?.Invoke($"[ApplyFilterTemplate] 分组筛选完成：{groups.Count} 组，{result.ColumnCount} 列 x {result.RowCount} 行");
        return result;
    }

    private sealed class ColumnGroup
    {
        public List<TemplateColumn> Columns { get; } = new();
    }

    private static List<ColumnGroup> BuildColumnGroups(List<TemplateColumn> columns, List<int> groupStartIndexes)
    {
        var starts = groupStartIndexes.OrderBy(i => i).ToList();
        var groups = new List<ColumnGroup>();

        // 若第一个筛选列不在第 0 列，前置列作为无筛选组保留，避免静默丢列。
        if (starts[0] > 0)
        {
            var leading = new ColumnGroup();
            leading.Columns.AddRange(columns.Take(starts[0]));
            groups.Add(leading);
        }

        for (int i = 0; i < starts.Count; i++)
        {
            int start = starts[i];
            int end = (i + 1 < starts.Count ? starts[i + 1] : columns.Count) - 1;
            var group = new ColumnGroup();
            group.Columns.AddRange(columns.Skip(start).Take(end - start + 1));
            groups.Add(group);
        }

        return groups.Where(g => g.Columns.Count > 0).ToList();
    }

    private static List<List<string>> FilterRowsByConditions(TableData table, IEnumerable<TemplateColumn> conditionColumns, HeaderMatchMode matchMode, Action<string>? logger)
    {
        var conditions = new List<(TemplateColumn Column, List<int> Indexes, string MatchValue)>();
        foreach (var col in conditionColumns)
        {
            if (string.IsNullOrWhiteSpace(col.MatchValue)) continue;

            var indexes = FindSourceColumnIndexes(table, col, matchMode);
            if (indexes.Count == 0)
            {
                logger?.Invoke($"[FilterRowsByConditions] 筛选列未找到：{col.Header}");
                return new List<List<string>>();
            }

            conditions.Add((col, indexes, col.MatchValue.Trim()));
        }

        if (conditions.Count == 0)
            return table.Rows.Select(r => r.ToList()).ToList();

        var rows = new List<List<string>>();
        foreach (var row in table.Rows)
        {
            bool allMatch = true;
            foreach (var condition in conditions)
            {
                bool anyColumnMatches = condition.Indexes.Any(idx =>
                    idx >= 0 && idx < row.Count &&
                    string.Equals((row[idx] ?? "").Trim(), condition.MatchValue, StringComparison.OrdinalIgnoreCase));

                if (!anyColumnMatches)
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                rows.Add(row.ToList());
        }

        return rows;
    }

    private static List<List<string>> MapRows(
        TableData source,
        List<List<string>> sourceRows,
        IReadOnlyList<TemplateColumn> columns,
        HeaderMatchMode matchMode,
        Action<string>? logger)
    {
        var mapped = new List<List<string>>();
        foreach (var row in sourceRows)
        {
            var outRow = new List<string>();
            foreach (var col in columns)
            {
                var value = FindSourceColumnValue(row, source, col, logger, matchMode);
                outRow.Add(value ?? col.Fallback ?? "");
            }
            mapped.Add(outRow);
        }
        return mapped;
    }

    /// <summary>应用模板前检查：模板列命中多个源列时返回提示项。</summary>
    public static List<(string Header, int Count)> GetMultiSourceColumnWarnings(TableData table, CleanTemplate template)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (template == null) throw new ArgumentNullException(nameof(template));

        var warnings = new List<(string Header, int Count)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var col in template.TargetHeaders)
        {
            var indexes = FindSourceColumnIndexes(table, col, template.HeaderMatchMode);
            if (indexes.Count <= 1) continue;

            var label = string.IsNullOrWhiteSpace(col.Header) ? "（空表头）" : col.Header.Trim();
            if (seen.Add(label))
                warnings.Add((label, indexes.Count));
        }

        return warnings;
    }

    private static List<int> FindSourceColumnIndexes(
        TableData table,
        TemplateColumn templateColumn,
        HeaderMatchMode matchMode = HeaderMatchMode.Fuzzy)
    {
        foreach (var candidate in EnumerateSourceCandidates(templateColumn))
        {
            var indexes = FindMatchingColumnIndexes(table, candidate, matchMode);
            if (indexes.Count > 0) return indexes;
        }

        return new List<int>();
    }

    private static IEnumerable<ColumnReference> EnumerateSourceCandidates(TemplateColumn column)
    {
        if (!string.IsNullOrWhiteSpace(column.SourceColumn.Header))
            yield return column.SourceColumn;
        foreach (var backup in column.BackupSources.Where(item => !string.IsNullOrWhiteSpace(item.Header)))
            yield return backup;
    }

    private static List<int> FindMatchingColumnIndexes(
        TableData table,
        ColumnReference reference,
        HeaderMatchMode matchMode = HeaderMatchMode.Fuzzy)
    {
        if (string.IsNullOrWhiteSpace(reference.Header) || reference.Occurrence < 1)
            return new List<int>();

        var exact = table.ResolveColumnIndex(reference);
        if (exact >= 0)
            return new List<int> { exact };

        if (matchMode == HeaderMatchMode.Exact)
            return new List<int>();

        var search = reference.Header.Trim();
        var fuzzyMatches = table.Columns
            .Select((Column, Index) => new { Column, Index })
            .Where(x => x.Column.Header.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        search.IndexOf(x.Column.Header, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(x => x.Index)
            .ToList();
        return reference.Occurrence <= fuzzyMatches.Count
            ? new List<int> { fuzzyMatches[reference.Occurrence - 1] }
            : new List<int>();
    }

    /// <summary>按首要列及备用列顺序查找并取值。</summary>
    private static string? FindSourceColumnValue(
        List<string> row,
        TableData source,
        TemplateColumn column,
        Action<string>? logger = null,
        HeaderMatchMode matchMode = HeaderMatchMode.Fuzzy)
    {
        var indexes = FindSourceColumnIndexes(source, column, matchMode);
        if (indexes.Count == 0) return null;

        foreach (var idx in indexes)
        {
            if (idx >= 0 && idx < row.Count && !string.IsNullOrWhiteSpace(row[idx]))
                return row[idx] ?? "";
        }

        return "";
    }

    /// <summary>旧版 ApplyTemplate（保留兼容，标记为过时）</summary>
    [Obsolete("模板库已与替换库解耦；请使用 ApplyTemplate(TableData, CleanTemplate, null, Action<string>?)")]
    public static TableData ApplyTemplate(TableData table, CleanTemplate template, List<ReplacementGroup> replacementGroups)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        if (template == null) throw new ArgumentNullException(nameof(template));

        return ApplyTemplate(table, template, replacementGroups, null);
    }
}
