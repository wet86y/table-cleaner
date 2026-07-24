using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>运行期表格列。Id 只用于程序内部，Header 是用户看到和导出的原始表头。</summary>
public sealed class TableColumn
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = CreateId();

    [JsonPropertyName("header")]
    public string Header { get; set; } = "";

    public static TableColumn Create(string? header) => new()
    {
        Id = CreateId(),
        Header = header?.Trim() ?? ""
    };

    public TableColumn Clone() => new()
    {
        Id = Id,
        Header = Header
    };

    private static string CreateId() => $"col_{Guid.NewGuid():N}";
}
