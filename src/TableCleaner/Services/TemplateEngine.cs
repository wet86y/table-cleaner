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

        var enabledItems = filter.MatchItems;
        if (enabledItems.Count == 0)
        {
            logger?.Invoke("[MatchFilter] 筛选模板无匹配条件，默认不匹配");
            return false;
        }

        foreach (var item in enabledItems)
        {
            int colIndex = table.GetColIndex(item.Field);
            if (colIndex < 0)
            {
                logger?.Invoke($"[MatchFilter] 列 '{item.Field}' 不存在 → 不匹配");
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
                logger?.Invoke($"[MatchFilter] 列 '{item.Field}' 值 '{cellValue}' 不匹配等值 '{item.Value}'");
                return false;
            }

            logger?.Invoke($"[MatchFilter] 列 '{item.Field}' 值 '{cellValue}' 匹配等值 '{item.Value}'");
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

        if (template.TargetHeaders.Count == 0)
        {
            logger?.Invoke("[ApplyTemplate] 模板没有定义目标表头，返回空表");
            return new TableData();
        }

        var mappedRows = MapRows(table.Headers, table.Rows, template.TargetHeaders, template.HeaderMatchMode, logger);
        var result = new TableData
        {
            Headers = template.TargetHeaders.Select(th => th.Header).ToList(),
            Rows = mappedRows
        };

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

        var matchValueColumns = template.TargetHeaders
            .Select((Column, Index) => new { Column, Index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Column.MatchValue))
            .ToList();

        if (matchValueColumns.Count == 0)
            return ApplyLegacyFilterTemplate(table, template, filter, logger);

        if (matchValueColumns.Count == 1)
            return ApplySingleFilterTemplate(table, template, matchValueColumns[0].Column, logger);

        return ApplyGroupedFilterTemplate(table, template, matchValueColumns.Select(x => x.Index).ToList(), logger);
    }

    private static TableData ApplyLegacyFilterTemplate(TableData table, CleanTemplate template, CleanTemplateFilter? filter, Action<string>? logger)
    {
        var items = filter?.MatchItems
            .Where(i => !string.IsNullOrWhiteSpace(i.Field) && !string.IsNullOrWhiteSpace(i.Value))
            .ToList() ?? new List<FilterMatchItem>();

        if (items.Count == 0)
            return ApplyTemplate(table, template, null, logger);

        var conditionColumns = items.Select(i => new TemplateColumn
        {
            Header = i.Field,
            SourceHeader = i.Field,
            MatchValue = i.Value
        }).ToList();

        var filteredRows = FilterRowsByConditions(table, conditionColumns, template.HeaderMatchMode, logger);
        var mappedRows = MapRows(table.Headers, filteredRows, template.TargetHeaders, template.HeaderMatchMode, logger);

        var result = new TableData
        {
            Headers = template.TargetHeaders.Select(th => th.Header).ToList(),
            Rows = mappedRows
        };
        logger?.Invoke($"[ApplyFilterTemplate] 旧版筛选后保留 {result.RowCount}/{table.RowCount} 行");
        return result;
    }

    private static TableData ApplySingleFilterTemplate(TableData table, CleanTemplate template, TemplateColumn conditionColumn, Action<string>? logger)
    {
        var filteredRows = FilterRowsByConditions(table, new[] { conditionColumn }, template.HeaderMatchMode, logger);
        var mappedRows = MapRows(table.Headers, filteredRows, template.TargetHeaders, template.HeaderMatchMode, logger);

        var result = new TableData
        {
            Headers = template.TargetHeaders.Select(th => th.Header).ToList(),
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

            var mappedRows = MapRows(table.Headers, filteredRows, group.Columns, template.HeaderMatchMode, logger);
            mappedGroups.Add(mappedRows);
            maxRows = Math.Max(maxRows, mappedRows.Count);
        }

        var result = new TableData
        {
            Headers = template.TargetHeaders.Select(th => th.Header).ToList()
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

            var indexes = FindSourceColumnIndexes(table.Headers, col.Header, col.SourceHeader, col.BackupSource1, col.BackupSource2, matchMode);
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

    private static List<List<string>> MapRows(List<string> sourceHeaders, List<List<string>> sourceRows, IReadOnlyList<TemplateColumn> columns, HeaderMatchMode matchMode, Action<string>? logger)
    {
        var mapped = new List<List<string>>();
        foreach (var row in sourceRows)
        {
            var outRow = new List<string>();
            foreach (var col in columns)
            {
                var value = FindSourceColumnValue(row, sourceHeaders, col.Header, col.SourceHeader, col.BackupSource1, col.BackupSource2, logger, matchMode);
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
            var indexes = FindSourceColumnIndexes(table.Headers, col.Header, col.SourceHeader, col.BackupSource1, col.BackupSource2, template.HeaderMatchMode);
            if (indexes.Count <= 1) continue;

            var label = string.IsNullOrWhiteSpace(col.Header) ? "（空表头）" : col.Header.Trim();
            if (seen.Add(label))
                warnings.Add((label, indexes.Count));
        }

        return warnings;
    }

    private static List<int> FindSourceColumnIndexes(List<string> headers, string header, string? legacySourceHeader, string? backup1, string? backup2, HeaderMatchMode matchMode = HeaderMatchMode.Fuzzy)
    {
        foreach (var candidate in EnumerateSourceCandidates(header, legacySourceHeader, backup1, backup2))
        {
            var indexes = FindMatchingColumnIndexes(headers, candidate, matchMode);
            if (indexes.Count > 0) return indexes;
        }

        return new List<int>();
    }

    private static IEnumerable<string?> EnumerateSourceCandidates(string header, string? legacySourceHeader, string? backup1, string? backup2)
    {
        yield return header;

        if (!string.IsNullOrWhiteSpace(legacySourceHeader) &&
            !string.Equals(legacySourceHeader.Trim(), header.Trim(), StringComparison.OrdinalIgnoreCase))
            yield return legacySourceHeader;

        yield return backup1;
        yield return backup2;
    }

    private static List<int> FindMatchingColumnIndexes(List<string> headers, string? searchHeader, HeaderMatchMode matchMode = HeaderMatchMode.Fuzzy)
    {
        if (string.IsNullOrWhiteSpace(searchHeader))
            return new List<int>();

        var search = searchHeader.Trim();
        var exact = headers
            .Select((Header, Index) => new { Header, Index })
            .Where(x => string.Equals(x.Header, search, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Index)
            .ToList();

        if (exact.Count > 0 || matchMode == HeaderMatchMode.Exact)
            return exact;

        return headers
            .Select((Header, Index) => new { Header, Index })
            .Where(x => x.Header.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        search.IndexOf(x.Header, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(x => x.Index)
            .ToList();
    }

    /// <summary>从源数据行中按三行优先级查找并取值；若命中多个同名源列，则从左到右取第一个非空值。</summary>
    private static string? FindSourceColumnValue(List<string> row, List<string> headers, string header, string? legacySourceHeader, string? backup1, string? backup2, Action<string>? logger = null, HeaderMatchMode matchMode = HeaderMatchMode.Fuzzy)
    {
        var indexes = FindSourceColumnIndexes(headers, header, legacySourceHeader, backup1, backup2, matchMode);
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
