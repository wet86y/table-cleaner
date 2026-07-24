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
    public ColumnReference? Scope { get; set; }

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
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    [JsonPropertyName("profiles")]
    public List<CleanProfile> Profiles { get; set; } = new();

    [JsonPropertyName("replacementGroups")]
    public List<ReplacementGroup> ReplacementGroups { get; set; } = new();

    [JsonPropertyName("templates")]
    public List<CleanTemplate> Templates { get; set; } = new();

    [JsonPropertyName("templateFilters")]
    public List<CleanTemplateFilter> TemplateFilters { get; set; } = new();
}
