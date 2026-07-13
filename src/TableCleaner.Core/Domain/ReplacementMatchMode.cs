using System.Text.Json.Serialization;

namespace TableCleaner.Models;

/// <summary>替换匹配模式</summary>
public enum ReplacementMatchMode
{
    /// <summary>包含替换（string.Contains + string.Replace，兼容旧行为）</summary>
    Fuzzy = 0,

    /// <summary>精确匹配（单元格值完全相等才替换）</summary>
    Exact = 1
}
