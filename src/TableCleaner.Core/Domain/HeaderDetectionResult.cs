namespace TableCleaner.Models;

public enum HeaderDecision
{
    Header,
    NoHeader,
    Uncertain
}

/// <summary>自动表头判断结果，供导入状态提示和人工纠正使用。</summary>
public sealed class HeaderDetectionResult
{
    public HeaderDecision Decision { get; init; }
    public int Confidence { get; init; }
    public IReadOnlyList<string> Features { get; init; } = Array.Empty<string>();

    public static HeaderDetectionResult ExplicitHeader(string feature) => new()
    {
        Decision = HeaderDecision.Header,
        Confidence = 100,
        Features = new[] { feature }
    };
}

public sealed class ParsedTableResult
{
    public required TableData Table { get; init; }
    public required HeaderDetectionResult HeaderDetection { get; init; }
}
