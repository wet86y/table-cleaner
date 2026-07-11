using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>表头匹配模式</summary>
public enum HeaderMatchMode
{
    /// <summary>先精确匹配，再模糊包含匹配（兼容旧行为）</summary>
    Fuzzy = 0,

    /// <summary>仅精确匹配（忽略大小写）</summary>
    Exact = 1
}
