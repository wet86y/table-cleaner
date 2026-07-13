using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>筛选模板：匹配条件 + 关联基础模板，用于从多组数据中自动匹配并执行</summary>
public class CleanTemplateFilter
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

    /// <summary>匹配成功后应用的基础模板 ID</summary>
    [JsonPropertyName("appliedTemplateId")]
    public string AppliedTemplateId { get; set; } = "";

    /// <summary>匹配条件列表（AND 逻辑），仅支持等值匹配</summary>
    [JsonPropertyName("matchItems")]
    public List<FilterMatchItem> MatchItems { get; set; } = new();

    /// <summary>匹配优先级（数字越小越优先）</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    /// <summary>是否启用</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
