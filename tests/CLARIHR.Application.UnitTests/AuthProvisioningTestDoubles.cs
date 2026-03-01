using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Provisioning;
using CLARIHR.Application.Features.Provisioning.Common;

namespace CLARIHR.Application.UnitTests;

internal sealed class TestUnitOfWork : IUnitOfWork
{
    public TestUnitOfWorkTransaction Transaction { get; } = new();

    public int SaveChangesCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        return Task.FromResult(1);
    }

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUnitOfWorkTransaction>(Transaction);
}

internal sealed class TestUnitOfWorkTransaction : IUnitOfWorkTransaction
{
    public bool CommitCalled { get; private set; }

    public bool RollbackCalled { get; private set; }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        CommitCalled = true;
        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        RollbackCalled = true;
        return Task.CompletedTask;
    }
}

internal sealed class TestProvisioningCommandDispatcher : ICommandDispatcher
{
    public ProvisionCompanyForUserCommand? LastCommand { get; private set; }

    public Result<ProvisionCompanyForUserResult> NextResult { get; set; } =
        Result<ProvisionCompanyForUserResult>.Success(new ProvisionCompanyForUserResult(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            AlreadyProvisioned: false,
            ProvisioningConstants.FreePlanCode));

    public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        if (command is ProvisionCompanyForUserCommand provisioningCommand &&
            typeof(TResponse) == typeof(ProvisionCompanyForUserResult))
        {
            LastCommand = provisioningCommand;
            return Task.FromResult((Result<TResponse>)(object)NextResult);
        }

        throw new NotSupportedException($"Unsupported command type {command.GetType().Name}.");
    }
}
