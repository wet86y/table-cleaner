using System.Text;
using System.Text.RegularExpressions;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>
/// 伪表格清洗服务：将 txt/剪切板中的伪表格转换为结构化 TableData。
/// v2.7.0: 多分隔符自动检测 + 整列均匀性校验，避免误伤单元格内偶然出现的分隔符。
/// </summary>
public static partial class PseudoTableCleanService
{
    // ── 分隔线正则 ──────────────────────────────────────────────
    private static readonly Regex PureSeparatorRegex = PureSeparatorMyRegex();

    // ── 多空格分割正则 ─────────────────────────────────────────
    private static readonly Regex MultiSpaceSplitRegex = MultiSpaceSplitMyRegex();

    // ── 候选分隔符定义 ──────────────────────────────────────────
    private static readonly (char Ch, string Name, double MinConsistency, double MinCoverage, int MinColumns)[] SingleCharCandidates = new[]
    {
        ('|',  "竖线",    0.70, 0.50, 2),  // 强分隔符，阈值最宽松
        ('\t', "TAB",     0.70, 0.50, 2),  // 强分隔符
        ('+',  "加号",    0.85, 0.60, 2),  // 弱分隔符，需更一致
        (';',  "分号",    0.85, 0.60, 2),  // 弱分隔符
        ('#',  "井号",    0.85, 0.60, 2),  // 弱分隔符
        (',',  "逗号",    0.90, 0.70, 3),  // 弱分隔符+极易误伤，需≥3列
    };

    private const double MultiSpaceMinConsistency = 0.85;
    private const double MultiSpaceMinCoverage = 0.60;
    private const int MultiSpaceMinColumns = 3;  // 多空格至少3列才可信

    // ══════════════════════════════════════════════════════════════
    // 公开 API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 从原始文本解析伪表格。自动检测分隔符。
    /// 如果文本不含任何有效分隔符结构，或解析后无有效数据，返回 null。
    /// </summary>
    public static TableData? ParsePseudoTableText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // 去除 UTF-8 BOM（EF BB BF），避免 BOM 附着到第一个单元格
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var rawLines = text.Split('\n');

        // Phase 0: 自动检测分隔符
        var delimiter = DetectDelimiter(rawLines);
        if (delimiter == null)
            return null;

