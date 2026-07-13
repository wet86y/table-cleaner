using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>替换规则分组</summary>
public class ReplacementGroup
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "默认分组";

    [JsonPropertyName("rules")]
    public List<ReplacementRule> Rules { get; set; } = new();

    /// <summary>分组级作用列列表。null 或空 = 全表；非空 = 仅这些列</summary>
    [JsonPropertyName("scopeColumns")]
    public List<string>? ScopeColumns { get; set; }

    /// <summary>匹配模式：精确/模糊；默认 Fuzzy 以兼容旧配置</summary>
    [JsonPropertyName("matchMode")]
    public ReplacementMatchMode MatchMode { get; set; } = ReplacementMatchMode.Fuzzy;

    /// <summary>替换类型：普通替换/拓展替换；默认 Normal 以兼容旧配置</summary>
    [JsonPropertyName("type")]
    public ReplacementType Type { get; set; } = ReplacementType.Normal;

    /// <summary>拓展替换写入模式：覆盖/插值；仅 Type=Extended 时生效</summary>
    [JsonPropertyName("extendWriteMode")]
    public ExtendWriteMode ExtendWriteMode { get; set; } = ExtendWriteMode.Overwrite;

    /// <summary>拓展替换自定义列名；仅 Type=Extended 时生效。非空时替代默认的"扩展1""扩展2"...</summary>
    [JsonPropertyName("extraColumnNames")]
    public List<string>? ExtraColumnNames { get; set; }
}
