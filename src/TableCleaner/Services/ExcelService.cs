using System.Data;
using System.Text;
using TableCleaner.Models;
using ClosedXML.Excel;
using ExcelDataReader;

namespace TableCleaner.Services;

/// <summary>Excel 导入/导出（XLSX/XLS）</summary>
public static class ExcelService
{
    static ExcelService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>从 Excel 导入第一个 Sheet</summary>
    public static TableData? ImportFirstSheet(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var sheets = ImportAllSheets(filePath);
        return sheets.Count > 0 ? sheets.Values.First() : null;
    }

    /// <summary>从 Excel 导入所有 Sheet</summary>
    public static Dictionary<string, TableData> ImportAllSheets(string filePath)
    {
        var result = new Dictionary<string, TableData>();
        if (!File.Exists(filePath)) return result;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext is ".xlsx" or ".xlsm")
        {
            if (TryImportOpenXmlWithClosedXml(filePath, result))
                return result;
        }

        ImportWithExcelDataReader(filePath, result);
        return result;
    }

    private static bool TryImportOpenXmlWithClosedXml(string filePath, Dictionary<string, TableData> result)
    {
        try
        {
            using var workbook = new XLWorkbook(filePath);
            foreach (var ws in workbook.Worksheets)
            {
                var used = ws.RangeUsed();
                if (used == null) continue;

                int firstRow = used.RangeAddress.FirstAddress.RowNumber;
                int lastRow = used.RangeAddress.LastAddress.RowNumber;
                int firstCol = used.RangeAddress.FirstAddress.ColumnNumber;
                int lastCol = used.RangeAddress.LastAddress.ColumnNumber;

                var rawHeaders = new List<string?>();
                for (int c = firstCol; c <= lastCol; c++)
                    rawHeaders.Add(GetCellTextWithMergedFallback(ws, firstRow, c));

                var td = new TableData
                {
                    Headers = HeaderNormalizationService.Normalize(rawHeaders)
                };

                for (int r = firstRow + 1; r <= lastRow; r++)
                {
                    var row = new List<string>();
                    for (int c = firstCol; c <= lastCol; c++)
                        row.Add(ws.Cell(r, c).GetFormattedString().Trim());
                    td.Rows.Add(row);
                }

                result[ws.Name] = td;
            }

            return result.Count > 0;
        }
        catch
        {
            result.Clear();
            return false;
        }
    }

    private static string GetCellTextWithMergedFallback(IXLWorksheet ws, int row, int col)
    {
        var direct = ws.Cell(row, col).GetFormattedString().Trim();
        if (!string.IsNullOrWhiteSpace(direct)) return direct;

        foreach (var merged in ws.MergedRanges)
        {
            var addr = merged.RangeAddress;
            if (row < addr.FirstAddress.RowNumber || row > addr.LastAddress.RowNumber) continue;
            if (col < addr.FirstAddress.ColumnNumber || col > addr.LastAddress.ColumnNumber) continue;

            return ws.Cell(addr.FirstAddress.RowNumber, addr.FirstAddress.ColumnNumber)
                .GetFormattedString()
                .Trim();
        }

        return direct;
    }

    private static void ImportWithExcelDataReader(string filePath, Dictionary<string, TableData> result)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            IExcelDataReader reader = ext == ".xls"
                ? ExcelReaderFactory.CreateBinaryReader(stream)
                : ExcelReaderFactory.CreateOpenXmlReader(stream);

            using (reader)
            {
                var ds = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                });

                for (int i = 0; i < ds.Tables.Count; i++)
                {
                    var dt = ds.Tables[i];
                    var td = new TableData();

                    var rawHeaders = new List<string?>();
                    for (int c = 0; c < dt.Columns.Count; c++)
                        rawHeaders.Add(dt.Columns[c]?.ColumnName);
                    td.Headers = HeaderNormalizationService.Normalize(rawHeaders, treatGeneratedColumnNamesAsEmpty: true);

                    foreach (DataRow r in dt.Rows)
                    {
                        var row = new List<string>();
                        for (int c = 0; c < dt.Columns.Count; c++)
                            row.Add(r[c]?.ToString()?.Trim() ?? "");
                        td.Rows.Add(row);
                    }
                    result[dt.TableName ?? $"Sheet{i + 1}"] = td;
                }
            }
        }
        catch { /* let caller handle */ }
    }

    /// <summary>导出为 XLSX</summary>
    public static bool ExportToXlsx(TableData data, string filePath)
    {
        try
        {
            TableDataValidator.EnsureValid(data, "XLSX export input");
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Sheet1");

            for (int c = 0; c < data.ColumnCount; c++)
                ws.Cell(1, c + 1).Value = data.Headers[c];

            for (int r = 0; r < data.RowCount; r++)
                for (int c = 0; c < data.ColumnCount; c++)
                    ws.Cell(r + 2, c + 1).Value = data.Rows[r][c] ?? "";

            ws.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
            return true;
        }
        catch { return false; }
    }
}
