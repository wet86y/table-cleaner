using TableCleaner.Models;
using TableCleaner.Services;

var tests = new (string Name, Action Run)[]
{
    ("Delimited parser preserves empty cells", DelimitedParserPreservesEmptyCells),
    ("Delimited parser handles quotes and line breaks", DelimitedParserHandlesQuotesAndLineBreaks),
    ("Tabular builder preserves wider data rows", TabularBuilderPreservesWiderRows),
    ("Column selection preserves requested order", ColumnSelectionPreservesRequestedOrder),
    ("Pseudo table preserves empty TSV columns", PseudoTablePreservesEmptyTsvColumns),
    ("Delimiter detection prefers TSV over inner commas", DelimiterDetectionPrefersTsvOverInnerCommas),
    ("One-column cleanup keeps delimited header", OneColumnCleanupKeepsDelimitedHeader),
    ("Extended replacement keeps headers aligned", ExtendedReplacementKeepsHeadersAligned),
    ("Extended replacement keeps target indexes stable", ExtendedReplacementKeepsTargetIndexesStable),
    ("Merge keys cannot collide on separator text", MergeKeysCannotCollide),
    ("Merge rejects invalid numeric values", MergeRejectsInvalidNumericValues),
    ("Merge rejects overlapping group and sum columns", MergeRejectsOverlappingColumns),
    ("Selection merge rejects ragged input", SelectionMergeRejectsRaggedInput),
    ("Extended replacement headers are case-insensitively unique", ExtendedReplacementHeadersAreCaseInsensitivelyUnique),
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
    SequenceEqual(new[] { "A", "B", "Column3" }, result.Headers);
    SequenceEqual(new[] { "1", "2", "3" }, result.Rows[0]);
}

static void ColumnSelectionPreservesRequestedOrder()
{
    var source = Table(new[] { "A", "B" }, new[] { "1", "2" });
    var result = CleaningService.KeepColumns(source, new List<string> { "B", "A" });
    SequenceEqual(new[] { "B", "A" }, result.Headers);
    SequenceEqual(new[] { "2", "1" }, result.Rows[0]);
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
    var source = new TableData
    {
        Headers = new List<string> { "H1|H2" },
        Rows = new List<List<string>> { new() { "A|B" }, new() { "C|D" } }
    };
    var result = PseudoTableCleanService.TryCleanOneColumnTable(source)
        ?? throw new Exception("Cleanup returned null.");
    SequenceEqual(new[] { "H1", "H2" }, result.Headers);
    Equal(2, result.RowCount);
}

static void ExtendedReplacementKeepsHeadersAligned()
{
    var source = Table(new[] { "Code", "Original" }, new[] { "A", "keep" });
    var group = new ReplacementGroup
    {
        Type = ReplacementType.Extended,
        ExtendWriteMode = ExtendWriteMode.Insert,
        ScopeColumns = new List<string> { "Code" },
        ExtraColumnNames = new List<string> { "Province", "City" },
        Rules = new List<ReplacementRule>
        {
            new() { Before = "A", After = "A1", ExtraValues = new List<string> { "Zhejiang", "Hangzhou" } }
        }
    };

    var result = ReplacementService.ApplyGroup(source, group);
    SequenceEqual(new[] { "Code", "Province", "City", "Original" }, result.Headers);
    SequenceEqual(new[] { "A1", "Zhejiang", "Hangzhou", "keep" }, result.Rows[0]);
}

static void ExtendedReplacementKeepsTargetIndexesStable()
{
    var source = Table(new[] { "Left", "Right" }, new[] { "ignore", "A" });
    var group = new ReplacementGroup
    {
        Type = ReplacementType.Extended,
        ExtendWriteMode = ExtendWriteMode.Insert,
        ScopeColumns = new List<string> { "Right" },
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

static void MergeKeysCannotCollide()
{
    var source = new TableData
    {
        Headers = new List<string> { "G1", "G2", "Amount" },
        Rows = new List<List<string>>
        {
            new() { "A|B", "C", "1" },
            new() { "A", "B|C", "2" }
        }
    };
    var result = MergeService.Merge(source, new List<string> { "G1", "G2" }, new List<string> { "Amount" });
    Equal(2, result.RowCount);
}

static void MergeRejectsInvalidNumericValues()
{
    var source = Table(new[] { "Group", "Amount" }, new[] { "A", "not-a-number" });
    Throws<InvalidDataException>(() =>
        MergeService.Merge(source, new List<string> { "Group" }, new List<string> { "Amount" }));
}

static void MergeRejectsOverlappingColumns()
{
    var source = Table(new[] { "Group", "Amount" }, new[] { "A", "1" });
    Throws<InvalidDataException>(() =>
        MergeService.Merge(source, new List<string> { "Group" }, new List<string> { "Group" }));
}

static void SelectionMergeRejectsRaggedInput()
{
    var source = Table(new[] { "A", "B" }, new[] { "only-one" });
    Throws<InvalidDataException>(() =>
        SelectionMergeService.MergeColumns(source, new List<int> { 0, 1 }));
}

static void ExtendedReplacementHeadersAreCaseInsensitivelyUnique()
{
    var source = Table(new[] { "Code", "city" }, new[] { "A", "existing" });
    var group = new ReplacementGroup
    {
        Type = ReplacementType.Extended,
        ExtendWriteMode = ExtendWriteMode.Insert,
        ScopeColumns = new List<string> { "Code" },
        ExtraColumnNames = new List<string> { "City" },
        Rules = new List<ReplacementRule>
        {
            new() { Before = "A", After = "B", ExtraValues = new List<string> { "Hangzhou" } }
        }
    };

    var result = ReplacementService.ApplyGroup(source, group);
    SequenceEqual(new[] { "Code", "City_2", "city" }, result.Headers);
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

static TableData Table(string[] headers, string[] row) => new()
{
    Headers = headers.ToList(),
    Rows = new List<List<string>> { row.ToList() }
};

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
    try { action(); }
    catch (TException) { return; }
    throw new Exception($"Expected {typeof(TException).Name}.");
}
