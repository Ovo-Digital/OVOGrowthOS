using ClosedXML.Excel;

namespace OvoGrowthOS.Api.Features;

internal static class ExcelExport
{
    public const string MimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Build(string sheetName, string title, IReadOnlyList<string> headers, IEnumerable<object?[]> rows, bool autoFilter = true)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        const int headerRow = 3;
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F2F2F2"));
            cell.Style.Alignment.WrapText = true;
        }
        var rowNumber = headerRow + 1;
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length && i < headers.Count; i++) Write(sheet.Cell(rowNumber, i + 1), row[i]);
            rowNumber++;
        }
        if (autoFilter) sheet.Range(headerRow, 1, headerRow, headers.Count).SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);
        for (var i = 1; i <= headers.Count; i++)
        {
            var column = sheet.Column(i);
            var width = column.CellsUsed().Select(c => c.GetString().Length + 2).DefaultIfEmpty(10).Max();
            column.Width = Math.Clamp(width, 10, 45);
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Write(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: cell.Value = Blank.Value; break;
            case decimal number: cell.Value = number; cell.Style.NumberFormat.Format = "#,##0.00##"; break;
            case double number: cell.Value = number; cell.Style.NumberFormat.Format = "#,##0.00##"; break;
            case float number: cell.Value = number; cell.Style.NumberFormat.Format = "#,##0.00##"; break;
            case int number: cell.Value = number; break;
            case long number: cell.Value = number; break;
            case DateOnly date: cell.Value = date.ToDateTime(TimeOnly.MinValue); cell.Style.NumberFormat.Format = "dd.MM.yyyy"; break;
            case DateTime date: cell.Value = date; cell.Style.NumberFormat.Format = "dd.MM.yyyy"; break;
            default: cell.Value = value.ToString() ?? ""; break;
        }
    }
}
