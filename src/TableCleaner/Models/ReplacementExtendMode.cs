using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>替换类型：普通替换 / 拓展替换</summary>
public enum ReplacementType
{
    /// <summary>普通替换：替换前 → 替换后</summary>
    [JsonPropertyName("normal")]
    Normal = 0,

    /// <summary>拓展替换：替换前 → 替换后 + 扩展值覆盖/插入</summary>
    [JsonPropertyName("extended")]
    Extended = 1
}

/// <summary>拓展替换写入模式</summary>
public enum ExtendWriteMode
{
    /// <summary>覆盖模式：命中单元格写替换后，右侧连续单元格被扩展值覆盖</summary>
    [JsonPropertyName("overwrite")]
    Overwrite = 0,

    /// <summary>插值模式：在命中列右侧插入空列，再写入扩展值</summary>
    [JsonPropertyName("insert")]
    Insert = 1
}
