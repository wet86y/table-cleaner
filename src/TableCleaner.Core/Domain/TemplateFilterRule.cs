using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>
/// 模板编辑器使用的筛选规则。
/// </summary>
public class TemplateFilterRule
{
    /// <summary>匹配的列</summary>
    [JsonPropertyName("column")]
    public ColumnReference Column { get; set; } = new();

    /// <summary>匹配运算符：equals|contains|startsWith|endsWith|regex</summary>
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "equals";

    /// <summary>匹配值</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    /// <summary>是否启用</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
