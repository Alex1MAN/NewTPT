using System.Text;
using Elchwinkel.Spc;
using ExcelDataReader;
using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Application.Models;

namespace JWTAuthTemplate.Infrastructure.FileParsing;

/// <summary>Чтение Excel (ExcelDataReader) и SPC (elch-spc) в текстовую таблицу.</summary>
public sealed class TableFileReader : ITableFileReader
{
    static TableFileReader()
    {
        // Кодировки нужны ExcelDataReader для старого формата .xls.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public TableData ReadExcel(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet();
        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count == 0)
            throw new ArgumentException("Excel file is empty.");

        var table = dataSet.Tables[0];
        int columns = table.Columns.Count;
        string Cell(int row, int col) => table.Rows[row][col]?.ToString() ?? "";

        var labels = Enumerable.Range(0, columns).Select(col => Cell(0, col)).ToArray();
        var rows = Enumerable.Range(1, table.Rows.Count - 1)
            .Select(row => Enumerable.Range(0, columns).Select(col => Cell(row, col)).ToArray())
            .ToList();
        return new TableData(labels, rows);
    }

    public TableData ReadSpc(byte[] content)
    {
        var spc = SpcReader.Read(content);
        var spectrum = spc.Spectra[0];
        var rows = new List<string[]>(spectrum.X.Length);
        for (int i = 0; i < spectrum.X.Length; i++)
            rows.Add(new[] { $"{spectrum.X[i]}", $"{spectrum.Y[i]}" });
        return new TableData(new[] { spc.XUnit.Name, spc.YUnit.Name }, rows);
    }
}
