using System.Collections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PDFHub.Application.Abstractions;

namespace PDFHub.Infrastructure.Persistence;

public class Repository<T>(AppDbContext context) : IRepository<T> where T : class
{
    private readonly DbSet<T> _dbSet = context.Set<T>();

    public T New() => Activator.CreateInstance<T>();

    public IQueryable<T> GetAll() => _dbSet;

    public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

    public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

    public Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity) => RemoveAsync(entity);

    public Task DeleteRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync() => await context.SaveChangesAsync();
}

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    private Hashtable? _repositories;

    public IRepository<T> Repository<T>() where T : class
    {
        _repositories ??= [];
        var type = typeof(T).Name;

        if (!_repositories.ContainsKey(type))
            _repositories.Add(type, new Repository<T>(context));

        return _repositories[type] as IRepository<T>
            ?? throw new InvalidOperationException($"Repository for type '{type}' could not be created.");
    }

    public async Task<int> CommitAsync() => await context.SaveChangesAsync();

    public IDbContextTransaction BeginTransaction() => context.Database.BeginTransaction();

    public void ClearTrackedChanges() => context.ChangeTracker.Clear();

    public void Dispose()
    {
        // The DbContext is owned by the DI scope, which disposes it.
        GC.SuppressFinalize(this);
    }
}
