using TableCleaner.Models;
using TableCleaner.Services;
using System.Text.Json;

var tests = new (string Name, Action Run)[]
{
    ("Delimited parser preserves empty cells", DelimitedParserPreservesEmptyCells),
    ("Delimited parser handles quotes and line breaks", DelimitedParserHandlesQuotesAndLineBreaks),
    ("Tabular builder preserves wider data rows", TabularBuilderPreservesWiderRows),
    ("Tabular builder preserves duplicate headers", TabularBuilderPreservesDuplicateHeaders),
    ("Table columns have unique IDs", TableColumnsHaveUniqueIds),
    ("Clone preserves column IDs", ClonePreservesColumnIds),
    ("Duplicate occurrence resolves independently", DuplicateOccurrenceResolvesIndependently),
    ("Duplicate display names are disambiguated outside grid", DuplicateDisplayNamesAreDisambiguatedOutsideGrid),
    ("New config model round-trips occurrence", NewConfigModelRoundTripsOccurrence),
    ("Column selection preserves requested duplicate", ColumnSelectionPreservesRequestedDuplicate),
    ("Pseudo table preserves empty TSV columns", PseudoTablePreservesEmptyTsvColumns),
    ("Delimiter detection prefers TSV over inner commas", DelimiterDetectionPrefersTsvOverInnerCommas),
    ("One-column cleanup keeps delimited header", OneColumnCleanupKeepsDelimitedHeader),
    ("Header detector recognizes wide Chinese header", HeaderDetectorRecognizesWideChineseHeader),
    ("Header detector rejects all-text data row", HeaderDetectorRejectsAllTextDataRow),
    ("Header detector keeps single row uncertain", HeaderDetectorKeepsSingleRowUncertain),
    ("Separator forces header decision", SeparatorForcesHeaderDecision),
    ("Header promotion preserves IDs", HeaderPromotionPreservesIds),
    ("Extended replacement keeps headers aligned", ExtendedReplacementKeepsHeadersAligned),
    ("Extended replacement keeps target indexes stable", ExtendedReplacementKeepsTargetIndexesStable),
    ("Extended replacement preserves duplicate display names", ExtendedReplacementPreservesDuplicateDisplayNames),
    ("Template maps a specific duplicate occurrence", TemplateMapsSpecificDuplicateOccurrence),
    ("Merge keys cannot collide on separator text", MergeKeysCannotCollide),
    ("Merge rejects invalid numeric values", MergeRejectsInvalidNumericValues),
    ("Merge rejects overlapping group and sum columns", MergeRejectsOverlappingColumns),
    ("Selection merge rejects ragged input", SelectionMergeRejectsRaggedInput),
    ("Validator rejects duplicate column IDs", ValidatorRejectsDuplicateColumnIds),
    ("Validator rejects ragged rows", ValidatorRejectsRaggedRows),
    ("Export normalization escapes CSV fields", ExportNormalizationEscapesCsvFields),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS: {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL: {test.Name} - {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Core regression tests failed: {failures.Count}/{tests.Length}");
    Environment.Exit(1);
}

Console.WriteLine($"PASS: Core regression tests passed ({tests.Length}/{tests.Length}).");

static void DelimitedParserPreservesEmptyCells()
{
    var records = DelimitedTextParser.Parse("A\t\tC\r\n1\t2\t3", '\t');
    Equal(3, records[0].Count);
    Equal("", records[0][1]);
    Equal("C", records[0][2]);
}

static void DelimitedParserHandlesQuotesAndLineBreaks()
{
    var records = DelimitedTextParser.Parse("Name,Note\r\nAlice,\"Shanghai, China\"\r\nBob,\"line1\r\nline2\"", ',');
    Equal(3, records.Count);
    Equal("Shanghai, China", records[1][1]);
    Equal("line1\r\nline2", records[2][1]);
}

static void TabularBuilderPreservesWiderRows()
{
    var records = new List<List<string>>
    {
        new() { "A", "B" },
        new() { "1", "2", "3" }
    };
    var result = TabularDataBuilder.FromRecords(records, firstRecordIsHeader: true)
        ?? throw new Exception("Builder returned null.");
    SequenceEqual(new[] { "A", "B", "Column3" }, Headers(result));
    SequenceEqual(new[] { "1", "2", "3" }, result.Rows[0]);
}

static void TabularBuilderPreservesDuplicateHeaders()
{
    var records = new List<List<string>>
    {
        new() { "序号", "数量", "数量" },
        new() { "1", "2", "3" }
    };
    var result = TabularDataBuilder.FromRecords(records, firstRecordIsHeader: true)
        ?? throw new Exception("Builder returned null.");
    SequenceEqual(new[] { "序号", "数量", "数量" }, Headers(result));
}

static void TableColumnsHaveUniqueIds()
{
    var table = Table(new[] { "A", "A", "序号" }, new[] { "1", "2", "3" });
    Equal(3, table.Columns.Select(column => column.Id).Distinct().Count());
}

static void ClonePreservesColumnIds()
{
    var source = Table(new[] { "A", "A" }, new[] { "1", "2" });
    var clone = source.Clone();
    SequenceEqual(source.Columns.Select(column => column.Id), clone.Columns.Select(column => column.Id));
    clone.Columns[0].Header = "Changed";
    Equal("A", source.Columns[0].Header);
}

static void DuplicateOccurrenceResolvesIndependently()
{
    var source = Table(new[] { "A", "A", "B" }, new[] { "left", "right", "tail" });
    Equal(0, source.ResolveColumnIndex(Ref("A", 1)));
    Equal(1, source.ResolveColumnIndex(Ref("A", 2)));
    Equal(-1, source.ResolveColumnIndex(Ref("A", 3)));
}

static void DuplicateDisplayNamesAreDisambiguatedOutsideGrid()
{
    var source = Table(new[] { "数量", "数量" }, new[] { "1", "2" });
    Equal("数量（第1个同名列）", source.GetColumnDisplayName(0));
    Equal("数量（第2个同名列）", source.GetColumnDisplayName(1));
}

static void NewConfigModelRoundTripsOccurrence()
{
    var profile = new CleanProfile
    {
        Name = "duplicate",
        KeptColumns = new List<ColumnReference> { Ref("数量", 2) }
    };
    var json = JsonSerializer.Serialize(profile);
    var restored = JsonSerializer.Deserialize<CleanProfile>(json)
        ?? throw new Exception("Profile deserialization returned null.");
    Equal("数量", restored.KeptColumns[0].Header);
    Equal(2, restored.KeptColumns[0].Occurrence);
}

static void ColumnSelectionPreservesRequestedDuplicate()
{
    var source = Table(new[] { "A", "A", "B" }, new[] { "left", "right", "tail" });
    var result = CleaningService.KeepColumns(source, new[] { Ref("A", 2), Ref("B") });
    SequenceEqual(new[] { "A", "B" }, Headers(result));
    SequenceEqual(new[] { "right", "tail" }, result.Rows[0]);
}

static void PseudoTablePreservesEmptyTsvColumns()
{
    var delimiter = new DelimiterInfo { Kind = DelimiterKind.SingleChar, Char = '\t' };
    var table = PseudoTableCleanService.ParseWithDelimiter("H1\tH2\tH3\r\nA\t\tC", delimiter)
        ?? throw new Exception("Parser returned null.");
    Equal(3, table.ColumnCount);
    Equal("", table.Rows[0][1]);
    Equal("C", table.Rows[0][2]);
}

static void DelimiterDetectionPrefersTsvOverInnerCommas()
{
    var lines = new[] { "Name\tAddress\tAmount", "Alice\tShanghai, China\t10", "Bob\tBeijing, China\t20" };
    var delimiter = PseudoTableCleanService.DetectDelimiter(lines)
        ?? throw new Exception("No delimiter detected.");
    Equal('\t', delimiter.Char);
}

static void OneColumnCleanupKeepsDelimitedHeader()
{
    var source = Table(new[] { "H1|H2" }, new[] { "A|B" });
    source.Rows.Add(new List<string> { "C|D" });
    var result = PseudoTableCleanService.TryCleanOneColumnTable(source)
        ?? throw new Exception("Cleanup returned null.");
    SequenceEqual(new[] { "H1", "H2" }, Headers(result));
    Equal(2, result.RowCount);
}

static void HeaderDetectorRecognizesWideChineseHeader()
{
    var header = ("序号\t订货号\t子图号\t物料名称\t发动机号\t机体号\t发动机号(后12位)\t批次\t可用库存数量\t" +
                  "锁帐数量\t总库存数量\t客户\t库别\t使用状态\t库存状态\t立库库位\t生产线编码\t库龄(天)\t托管标志\t" +
                  "库位编码\t库位名称\t供应商编码\t供应商名称\t制造商编码\t制造商名称\t库区编码\t库区名称\t仓库编码\t" +
                  "仓库名称\t采购入库时间\t物料条码\t创建时间\t排放标准\t机型\t排量\t额定功率\t配置\t排产备注\t整车图号")
        .Split('\t').ToList();
    var body1 = Enumerable.Range(1, header.Count).Select(index => index % 4 == 0 ? "10" : $"V{index:000}").ToList();
    var body2 = Enumerable.Range(1, header.Count).Select(index => index % 3 == 0 ? "20" : $"X{index:000}").ToList();
    var decision = HeaderDetectionService.Detect(new[] { header, body1, body2 }, false);
    Equal(HeaderDecision.Header, decision.Decision);
}

static void HeaderDetectorRejectsAllTextDataRow()
{
    var rows = new[]
    {
        new List<string> { "张三", "上海" },
        new List<string> { "李四", "北京" },
        new List<string> { "王五", "深圳" }
    };
    var decision = HeaderDetectionService.Detect(rows, false);
    Equal(HeaderDecision.NoHeader, decision.Decision);
}

static void HeaderDetectorKeepsSingleRowUncertain()
{
    var decision = HeaderDetectionService.Detect(
        new[] { new List<string> { "名称", "数量" } },
        false);
    Equal(HeaderDecision.Uncertain, decision.Decision);
}

static void SeparatorForcesHeaderDecision()
{
    var rows = new[]
    {
        new List<string> { "anything", "anything" },
        new List<string> { "text", "text" }
    };
    var decision = HeaderDetectionService.Detect(rows, true);
    Equal(HeaderDecision.Header, decision.Decision);
    Equal(100, decision.Confidence);
}

static void HeaderPromotionPreservesIds()
{
    var table = Table(new[] { "列1", "列2" }, new[] { "名称", "名称" });
    table.Rows.Add(new List<string> { "A", "B" });
    var ids = table.Columns.Select(column => column.Id).ToArray();
    Equal(true, HeaderPromotionService.PromoteFirstRow(table));
    SequenceEqual(ids, table.Columns.Select(column => column.Id));
    SequenceEqual(new[] { "名称", "名称" }, Headers(table));
    Equal(1, table.RowCount);
}

static void ExtendedReplacementKeepsHeadersAligned()
{
    var source = Table(new[] { "Code", "Original" }, new[] { "A", "keep" });
    var group = ExtendedGroup("Code", new[] { "Province", "City" }, new[] { "Zhejiang", "Hangzhou" });
    var result = ReplacementService.ApplyGroup(source, group);
    SequenceEqual(new[] { "Code", "Province", "City", "Original" }, Headers(result));
    SequenceEqual(new[] { "A1", "Zhejiang", "Hangzhou", "keep" }, result.Rows[0]);
}

static void ExtendedReplacementKeepsTargetIndexesStable()
{
    var source = Table(new[] { "Left", "Right" }, new[] { "ignore", "A" });
    var group = new ReplacementGroup
    {
        Type = ReplacementType.Extended,
        ExtendWriteMode = ExtendWriteMode.Insert,
        ScopeColumns = new List<ColumnReference> { Ref("Right") },
        Rules = new List<ReplacementRule>
        {
            new() { Before = "A", After = "B", ExtraValues = new List<string> { "first" } },
            new() { Before = "B", After = "C", ExtraValues = new List<string> { "second" } }
        }
    };
    var result = ReplacementService.ApplyGroup(source, group);
    Equal("C", result.Rows[0][1]);
    Equal("ignore", result.Rows[0][0]);
}

static void ExtendedReplacementPreservesDuplicateDisplayNames()
{
    var source = Table(new[] { "Code", "City" }, new[] { "A", "existing" });
    var group = ExtendedGroup("Code", new[] { "City" }, new[] { "Hangzhou" });
    var result = ReplacementService.ApplyGroup(source, group);
    SequenceEqual(new[] { "Code", "City", "City" }, Headers(result));
    Equal(3, result.Columns.Select(column => column.Id).Distinct().Count());
}

static void TemplateMapsSpecificDuplicateOccurrence()
{
    var source = Table(new[] { "Code", "Code" }, new[] { "left", "right" });
    var template = new CleanTemplate
    {
        TargetHeaders = new List<TemplateColumn>
        {
            new()
            {
                Header = "Selected",
                SourceColumn = Ref("Code", 2)
            }
        }
    };
    var result = TemplateEngine.ApplyTemplate(source, template);
    Equal("right", result.Rows[0][0]);
}

static void MergeKeysCannotCollide()
{
    var source = new TableData
    {
        Columns = TableData.CreateColumns(new[] { "G1", "G2", "Amount" }),
        Rows = new List<List<string>>
        {
            new() { "A|B", "C", "1" },
            new() { "A", "B|C", "2" }
        }
    };
    var result = MergeService.Merge(source, new[] { Ref("G1"), Ref("G2") }, new[] { Ref("Amount") });
    Equal(2, result.RowCount);
}

static void MergeRejectsInvalidNumericValues()
{
    var source = Table(new[] { "Group", "Amount" }, new[] { "A", "not-a-number" });
    Throws<InvalidDataException>(() =>
        MergeService.Merge(source, new[] { Ref("Group") }, new[] { Ref("Amount") }));
}

static void MergeRejectsOverlappingColumns()
{
    var source = Table(new[] { "Group", "Amount" }, new[] { "A", "1" });
    Throws<InvalidDataException>(() =>
        MergeService.Merge(source, new[] { Ref("Group") }, new[] { Ref("Group") }));
}

static void SelectionMergeRejectsRaggedInput()
{
    var source = Table(new[] { "A", "B" }, new[] { "only-one" });
    Throws<InvalidDataException>(() =>
        SelectionMergeService.MergeColumns(source, new List<int> { 0, 1 }));
}

static void ValidatorRejectsDuplicateColumnIds()
{
    var source = Table(new[] { "A", "A" }, new[] { "1", "2" });
    source.Columns[1].Id = source.Columns[0].Id;
    Equal(1, TableDataValidator.Validate(source).Count);
}

static void ValidatorRejectsRaggedRows()
{
    var source = Table(new[] { "A", "B" }, new[] { "only-one" });
    Equal(1, TableDataValidator.Validate(source).Count);
}

static void ExportNormalizationEscapesCsvFields()
{
    Equal("\"a,b\"", ExportNormalizationService.EscapeDelimitedField("a,b", ','));
    Equal("\"a\"\"b\"", ExportNormalizationService.EscapeDelimitedField("a\"b", ','));
    Equal("plain", ExportNormalizationService.EscapeDelimitedField("plain", ','));
}

static ReplacementGroup ExtendedGroup(
    string scopeHeader,
    IEnumerable<string> extraHeaders,
    IEnumerable<string> extraValues) => new()
{
    Type = ReplacementType.Extended,
    ExtendWriteMode = ExtendWriteMode.Insert,
    ScopeColumns = new List<ColumnReference> { Ref(scopeHeader) },
    ExtraColumnNames = extraHeaders.ToList(),
    Rules = new List<ReplacementRule>
    {
        new()
        {
            Before = "A",
            After = "A1",
            ExtraValues = extraValues.ToList()
        }
    }
};

static ColumnReference Ref(string header, int occurrence = 1) => new()
{
    Header = header,
    Occurrence = occurrence
};

static TableData Table(string[] headers, string[] row) => new()
{
    Columns = TableData.CreateColumns(headers),
    Rows = new List<List<string>> { row.ToList() }
};

static IEnumerable<string> Headers(TableData table) =>
    table.Columns.Select(column => column.Header);

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected '{expected}', got '{actual}'.");
}

static void SequenceEqual(IEnumerable<string> expected, IEnumerable<string> actual)
{
    if (!expected.SequenceEqual(actual))
        throw new Exception($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
}

static void Throws<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new Exception($"Expected {typeof(TException).Name}.");
}
