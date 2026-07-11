namespace TableCleaner.Models;

/// <summary>伪表格分隔符检测结果</summary>
public class DelimiterInfo
{
    /// <summary>分隔符名称（用于日志/UI展示）</summary>
    public string Name { get; set; } = "";

    /// <summary>分隔符类型</summary>
    public DelimiterKind Kind { get; set; }

    /// <summary>单字符分隔符（Kind=SingleChar 时有效）</summary>
    public char Char { get; set; }

    /// <summary>正则分割模式（Kind=MultiSpace 时有效）</summary>
    public string Pattern { get; set; } = "";

    /// <summary>检测到的列数（众数+1）</summary>
    public int DetectedColumnCount { get; set; }

    /// <summary>一致性比率：众数行占含分隔符行的比例（0~1）</summary>
    public double Consistency { get; set; }

    /// <summary>行覆盖率：含分隔符行占总非空行的比例（0~1）</summary>
    public double LineCoverage { get; set; }

    /// <summary>综合评分（越高越可能是正确的分隔符）</summary>
    public double Score { get; set; }

    public override string ToString() =>
        $"{Name} [列数={DetectedColumnCount}, 一致性={Consistency:P0}, 覆盖率={LineCoverage:P0}, 评分={Score:F2}]";
}

/// <summary>分隔符类型</summary>
public enum DelimiterKind
{
    /// <summary>单字符分隔符</summary>
    SingleChar,

    /// <summary>多空格分隔符（2+ 连续空格）</summary>
    MultiSpace
}