        return ParseWithDelimiter(rawLines, delimiter);
    }

    /// <summary>
    /// 使用指定分隔符解析伪表格（供外部显式指定时使用）。
    /// </summary>
    public static TableData? ParseWithDelimiter(string text, DelimiterInfo delimiter)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // 去除 UTF-8 BOM
        if (text.Length > 0 && text[0] == '\uFEFF')
            text = text[1..];

        text = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var rawLines = text.Split('\n');
        return ParseWithDelimiter(rawLines, delimiter);
    }

    /// <summary>
    /// 尝试将单列表格（单元格内含有分隔符）解析为多列表格。
    /// 如果当前表格不是单列，或单元格内不含分隔符，返回 null。
    /// </summary>
    public static TableData? TryCleanOneColumnTable(TableData source)
    {
        if (source == null || source.ColumnCount != 1)
            return null;

        bool hasPotentialDelimiter = source.Rows.Any(r =>
            r.Count > 0 && r[0] != null && ContainsPotentialDelimiter(r[0]));

        if (!hasPotentialDelimiter && !ContainsPotentialDelimiter(source.Headers[0]))
            return null;

        var sb = new StringBuilder();
        if (source.Headers.Count > 0 && ContainsPotentialDelimiter(source.Headers[0]))
            sb.AppendLine(source.Headers[0]);
        foreach (var row in source.Rows)
        {
            if (row.Count > 0 && !string.IsNullOrEmpty(row[0]))
                sb.AppendLine(row[0]);
        }

        var combinedText = sb.ToString();
        var result = ParsePseudoTableText(combinedText);
        if (result is null || source.Headers.Count == 0 || !ContainsPotentialDelimiter(source.Headers[0]))
            return result;

        var normalizedText = combinedText.Replace("\r\n", "\n").Replace("\r", "\n");
        var delimiter = DetectDelimiter(normalizedText.Split('\n'));
        if (delimiter is null)
            return result;

        var parsedHeader = SplitByDelimiter(source.Headers[0], delimiter)
            .Select(value => value.Trim())
            .ToList();
        if (parsedHeader.Count != result.ColumnCount)
            return result;

        result.Headers = parsedHeader;
        if (result.Rows.Count > 0 && result.Rows[0].SequenceEqual(parsedHeader))
            result.Rows.RemoveAt(0);
        return result;
    }

    /// <summary>
    /// 对一组原始行执行分隔符检测，返回最佳候选或 null。
    /// </summary>
    public static DelimiterInfo? DetectDelimiter(string[] rawLines)
    {
        // 收集非空、非纯分隔线的行
        var contentLines = new List<string>();
        foreach (var line in rawLines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (IsSeparatorLine(trimmed))
                continue;
            contentLines.Add(trimmed);
        }

        if (contentLines.Count == 0)
            return null;

        // 检测各候选
        var candidates = new List<DelimiterInfo>();

        // 1. 单字符候选
        foreach (var (ch, name, minCon, minCov, minCol) in SingleCharCandidates)
        {
            var info = DetectSingleCharDelimiter(contentLines, ch, name, minCon, minCov, minCol);
            if (info != null)
                candidates.Add(info);
        }

        // 2. 多空格候选
        var multiSpaceInfo = DetectMultiSpaceDelimiter(contentLines);
        if (multiSpaceInfo != null)
            candidates.Add(multiSpaceInfo);

        if (candidates.Count == 0)
            return null;

        // 排序选最佳：
        // 1) 一致性 ≥ 阈值的候选才参与排序
        // 2) 优先选"列数更少"的（避免内层分隔符抢赢外层，如 | 内含逗号的情况）
        // 3) 列数相同则按 Score 降序
        var best = candidates
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.Consistency)
            .ThenByDescending(c => c.LineCoverage)
            .First();

        return best;
    }

    // ══════════════════════════════════════════════════════════════
    // Phase 0: 分隔符检测
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 检测单字符分隔符。
    /// 核心逻辑：统计每行中该字符出现次数，计算"众数"和一致性比率。
    /// 只有一致性 ≥ minConsistency 且覆盖率 ≥ minCoverage 且列数 ≥ minColumns 才算合格。
    /// </summary>
    private static DelimiterInfo? DetectSingleCharDelimiter(
        List<string> lines, char ch, string name,
        double minConsistency, double minCoverage, int minColumns)
    {
        // 对竖线和加号：用 Split 计数（因为它们可能是表格边框的一部分）
        // 对其他字符：用 Count 计数
        var countPerLine = new List<int>();
        int linesWithDelimiter = 0;

        foreach (var line in lines)
        {
            int count = DelimitedTextParser.CountDelimiterOutsideQuotes(line, ch);

            if (count > 0)
                linesWithDelimiter++;

            countPerLine.Add(count);
        }

        return EvaluateCandidate(lines, countPerLine, linesWithDelimiter,
            name, DelimiterKind.SingleChar, minConsistency, minCoverage, minColumns,
            ch: ch);
    }

    /// <summary>
    /// 检测多空格分隔符。
    /// 用正则按 2+ 连续空格拆分，统计每行拆出段数。
    /// </summary>
    private static DelimiterInfo? DetectMultiSpaceDelimiter(List<string> lines)
    {
        var countPerLine = new List<int>();
        int linesWithDelimiter = 0;

        foreach (var line in lines)
        {
            // 按 2+ 空格拆分，过滤空段
            var segments = MultiSpaceSplitRegex.Split(line)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            int count = Math.Max(0, segments.Count - 1); // 间隔数 = 段数 - 1
            if (count > 0)
                linesWithDelimiter++;

            countPerLine.Add(count);
        }

        return EvaluateCandidate(lines, countPerLine, linesWithDelimiter,
            "多空格", DelimiterKind.MultiSpace,
            MultiSpaceMinConsistency, MultiSpaceMinCoverage, MultiSpaceMinColumns,
            pattern: @"\s{2,}");
    }

    /// <summary>
    /// 评估候选分隔符是否合格。
    /// </summary>
    private static DelimiterInfo? EvaluateCandidate(
        List<string> lines,
        List<int> countPerLine,
        int linesWithDelimiter,
        string name,
        DelimiterKind kind,
        double minConsistency,
        double minCoverage,
        int minColumns,
        char ch = '\0',
        string pattern = "")
    {
        if (linesWithDelimiter == 0)
            return null;

        // 计算众数（出现次数最多的 count 值）
        var modeGroup = countPerLine
            .Where(c => c > 0)
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)  // 同频取更少列
            .First();

        int modeCount = modeGroup.Key;          // 众数 = 每行分隔符个数的众数
        int modeLines = modeGroup.Count();      // 符合众数的行数
        int columnCount = modeCount + 1;        // 列数 = 分隔符数 + 1

        // 一致性 = 众数行数 / 含分隔符的行数
        double consistency = (double)modeLines / linesWithDelimiter;

        // 行覆盖率 = 含分隔符的行数 / 总非空行数
        double lineCoverage = (double)linesWithDelimiter / lines.Count;

        // 综合评分 = 一致性 * 0.6 + 覆盖率 * 0.3 + min(列数/10, 0.1)
        // 列数贡献封顶 0.1，避免纯靠列数刷分
        double score = consistency * 0.6
                     + lineCoverage * 0.3
                     + Math.Min(columnCount / 10.0, 0.1);

        // 阈值过滤
        if (consistency < minConsistency)
            return null;
        if (lineCoverage < minCoverage)
            return null;
        if (columnCount < minColumns)
            return null;

        return new DelimiterInfo
        {
            Name = name,
            Kind = kind,
            Char = ch,
            Pattern = pattern,
            DetectedColumnCount = columnCount,
            Consistency = consistency,
            LineCoverage = lineCoverage,
            Score = score
        };
    }

    // ══════════════════════════════════════════════════════════════
    // 核心解析（多分隔符版）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 使用指定分隔符解析原始行列表。
    /// </summary>
    private static TableData? ParseWithDelimiter(string[] rawLines, DelimiterInfo delimiter)
    {
        // Phase 1: 收集有效数据行，跳过分隔线和空行
        var rawHasSeparatorAfterFirst = DetectSeparatorAfterFirstPipe(rawLines);
        var dataLines = new List<string>();

        foreach (var line in rawLines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (IsSeparatorLine(trimmed))
                continue;
            dataLines.Add(trimmed);
        }

        if (dataLines.Count == 0)
            return null;

        // Phase 2: 按分隔符拆分每行
        var parsedRows = new List<List<string>>();
        foreach (var line in dataLines)
        {
            var cells = SplitByDelimiter(line, delimiter)
                .Select(c => c.Trim())
                .ToList();
            parsedRows.Add(cells);
        }

        if (parsedRows.Count == 0)
            return null;

        // Phase 3: 对齐列宽，删除全空行
        int maxWidth = parsedRows.Max(r => r.Count);
        var normalized = parsedRows
            .Select(r =>
            {
                if (r.Count < maxWidth)
                    r.AddRange(Enumerable.Repeat("", maxWidth - r.Count));
                return r;
            })
            .Where(row => row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        if (normalized.Count == 0)
            return null;

        // Phase 4: 表头检测
        bool hasHeader = DetectHeader(normalized, rawHasSeparatorAfterFirst);
        List<string>? headers = null;
        List<List<string>> dataRows;

        if (hasHeader)
        {
            headers = new List<string>(normalized[0]);
            dataRows = normalized.Skip(1).ToList();

            // 裁剪首尾空列到表头范围
            int firstNonEmpty = headers.FindIndex(c => !string.IsNullOrWhiteSpace(c));
            int lastNonEmpty = headers.FindLastIndex(c => !string.IsNullOrWhiteSpace(c));
            if (firstNonEmpty >= 0)
            {
                int headerWidth = lastNonEmpty - firstNonEmpty + 1;
                headers = headers.Skip(firstNonEmpty).Take(headerWidth).ToList();

                var trimmedData = new List<List<string>>();
                foreach (var row in dataRows)
                {
                    var projected = new List<string>();
                    for (int ci = firstNonEmpty; ci <= lastNonEmpty && ci < row.Count; ci++)
                        projected.Add(row[ci]);
                    while (projected.Count < headerWidth)
                        projected.Add("");
                    trimmedData.Add(projected);
                }
                dataRows = trimmedData;
            }
        }
        else
        {
            dataRows = normalized;
        }

        // Phase 5: 删除空列
        int colCount = headers != null ? headers.Count : (dataRows.Count > 0 ? dataRows[0].Count : 0);
        if (colCount == 0)
            return null;

        bool HeaderIsEmpty(int c) => headers == null || c >= headers.Count || string.IsNullOrWhiteSpace(headers[c]);
        bool DataAllEmpty(int c) => dataRows.All(row => c >= row.Count || string.IsNullOrWhiteSpace(row[c]));

        var keepIndices = new List<int>();
        for (int c = 0; c < colCount; c++)
        {
            bool shouldRemove = hasHeader
                ? HeaderIsEmpty(c) && DataAllEmpty(c)
                : DataAllEmpty(c);
            if (!shouldRemove)
                keepIndices.Add(c);
        }

        if (keepIndices.Count == 0)
            return null;

        // Phase 6: 投影 + 删全空行
        var projectedRows = new List<List<string>>();
        foreach (var row in dataRows)
        {
            var projected = keepIndices.Select(ci => ci < row.Count ? row[ci] : "").ToList();
            if (projected.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                projectedRows.Add(projected);
        }

        if (projectedRows.Count == 0)
            return null;

        // Phase 7: 构建结果
        var result = new TableData();
        if (hasHeader)
        {
            result.Headers = keepIndices.Select(ci => ci < headers!.Count ? headers[ci] : "").ToList();
        }
        else
        {
            for (int i = 0; i < keepIndices.Count; i++)
                result.Headers.Add($"列{i + 1}");
        }
        result.Rows = projectedRows;

        return result;
    }

    // ══════════════════════════════════════════════════════════════
    // 按分隔符拆分行
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 按指定分隔符拆分一行文本。
    /// </summary>
    private static List<string> SplitByDelimiter(string line, DelimiterInfo delimiter)
    {
        return delimiter.Kind switch
        {
            DelimiterKind.SingleChar => SplitByChar(line, delimiter.Char),
            DelimiterKind.MultiSpace => SplitByMultiSpace(line),
            _ => new List<string> { line }
        };
    }

    /// <summary>
    /// 按单字符分隔符拆分。对竖线和加号特殊处理：首尾空段要保留（对应表格边框）。
    /// </summary>
    private static List<string> SplitByChar(string line, char ch)
    {
        return DelimitedTextParser.ParseLine(line, ch);
    }

    /// <summary>
    /// 按多空格（2+连续空格）拆分。
    /// </summary>
    private static List<string> SplitByMultiSpace(string line)
    {
        return MultiSpaceSplitRegex.Split(line)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }

    // ══════════════════════════════════════════════════════════════
    // 辅助方法
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 检查文本是否含有任何候选分隔符。
    /// </summary>
    private static bool ContainsPotentialDelimiter(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        // 检查单字符候选
        foreach (var (ch, _, _, _, _) in SingleCharCandidates)
        {
            if (text.Contains(ch))
                return true;
        }

        // 检查多空格
        if (MultiSpaceSplitRegex.IsMatch(text))
            return true;

        return false;
    }

    /// <summary>
    /// 检查原始行中第一条有效 pipe 行后是否紧跟着一个分隔线（忽略空行）。
    /// </summary>
    private static bool DetectSeparatorAfterFirstPipe(string[] rawLines)
    {
        int firstPipeIndex = -1;
        for (int i = 0; i < rawLines.Length; i++)
        {
            var trimmed = rawLines[i].Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            if (IsSeparatorLine(trimmed))
                continue;
            // 检测含任何表格分隔符的行（不只是 |）
            if (ContainsPotentialDelimiter(trimmed))
            {
                firstPipeIndex = i;
                break;
            }
        }

        if (firstPipeIndex < 0)
            return false;

        for (int j = firstPipeIndex + 1; j < rawLines.Length; j++)
        {
            var trimmed = rawLines[j].Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
            return IsSeparatorLine(trimmed);
        }

        return false;
    }

    /// <summary>
    /// 判断第一行是否表头行。
    /// 策略：1) 原始行中第一条有效行后紧跟分隔线 → 表头；
    ///       2) 第一行内层所有单元格非空，且后续至少一行有空单元格 → 表头；
    ///       3) 第一行全为非数值文本，且后续行至少有一个数值型单元格 → 表头。
    /// </summary>
    private static bool DetectHeader(List<List<string>> allRows, bool firstPipeFollowedBySeparator)
    {
        if (allRows.Count < 2)
            return false;

        // 策略 1：分隔线紧随其后
        if (firstPipeFollowedBySeparator)
            return true;

        var firstRow = allRows[0];
        int firstNonEmpty = firstRow.FindIndex(c => !string.IsNullOrWhiteSpace(c));
        int lastNonEmpty = firstRow.FindLastIndex(c => !string.IsNullOrWhiteSpace(c));

        if (firstNonEmpty < 0)
            return false;

        // 检查第一行内层是否全部非空
        bool allInnerNonEmpty = true;
        for (int i = firstNonEmpty; i <= lastNonEmpty; i++)
        {
            if (string.IsNullOrWhiteSpace(firstRow[i]))
            {
                allInnerNonEmpty = false;
                break;
            }
        }

        if (!allInnerNonEmpty)
            return false;

        // 策略 2：后续行有空单元格 → 第一行更可能是表头
        bool subsequentHasEmpty = allRows.Skip(1).Any(r =>
        {
            int end = Math.Min(r.Count - 1, lastNonEmpty);
            for (int i = firstNonEmpty; i <= end && i < r.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(r[i]))
                    return true;
            }
            return false;
        });

        if (subsequentHasEmpty)
            return true;

        // 策略 3：第一行全为非数值文本 + 后续行有数值 → 表头
        // 这是处理全满数据的关键启发式
        bool firstRowAllText = IsAllNonNumeric(firstRow, firstNonEmpty, lastNonEmpty);
        bool subsequentHasNumeric = allRows.Skip(1).Any(r =>
            HasNumericCell(r, firstNonEmpty, lastNonEmpty));

        if (firstRowAllText && subsequentHasNumeric)
            return true;

        return false;
    }

    /// <summary>
    /// 检查指定范围内所有单元格是否都是非数值文本。
    /// 数值判定：可解析为 double 的视为数值；含中文的必定非数值。
    /// </summary>
    private static bool IsAllNonNumeric(List<string> row, int start, int end)
    {
        for (int i = start; i <= end && i < row.Count; i++)
        {
            var cell = row[i].Trim();
            if (string.IsNullOrEmpty(cell))
                continue;
            // 含中文 → 非数值
            if (cell.Any(c => c > 0x4E00 && c < 0x9FFF))
                continue;
            // 可解析为 double → 数值
            if (double.TryParse(cell, out _))
                return false;
        }
        return true;
    }

    /// <summary>
    /// 检查指定范围内是否有数值型单元格。
    /// </summary>
    private static bool HasNumericCell(List<string> row, int start, int end)
    {
        for (int i = start; i <= end && i < row.Count; i++)
        {
            var cell = row[i].Trim();
            if (string.IsNullOrEmpty(cell))
                continue;
            // 含中文 → 不是纯数值
            if (cell.Any(c => c > 0x4E00 && c < 0x9FFF))
                continue;
            if (double.TryParse(cell, out _))
                return true;
        }
        return false;
    }

    /// <summary>
    /// 判断一行是否为分隔线（纯分隔线或 pipe/加号 分隔线）。
    /// </summary>
    private static bool IsSeparatorLine(string trimmed)
    {
        // 纯分隔线: ---, ===, ___
        if (PureSeparatorRegex.IsMatch(trimmed))
            return true;

        // 含竖线/加号的分隔线: |---|---|, +---+---+
        if (trimmed.Contains('|') || trimmed.Contains('+'))
        {
            var parts = trimmed.Split(new[] { '|', '+' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 && parts.All(p => PureSeparatorRegex.IsMatch(p.Trim()));
        }

        return false;
    }

    // ── 正则 ─────────────────────────────────────────────────────

    /// <summary>纯分隔线：至少 3 个 -/—/=/_ 字符</summary>
    [GeneratedRegex(@"^\s*[-—_=]{3,}\s*$")]
    private static partial Regex PureSeparatorMyRegex();

    /// <summary>多空格分割：2+ 连续空格</summary>
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceSplitMyRegex();
}
