using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using TableCleaner.Models;

namespace TableCleaner.Services;

/// <summary>仅支持 schemaVersion=2 的配置持久化。</summary>
public static class ConfigService
{
    public const int CurrentSchemaVersion = 2;

    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "笨蛋表格");
    private static readonly string BaseDir = Path.Combine(AppDataDir, "config");
    private static readonly string ProfilesPath = Path.Combine(BaseDir, "profiles.json");
    private static readonly string ReplacementsPath = Path.Combine(BaseDir, "replacements.json");
    private static readonly string TemplatesPath = Path.Combine(BaseDir, "templates.json");
    private static readonly string FiltersPath = Path.Combine(BaseDir, "templateFilters.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static bool _initialized;
    private static string? _schemaNotice;

    public static string? LastError { get; private set; }

    private sealed class ConfigDocument<T>
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }

    public static void EnsureDirs()
    {
        if (_initialized)
            return;

        _initialized = true;
        Directory.CreateDirectory(BaseDir);
        EnsureCurrentSchema();
    }

    public static string? ConsumeSchemaNotice()
    {
        EnsureDirs();
        var notice = _schemaNotice;
        _schemaNotice = null;
        return notice;
    }

    private static void EnsureCurrentSchema()
    {
        _schemaNotice = InitializeSchema(BaseDir, AppDataDir);
    }

    private static string? InitializeSchema(string baseDir, string appDataDir)
    {
        var markerPath = Path.Combine(baseDir, ".schema-v2");
        if (File.Exists(markerPath))
            return null;

        var existingJson = Directory.GetFiles(baseDir, "*.json");
        var allCurrent = existingJson.Length > 0 && existingJson.All(IsCurrentDocument);
        string? notice = null;
        if (!allCurrent && existingJson.Length > 0)
        {
            var backupDir = Path.Combine(
                appDataDir,
                $"config-legacy-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(backupDir);
            foreach (var file in existingJson)
                File.Copy(file, Path.Combine(backupDir, Path.GetFileName(file)), overwrite: false);

            WriteDocument(Path.Combine(baseDir, "profiles.json"), new List<CleanProfile>());
            WriteDocument(Path.Combine(baseDir, "replacements.json"), new List<ReplacementGroup>());
            WriteDocument(Path.Combine(baseDir, "templates.json"), new List<CleanTemplate>());
            WriteDocument(Path.Combine(baseDir, "templateFilters.json"), new List<CleanTemplateFilter>());
            notice = $"检测到旧版配置，已备份到：{backupDir}。当前版本已启用全新的列引用配置，请重新创建规则。";
        }

        File.WriteAllText(markerPath, CurrentSchemaVersion.ToString());
        return notice;
    }

    private static bool IsCurrentDocument(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("schemaVersion", out var version) &&
                   version.GetInt32() == CurrentSchemaVersion &&
                   document.RootElement.TryGetProperty("data", out _);
        }
        catch
        {
            return false;
        }
    }

    public static bool VerifyLegacyConfigBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tablecleaner-config-verify-{Guid.NewGuid():N}");
        var configDir = Path.Combine(root, "config");
        Directory.CreateDirectory(configDir);
        try
        {
            var legacyPath = Path.Combine(configDir, "profiles.json");
            File.WriteAllText(legacyPath, "[{\"name\":\"legacy\"}]");
            var notice = InitializeSchema(configDir, root);
            var backup = Directory.GetDirectories(root, "config-legacy-*").SingleOrDefault();
            return !string.IsNullOrWhiteSpace(notice) &&
                   backup is not null &&
                   File.Exists(Path.Combine(backup, "profiles.json")) &&
                   IsCurrentDocument(legacyPath) &&
                   File.Exists(Path.Combine(configDir, ".schema-v2"));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // Ignore verification temp cleanup failures.
            }
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

    public static List<CleanProfile> LoadProfiles() =>
        ReadDocument(ProfilesPath, new List<CleanProfile>());

    public static void SaveProfiles(List<CleanProfile> profiles) =>
        WriteDocument(ProfilesPath, profiles);

    public static List<ReplacementRule> LoadReplacements() =>
        LoadReplacementGroups().SelectMany(group => group.Rules).ToList();

    public static void SaveReplacements(List<ReplacementRule> replacements) =>
        SaveReplacementGroups(new List<ReplacementGroup>
        {
            new() { Name = "默认分组", Rules = replacements }
        });

    public static List<ReplacementGroup> LoadReplacementGroups()
    {
        var groups = ReadDocument(ReplacementsPath, new List<ReplacementGroup>());
        return groups.Count > 0
            ? groups
            : new List<ReplacementGroup> { new() { Name = "默认分组" } };
    }

    public static void SaveReplacementGroups(List<ReplacementGroup> groups) =>
        WriteDocument(ReplacementsPath, groups);

    public static List<CleanTemplate> LoadTemplates() =>
        ReadDocument(TemplatesPath, new List<CleanTemplate>());

    public static void SaveTemplates(List<CleanTemplate> templates) =>
        WriteDocument(TemplatesPath, templates);

    public static List<CleanTemplateFilter> LoadFilters() =>
        ReadDocument(FiltersPath, new List<CleanTemplateFilter>());

    public static void SaveFilters(List<CleanTemplateFilter> filters) =>
        WriteDocument(FiltersPath, filters);

    public static bool ExportPackage(string zipPath)
    {
        LastError = null;
        try
        {
            var package = new ConfigPackage
            {
                SchemaVersion = CurrentSchemaVersion,
                Profiles = LoadProfiles(),
                ReplacementGroups = LoadReplacementGroups(),
                Templates = LoadTemplates(),
                TemplateFilters = LoadFilters()
            };

            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("config.json", CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(package, JsonOptions));
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            try
            {
                if (File.Exists(zipPath))
                    File.Delete(zipPath);
            }
            catch
            {
                // Ignore cleanup failures.
            }
            return false;
        }
    }

    public static ConfigPackage? ImportPackage(string zipPath)
    {
        LastError = null;
        if (!File.Exists(zipPath))
        {
            LastError = "配置包不存在。";
            return null;
        }

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.Entries.SingleOrDefault(candidate =>
                string.Equals(candidate.FullName.Replace('\\', '/'), "config.json", StringComparison.OrdinalIgnoreCase));
            const long maxConfigBytes = 16 * 1024 * 1024;
            if (entry is null || entry.Length <= 0 || entry.Length > maxConfigBytes)
            {
                LastError = "配置包缺少有效的 config.json。";
                return null;
            }

            using var stream = entry.Open();
            var package = JsonSerializer.Deserialize<ConfigPackage>(stream, JsonOptions);
            if (package is null || package.SchemaVersion != CurrentSchemaVersion)
            {
                LastError = $"配置包版本不兼容，仅支持 schemaVersion={CurrentSchemaVersion}。";
                return null;
            }

            SaveProfiles(package.Profiles);
            SaveReplacementGroups(package.ReplacementGroups);
            SaveTemplates(package.Templates);
            SaveFilters(package.TemplateFilters);
            return package;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
    }

    private static T ReadDocument<T>(string path, T fallback)
    {
        EnsureDirs();
        if (!File.Exists(path))
            return fallback;

        try
        {
            var document = JsonSerializer.Deserialize<ConfigDocument<T>>(
                File.ReadAllText(path),
                JsonOptions);
            return document is { SchemaVersion: CurrentSchemaVersion, Data: not null }
                ? document.Data
                : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private static void WriteDocument<T>(string path, T data)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        var document = new ConfigDocument<T>
        {
            SchemaVersion = CurrentSchemaVersion,
            Data = data
        };
        WriteTextAtomically(path, JsonSerializer.Serialize(document, JsonOptions));
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
}
