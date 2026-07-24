using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>清洗方案</summary>
public class CleanProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "默认方案";

    [JsonPropertyName("keptColumns")]
    public List<ColumnReference> KeptColumns { get; set; } = new();

    [JsonPropertyName("groupColumns")]
    public List<ColumnReference> GroupColumns { get; set; } = new();

    [JsonPropertyName("sumColumns")]
    public List<ColumnReference> SumColumns { get; set; } = new();

    /// <summary>替换作用列列表，null=全表，空列表=全表，列表内有值=仅这些列</summary>
    [JsonPropertyName("replacementScope")]
    public List<ColumnReference>? ReplacementScope { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    public bool HasColumnSelection => KeptColumns.Count > 0;
    public bool HasMerge => GroupColumns.Count > 0;
}
