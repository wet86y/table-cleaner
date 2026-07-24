using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>可跨导入持久化的列引用：表头名称 + 从左到右的同名出现序号。</summary>
public sealed class ColumnReference : IEquatable<ColumnReference>
{
    [JsonPropertyName("header")]
    public string Header { get; set; } = "";

    [JsonPropertyName("occurrence")]
    public int Occurrence { get; set; } = 1;

    public bool Equals(ColumnReference? other) =>
        other is not null &&
        Occurrence == other.Occurrence &&
        string.Equals(Header, other.Header, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ColumnReference);

    public override int GetHashCode() =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Header ?? ""), Occurrence);

    public override string ToString() =>
        Occurrence <= 1 ? Header : $"{Header}（第{Occurrence}个同名列）";
}
