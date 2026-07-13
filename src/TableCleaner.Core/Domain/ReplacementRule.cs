using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>单条替换规则</summary>
public class ReplacementRule
{
    [JsonPropertyName("before")]
    public string Before { get; set; } = "";

    [JsonPropertyName("after")]
    public string After { get; set; } = "";

    [JsonPropertyName("scope")]
    public string? Scope { get; set; } // null=全表, 列名=仅该列

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>拓展替换的扩展值列表；普通替换时为空</summary>
    [JsonPropertyName("extraValues")]
    public List<string> ExtraValues { get; set; } = new();

    /// <summary>替换类型：普通/拓展；默认 Normal</summary>
    [JsonPropertyName("replacementType")]
    public ReplacementType ReplacementType { get; set; } = ReplacementType.Normal;
}

/// <summary>配置导入导出包</summary>
public class ConfigPackage
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("profiles")]
    public List<CleanProfile> Profiles { get; set; } = new();

    [JsonPropertyName("replacements")]
    public List<ReplacementRule> Replacements { get; set; } = new();

    [JsonPropertyName("replacementGroups")]
    public List<ReplacementGroup>? ReplacementGroups { get; set; }

    [JsonPropertyName("templates")]
    public List<CleanTemplate>? Templates { get; set; }

    [JsonPropertyName("templateFilters")]
    public List<CleanTemplateFilter>? TemplateFilters { get; set; }
}
