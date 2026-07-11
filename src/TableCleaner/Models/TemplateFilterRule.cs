using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>
/// 已过时。请改用 FilterMatchItem。
/// 保留仅用于旧代码（TemplateEditorForm）的编译兼容。
/// </summary>
[Obsolete("Use FilterMatchItem instead")]
public class TemplateFilterRule
{
    /// <summary>匹配的列名</summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = "";

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
