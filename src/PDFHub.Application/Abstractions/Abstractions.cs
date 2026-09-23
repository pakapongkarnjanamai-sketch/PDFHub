using Microsoft.EntityFrameworkCore.Storage;
using PDFHub.Domain.DTOs;

namespace PDFHub.Application.Abstractions;

public interface IRepository<T> where T : class
{
    T New();
    IQueryable<T> GetAll();
    Task<T?> GetByIdAsync(int id);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task RemoveAsync(T entity);
    Task DeleteAsync(T entity);
    Task DeleteRangeAsync(IEnumerable<T> entities);
    Task<int> SaveChangesAsync();
}

public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> CommitAsync();
    IDbContextTransaction BeginTransaction();
    void ClearTrackedChanges();
}

public interface ICurrentUserService
{
    /// <summary>The login name, stamped into CreatedBy/UpdatedBy.</summary>
    string UserId { get; }
    string FullName { get; }
    bool IsAuthenticated { get; }
}

public interface IDateTime
{
    DateTime Now { get; }
    DateTime UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}

/// <summary>
/// PDFs are stored as files ({root}/{section}/{PdfCode}.pdf), not in the database. A replaced or removed
/// file is moved to an archive folder instead of being deleted.
/// </summary>
public interface IPdfStorage
{
    long MaxFileBytes { get; }
    string FullPath(string relativePath);
    Task<string> SaveAsync(string pdfCode, Stream content, CancellationToken ct = default);
    void Archive(string relativePath, string pdfCode, string reason);
    /// <summary>Moves the stored PDF to the path of a new code; returns the new relative path.</summary>
    string Rename(string relativePath, string newCode);
}

public sealed record SpreadsheetRow(
    int RowNumber,
    string? Code,
    string? PartName,
    string? DrawingNo,
    string? Material,
    /// <summary>Raw text of the Price cell, null when the cell is empty.</summary>
    string? PriceText,
    decimal? Price,
    string? InputDateText,
    DateOnly? InputDate,
    string? QuoNo,
    string? Remark,
    string? PoNo,
    /// <summary>The old "Have a PO" tick column (a Wingdings "ü"), when the sheet has one.</summary>
    string? LegacyPoTick);

public sealed record SpreadsheetSheet(string Name, IReadOnlyList<SpreadsheetRow> Rows);

/// <summary>Reads and writes the column layout of the original "NMB-2026" sheet.</summary>
public interface IDrawingSpreadsheet
{
    /// <summary>Every sheet that has a "PdfCodeS" header; sheets without one are ignored.</summary>
    IReadOnlyList<SpreadsheetSheet> Read(Stream xlsx);

    byte[] Write(IReadOnlyList<DrawingListItemDto> drawings, string title, Func<string, string> pdfUrl);
}

public sealed record PdfCopyProgress(int Total, int Copied, int Skipped, long BytesCopied);

/// <summary>File work of a manual backup to a folder the admin chooses (local disk, USB drive or network share).</summary>
public interface IDataBackupStore
{
    /// <summary>Server folder of the automatic daily database copies.</summary>
    string DailyBackupFolder { get; }

    /// <summary>Null when the destination can be written; otherwise a message for the admin.</summary>
    string? CheckDestination(string destination);

    /// <summary>Writes a consistent copy of the live database; returns its path relative to the destination.</summary>
    Task<string> SnapshotDatabaseAsync(string destination, DateTime stamp, CancellationToken ct);

    /// <summary>
    /// Copies PDFs that are new or changed since the last backup to the same destination. Never deletes
    /// at the destination, so a PDF removed by mistake stays recoverable.
    /// </summary>
    Task<PdfCopyProgress> CopyPdfsAsync(string destination, IProgress<PdfCopyProgress> progress, CancellationToken ct);
}

/// <summary>Runs a started backup in the background, so the admin's request returns immediately.</summary>
public interface IBackupQueue
{
    void Enqueue(int backupRunId);
}
