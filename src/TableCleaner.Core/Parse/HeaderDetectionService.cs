using System.Globalization;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>保守的首行表头特征评分。只有高置信结果才自动提升。</summary>
public static class HeaderDetectionService
{
    private static readonly string[] HeaderKeywords =
    {
        "序号", "编号", "编码", "条码", "名称", "类型", "类别", "状态", "数量", "金额",
        "价格", "日期", "时间", "备注", "说明", "客户", "供应商", "制造商", "仓库", "库位",
        "库区", "库存", "批次", "型号", "机型", "功率", "排量", "标准", "标志", "图号",
        "id", "code", "name", "type", "status", "count", "quantity", "amount", "price",
        "date", "time", "note", "description", "customer", "supplier", "warehouse"
    };

    public static HeaderDetectionResult Detect(
        IReadOnlyList<List<string>> rows,
        bool separatorAfterFirstRow)
    {
        if (separatorAfterFirstRow)
            return HeaderDetectionResult.ExplicitHeader("首行后存在表格分隔线");

        if (rows.Count < 2)
        {
            return new HeaderDetectionResult
            {
                Decision = HeaderDecision.Uncertain,
                Confidence = 0,
                Features = new[] { "数据不足两行，不自动判断表头" }
            };
        }

        var features = new List<string>();
        var score = 0;
        var first = rows[0];
        var body = rows.Skip(1).Take(50).ToList();
        var laterHasEmpty = body.Any(row =>
            Enumerable.Range(0, first.Count)
                .Any(index => index >= row.Count || string.IsNullOrWhiteSpace(row[index])));
        var modalWidth = body
            .GroupBy(row => row.Count)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .First().Key;

        if (first.Count == modalWidth)
        {
            score += 15;
            features.Add("首行列数与正文一致 +15");
        }

        var nonEmptyRatio = first.Count == 0
            ? 0
            : first.Count(value => !string.IsNullOrWhiteSpace(value)) / (double)first.Count;
        if (nonEmptyRatio >= 0.8)
        {
            score += 15;
            features.Add("首行非空率高 +15");
        }

        var labelLikeRatio = first.Count == 0
            ? 0
            : first.Count(IsLabelLike) / (double)first.Count;
        if (labelLikeRatio >= 0.7)
        {
            score += 15;
            features.Add("首行以短文本标签为主 +15");
        }

        var keywordRatio = first.Count == 0
            ? 0
            : first.Count(ContainsHeaderKeyword) / (double)first.Count;
        if (keywordRatio >= 0.25)
        {
            score += 30;
            features.Add("表头关键词覆盖率高 +30");
        }
        else if (keywordRatio >= 0.1)
        {
            score += 15;
            features.Add("命中部分表头关键词 +15");
        }

        var comparableColumns = 0;
        var contrastColumns = 0;
        var sameShapeColumns = 0;
        for (var column = 0; column < first.Count; column++)
        {
            var bodyKinds = body
                .Where(row => column < row.Count && !string.IsNullOrWhiteSpace(row[column]))
                .Select(row => Classify(row[column]))
                .ToList();
            if (bodyKinds.Count == 0)
                continue;

            comparableColumns++;
            var dominant = bodyKinds
                .GroupBy(kind => kind)
                .OrderByDescending(group => group.Count())
                .First().Key;
            var firstKind = Classify(first[column]);
            if (firstKind == ValueKind.Text && dominant is not ValueKind.Text)
                contrastColumns++;
            if (firstKind == dominant)
                sameShapeColumns++;
        }

        if (comparableColumns > 0)
        {
            var contrastRatio = contrastColumns / (double)comparableColumns;
            var sameShapeRatio = sameShapeColumns / (double)comparableColumns;
            if (contrastRatio >= 0.3)
            {
                score += 25;
                features.Add("首行与正文类型差异明显 +25");
            }
            if (sameShapeRatio >= 0.8 && !laterHasEmpty)
            {
                score -= 25;
                features.Add("首行与正文形态高度一致 -25");
            }
        }

        if (laterHasEmpty && nonEmptyRatio >= 0.8)
        {
            score += 30;
            features.Add("首行完整而正文存在空值 +30");
        }

        score = Math.Clamp(score, 0, 100);
        var decision = score >= 60
            ? HeaderDecision.Header
            : score <= 25
                ? HeaderDecision.NoHeader
                : HeaderDecision.Uncertain;

        return new HeaderDetectionResult
        {
            Decision = decision,
            Confidence = score,
            Features = features
        };
    }

    private static bool ContainsHeaderKeyword(string value)
    {
        var normalized = value.Trim();
        return HeaderKeywords.Any(keyword =>
            normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLabelLike(string value)
    {
        var normalized = value.Trim();
        return normalized.Length is > 0 and <= 24 &&
               !normalized.Contains('\n') &&
               Classify(normalized) == ValueKind.Text;
    }

    private static ValueKind Classify(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
            return ValueKind.Empty;
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out _) ||
            decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            return ValueKind.Number;
        if (DateTime.TryParse(normalized, CultureInfo.CurrentCulture, DateTimeStyles.None, out _))
            return ValueKind.Date;
        if (bool.TryParse(normalized, out _))
            return ValueKind.Boolean;
        if (normalized.All(character =>
                char.IsAsciiLetterOrDigit(character) ||
                character is '-' or '_' or '/' or '.' or '(' or ')'))
            return ValueKind.Identifier;
        return ValueKind.Text;
    }

    private enum ValueKind
    {
        Empty,
        Number,
        Date,
        Boolean,
        Identifier,
        Text
    }
}
