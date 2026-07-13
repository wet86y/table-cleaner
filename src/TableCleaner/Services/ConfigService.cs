using System.IO.Compression;
using System.Text.Json;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>配置持久化：profiles.json、replacements.json（含分组）、配置包 zip</summary>
public static class ConfigService
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "笨蛋表格");

    private static readonly string BaseDir = Path.Combine(AppDataDir, "config");

    private static readonly string LegacyBaseDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config");

    private static bool _initialized;

    private static readonly string ProfilesPath = Path.Combine(BaseDir, "profiles.json");
    private static readonly string ReplacementsPath = Path.Combine(BaseDir, "replacements.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions JsonLenient = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public static void EnsureDirs()
    {
        if (!_initialized)
        {
            _initialized = true;
            Directory.CreateDirectory(BaseDir);
            MigrateLegacyConfig();
        }

        Directory.CreateDirectory(BaseDir);
    }

    /// <summary>
    /// 将旧版 exe 隔壁 config/ 中的配置文件迁到 %LocalAppData%。
    /// 仅当目标目录为空时执行一次性迁移。
    /// </summary>
    private static void MigrateLegacyConfig()
    {
        try
        {
            if (!Directory.Exists(LegacyBaseDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(LegacyBaseDir, "*.json"))
            {
                var dest = Path.Combine(BaseDir, Path.GetFileName(file));
                if (!File.Exists(dest))
                    File.Copy(file, dest, overwrite: false);
            }
        }
        catch
        {
            // Best-effort migration; fall back to defaults on failure.
        }
    }

    public static string GetExportsDir()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetSamplesDir()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");
        Directory.CreateDirectory(dir);
        return dir;
    }

    #region Profiles

    public static List<CleanProfile> LoadProfiles()
    {
        EnsureDirs();
        if (!File.Exists(ProfilesPath)) return new List<CleanProfile>();
        try
        {
            var json = File.ReadAllText(ProfilesPath);
            return JsonSerializer.Deserialize<List<CleanProfile>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void SaveProfiles(List<CleanProfile> profiles)
    {
        EnsureDirs();
        WriteTextAtomically(ProfilesPath, JsonSerializer.Serialize(profiles, JsonOpts));
    }

    #endregion

    #region Replacements (flat — legacy compat, used by old callers)

    /// <summary>加载替换规则（扁平列表，兼容旧代码）</summary>
    public static List<ReplacementRule> LoadReplacements()
    {
        var groups = LoadReplacementGroups();
        return groups.SelectMany(g => g.Rules).ToList();
    }

    /// <summary>保存替换规则（扁平列表，所有规则归入默认分组）</summary>
    public static void SaveReplacements(List<ReplacementRule> replacements)
    {
        var groups = new List<ReplacementGroup>
        {
            new ReplacementGroup { Name = "默认分组", Rules = replacements }
        };
        SaveReplacementGroups(groups);
    }

    #endregion

    #region Replacement Groups

    /// <summary>加载替换规则分组。兼容旧版扁平列表格式。</summary>
    public static List<ReplacementGroup> LoadReplacementGroups()
    {
        EnsureDirs();
        if (!File.Exists(ReplacementsPath))
            return new List<ReplacementGroup> { new ReplacementGroup { Name = "默认分组" } };

        try
        {
            var json = File.ReadAllText(ReplacementsPath);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];

                if (first.TryGetProperty("rules", out _))
                {
                    // New format: array of ReplacementGroup (has "rules")
                    var groups = JsonSerializer.Deserialize<List<ReplacementGroup>>(json, JsonLenient);
                    if (groups != null && groups.Count > 0) return groups;
                }
                else if (first.TryGetProperty("before", out _) || first.TryGetProperty("enabled", out _))
                {
                    // Old format: flat list of ReplacementRule → wrap in default group
                    var rules = JsonSerializer.Deserialize<List<ReplacementRule>>(json, JsonLenient);
                    if (rules != null && rules.Count > 0)
                        return new List<ReplacementGroup> { new ReplacementGroup { Name = "默认分组", Rules = rules } };
                }
            }

            // Empty array or unknown format → default group with empty rules
        }
        catch
        {
            // Ignore corrupt/empty files
        }

        return new List<ReplacementGroup> { new ReplacementGroup { Name = "默认分组" } };
    }

    /// <summary>保存替换规则分组</summary>
    public static void SaveReplacementGroups(List<ReplacementGroup> groups)
    {
        EnsureDirs();
        WriteTextAtomically(ReplacementsPath, JsonSerializer.Serialize(groups, JsonOpts));
    }

    #endregion

    #region Config Package (Zip)

    /// <summary>导出完整配置包为 zip</summary>
    public static bool ExportPackage(string zipPath)
    {
        try
        {
            var pkg = new ConfigPackage
            {
                Profiles = LoadProfiles(),
                Replacements = LoadReplacements(),
                ReplacementGroups = LoadReplacementGroups(),
                Templates = LoadTemplates(),
                TemplateFilters = LoadFilters()
            };
            var json = JsonSerializer.Serialize(pkg, JsonOpts);

            if (File.Exists(zipPath)) File.Delete(zipPath);
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("config.json", CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(json);
            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
            }
            catch { }
            return false;
        }
    }

    /// <summary>导入配置包，覆盖当前配置</summary>
    public static ConfigPackage? ImportPackage(string zipPath)
    {
        if (!File.Exists(zipPath)) return null;

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.SingleOrDefault(candidate =>
                string.Equals(candidate.FullName.Replace('\\', '/'), "config.json", StringComparison.OrdinalIgnoreCase));
            const long maxConfigBytes = 16 * 1024 * 1024;
            if (entry == null || entry.Length <= 0 || entry.Length > maxConfigBytes)
                return null;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var pkg = JsonSerializer.Deserialize<ConfigPackage>(json, JsonLenient);

            if (pkg != null)
            {
                pkg.Profiles ??= new List<CleanProfile>();
                pkg.Replacements ??= new List<ReplacementRule>();
                if (pkg.ReplacementGroups != null)
                {
                    foreach (var group in pkg.ReplacementGroups)
                        group.Rules ??= new List<ReplacementRule>();
                }

                SaveProfiles(pkg.Profiles);
                // Save groups if available, otherwise save flat replacements
                if (pkg.ReplacementGroups is { Count: > 0 })
                    SaveReplacementGroups(pkg.ReplacementGroups);
                else if (pkg.Replacements is { Count: > 0 })
                    SaveReplacements(pkg.Replacements);

                // Null means an older package that did not contain template libraries.
                if (pkg.Templates is not null)
                    SaveTemplates(pkg.Templates);
                if (pkg.TemplateFilters is not null)
                    SaveFilters(pkg.TemplateFilters);
            }
            return pkg;
        }
        catch { return null; }
    }

    #endregion

    #region Template Library

    private static readonly string TemplatesPath = Path.Combine(BaseDir, "templates.json");

    public static List<CleanTemplate> LoadTemplates()
    {
        EnsureDirs();
        if (!File.Exists(TemplatesPath)) return new List<CleanTemplate>();
        try
        {
            var json = File.ReadAllText(TemplatesPath);
            return JsonSerializer.Deserialize<List<CleanTemplate>>(json, JsonLenient) ?? new();
        }
        catch { return new(); }
    }

    public static void SaveTemplates(List<CleanTemplate> templates)
    {
        EnsureDirs();
        WriteTextAtomically(TemplatesPath, JsonSerializer.Serialize(templates, JsonOpts));
    }

    private static readonly string FiltersPath = Path.Combine(BaseDir, "templateFilters.json");

    public static List<CleanTemplateFilter> LoadFilters()
    {
        EnsureDirs();
        if (!File.Exists(FiltersPath)) return new List<CleanTemplateFilter>();
        try
        {
            var json = File.ReadAllText(FiltersPath);

            // 检测是否为旧格式（有 "rules" 字段但无 "matchItems"）
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                bool hasRules = first.TryGetProperty("rules", out _);
                bool hasMatchItems = first.TryGetProperty("matchItems", out _);

                if (hasRules && !hasMatchItems)
                {
                    // 旧格式：将 "rules" 转换为 "matchItems"
                    var migrated = MigrateOldFilterJson(json);
                    if (migrated != null)
                    {
                        // 写回迁移后的 JSON
                        WriteTextAtomically(FiltersPath, migrated);
                        return JsonSerializer.Deserialize<List<CleanTemplateFilter>>(migrated, JsonLenient) ?? new();
                    }
                }
            }

            return JsonSerializer.Deserialize<List<CleanTemplateFilter>>(json, JsonLenient) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>将旧格式 Rules 迁移为新格式 MatchItems</summary>
    private static string? MigrateOldFilterJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var entries = new List<Dictionary<string, JsonElement?>>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var newEntry = new Dictionary<string, JsonElement?>();

                // 复制除 "rules" 以外的所有字段
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == "rules")
                    {
                        // 将旧 rules 数组转换为 matchItems
                        var matchItems = new List<Dictionary<string, string>>();
                        foreach (var rule in prop.Value.EnumerateArray())
                        {
                            var field = "";
                            var value = "";
                            if (rule.TryGetProperty("field", out var f)) field = f.GetString() ?? "";
                            if (rule.TryGetProperty("value", out var v)) value = v.GetString() ?? "";
                            matchItems.Add(new Dictionary<string, string>
                            {
                                ["field"] = field,
                                ["value"] = value
                            });
                        }

                        // 序列化 matchItems 为 JSON 字符串
                        var miJson = JsonSerializer.Serialize(matchItems, JsonOpts);
                        using var miDoc = JsonDocument.Parse(miJson);
                        newEntry["matchItems"] = miDoc.RootElement.Clone();
                    }
                    else
                    {
                        newEntry[prop.Name] = prop.Value.Clone();
                    }
                }

                entries.Add(newEntry);
            }

            // 重建完整 JSON
            var resultEntries = new List<object>();
            foreach (var entry in entries)
            {
                var dict = new Dictionary<string, object?>();
                foreach (var kvp in entry)
                {
                    dict[kvp.Key] = kvp.Value;
                }
                resultEntries.Add(dict);
            }

            return JsonSerializer.Serialize(resultEntries, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveFilters(List<CleanTemplateFilter> filters)
    {
        EnsureDirs();
        WriteTextAtomically(FiltersPath, JsonSerializer.Serialize(filters, JsonOpts));
    }

    private static void WriteTextAtomically(string path, string content)
    {
        var tempPath = path + $".tmp.{Environment.ProcessId}.{Guid.NewGuid():N}";
        try
        {
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    #endregion
}
