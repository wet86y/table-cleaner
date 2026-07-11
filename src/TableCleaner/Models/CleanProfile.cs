using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>清洗方案</summary>
public class CleanProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "默认方案";

    [JsonPropertyName("keptColumns")]
    public List<string> KeptColumns { get; set; } = new();

    [JsonPropertyName("groupColumns")]
    public List<string> GroupColumns { get; set; } = new();

    [JsonPropertyName("sumColumns")]
    public List<string> SumColumns { get; set; } = new();

    /// <summary>替换作用列列表，null=全表，空列表=全表，列表内有值=仅这些列</summary>
    [JsonPropertyName("replacementScope")]
    [JsonConverter(typeof(ReplacementScopeConverter))]
    public List<string>? ReplacementScope { get; set; } // null=全表, [列名]=仅这些列

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    public bool HasColumnSelection => KeptColumns.Count > 0;
    public bool HasMerge => GroupColumns.Count > 0;
}

/// <summary>
/// 兼容旧配置：replacementScope 在旧版中是单个 string（列名），新版是 List&lt;string&gt;?。
/// 反序列化时接受两者；序列化时始终输出数组。
/// </summary>
public class ReplacementScopeConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var val = reader.GetString();
            return string.IsNullOrEmpty(val) ? null : new List<string> { val };
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = JsonSerializer.Deserialize<List<string>>(ref reader, options);
            return list == null || list.Count == 0 ? null : list;
        }

        // Fallback: skip unknown tokens
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value == null || value.Count == 0)
        {
            writer.WriteNullValue();
            return;
        }
        JsonSerializer.Serialize(writer, value, options);
    }
}
