using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Exceptions;
using PDFHub.Domain;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Application.Services;

public interface IImportService
{
    /// <summary>Imports drawing rows from an .xlsx laid out like the original sheet (or an export from this app).</summary>
    Task<ImportResultDto> ImportAsync(Stream xlsx, ImportMode mode, bool dryRun, CancellationToken ct = default);
}

public sealed class ImportService(IUnitOfWork uow, IDrawingSpreadsheet spreadsheet, IDateTime clock) : IImportService
{
    // The old sheet ticks "Have a PO" with a Wingdings "ü"; anything except these counts as ticked.
    // A tick carries no number, so it becomes PdfCodeRules.PoWithoutNumber unless the sheet also has a PO No.
    private static readonly string[] FalseWords = ["0", "false", "no", "n", "-", "ไม่มี"];

    public async Task<ImportResultDto> ImportAsync(Stream xlsx, ImportMode mode, bool dryRun, CancellationToken ct = default)
    {
        IReadOnlyList<SpreadsheetSheet> sheets;
        try
        {
            sheets = spreadsheet.Read(xlsx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The Excel library throws many unrelated exception types for damaged or non-Excel files.
            throw new BusinessRuleException($"อ่านไฟล์ Excel ไม่ได้ ตรวจว่าเป็นไฟล์ .xlsx ที่เปิดใน Excel ได้ ({ex.Message})");
        }
        var sections = await uow.Repository<Section>().GetAll().ToDictionaryAsync(s => s.Code, ct);
        var drawings = uow.Repository<Drawing>();
        var existing = await drawings.GetAll().ToDictionaryAsync(d => d.PdfCode, ct);
        var seen = new HashSet<string>();
        var result = new ImportResultDto { DryRun = dryRun };

        foreach (var sheet in sheets)
        {
            result.Sheets.Add(sheet.Name);
            foreach (var row in sheet.Rows)
            {
                result.RowsRead++;
                void Error(string message) => result.Errors.Add(new(sheet.Name, row.RowNumber, row.Code, message));
                void Warn(string message) => result.Warnings.Add(new(sheet.Name, row.RowNumber, row.Code, message));

                var code = PdfCodeRules.Normalize(row.Code);
                if (PdfCodeRules.FormatError(code) is { } formatError) { Error(formatError); continue; }
                if (!sections.TryGetValue(code[..2], out var section)) { Error(PdfCodeRules.UnknownSection(code[..2])); continue; }
                if (!seen.Add(code)) { Error($"PdfCode {code} ซ้ำกันในไฟล์"); continue; }
                if (row.PartName is null) { Error("ไม่มี PartName"); continue; }

                if (row.PriceText is not null && row.Price is null)
                    Warn($"อ่านราคา \"{row.PriceText}\" ไม่ได้ จึงเว้นว่างไว้");
                var inputDate = row.InputDate;
                if (inputDate is null)
                {
                    inputDate = DateOnly.FromDateTime(clock.Now);
                    Warn(row.InputDateText is null
                        ? "ไม่มี InputDate จึงใช้วันที่นำเข้าแทน"
                        : $"อ่านวันที่ \"{row.InputDateText}\" ไม่ได้ จึงใช้วันที่นำเข้าแทน");
                }

                if (existing.TryGetValue(code, out var drawing))
                {
                    if (mode == ImportMode.SkipExisting) { result.Skipped++; continue; }
                    result.Updated++;
                }
                else
                {
                    drawing = drawings.New();
                    drawing.PdfCode = code;
                    existing[code] = drawing;
                    if (!dryRun) await drawings.AddAsync(drawing);
                    result.Added++;
                }

                if (dryRun)
                    continue;
                drawing.SectionId = section.Id;
                drawing.PartName = row.PartName;
                drawing.DrawingNo = row.DrawingNo;
                drawing.Material = row.Material;
                drawing.Price = row.Price;
                drawing.InputDate = inputDate.Value;
                drawing.QuoNo = row.QuoNo;
                drawing.Remark = row.Remark;
                drawing.PoNo = row.PoNo ?? (row.LegacyPoTick is { } tick && !FalseWords.Contains(tick, StringComparer.OrdinalIgnoreCase)
                    ? PdfCodeRules.PoWithoutNumber
                    : null);
            }
        }

        if (result.Sheets.Count == 0)
            result.Errors.Add(new("-", 0, null, "ไม่พบหัวตารางที่มีคอลัมน์ PdfCodeS ในไฟล์นี้"));

        if (!dryRun)
            await uow.CommitAsync();
        return result;
    }
}
