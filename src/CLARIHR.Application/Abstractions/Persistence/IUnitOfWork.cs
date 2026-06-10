namespace CLARIHR.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// AC-4: detaches all tracked entities so a failed attempt can be retried on a fresh transaction without
    /// the prior attempt's Added entities lingering in the change tracker (the create-company duplicate-slug
    /// retry). No-op effect on the database. The default is a no-op (test fakes); the EF unit of work clears
    /// the change tracker.
    /// </summary>
    void ClearTracked() { }
}
