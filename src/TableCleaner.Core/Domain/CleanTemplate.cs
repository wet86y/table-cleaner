using System.Text.Json.Serialization;
namespace TableCleaner.Models;

/// <summary>模板类型</summary>
public enum TemplateKind
{
    /// <summary>基础模板：指定输出表头 + fallback 列</summary>
    Basic,
    /// <summary>筛选模板：继承基础模板 + 匹配条件行</summary>
    Filter
}

/// <summary>清洗模板：包含目标表头配置的快照，可保存为预设模板</summary>
public class CleanTemplate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>表头匹配方式（精确匹配 / 模糊匹配）</summary>
    [JsonPropertyName("headerMatchMode")]
    public HeaderMatchMode HeaderMatchMode { get; set; } = HeaderMatchMode.Fuzzy;

    /// <summary>模板类型</summary>
    [JsonPropertyName("kind")]
    public TemplateKind Kind { get; set; } = TemplateKind.Basic;

    /// <summary>目标表头列表（输出列的顺序和名称）</summary>
    [JsonPropertyName("targetHeaders")]
    public List<TemplateColumn> TargetHeaders { get; set; } = new();
}

/// <summary>模板中定义的输出列配置</summary>
public class TemplateColumn
{
    /// <summary>输出列名（目标表头名称）</summary>
    [JsonPropertyName("header")]
    public string Header { get; set; } = "";

    /// <summary>源数据中的首要列引用。</summary>
    [JsonPropertyName("sourceColumn")]
    public ColumnReference SourceColumn { get; set; } = new();

    /// <summary>当源列不存在时使用的默认值</summary>
    [JsonPropertyName("fallback")]
    public string? Fallback { get; set; }

    /// <summary>备用源列引用，按顺序尝试。</summary>
    [JsonPropertyName("backupSources")]
    public List<ColumnReference> BackupSources { get; set; } = new();

    /// <summary>
    /// 筛选匹配值（仅筛选模板使用）。
    /// 匹配逻辑：按三行表头优先级找到源列后，该列的值须等于此值才保留行。
    /// </summary>
    [JsonPropertyName("matchValue")]
    public string MatchValue { get; set; } = "";
}

/// <summary>筛选模板的匹配规则（等值匹配）</summary>
public class FilterMatchItem
{
    /// <summary>源数据列</summary>
    [JsonPropertyName("column")]
    public ColumnReference Column { get; set; } = new();

    /// <summary>匹配值（等值匹配）</summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
