using CLARIHR.Api.Controllers;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Application.UnitTests;

public sealed class PersonnelFileEmploymentControllerTests
{
    [Fact]
    public async Task Finalize_WhenCreateUserAccountIsOmitted_ShouldDefaultToTrue()
    {
        var dispatcher = new CaptureFinalizeCommandDispatcher();
        var queryDispatcher = new CapturePreviewQueryDispatcher();
        var controller = new PersonnelFileEmploymentController(
            dispatcher,
            queryDispatcher,
            new NoOpAuditService(),
            new NoOpUnitOfWork());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _ = await controller.Finalize(
            Guid.NewGuid(),
            new FinalizePersonnelFileRequest(Guid.NewGuid(), CreateUserAccount: null),
            CancellationToken.None);

        Assert.NotNull(dispatcher.LastCommand);
        Assert.True(dispatcher.LastCommand!.CreateUserAccount);
    }

    [Fact]
    public async Task PreviewFinalize_WhenCreateUserAccountIsOmitted_ShouldDefaultToTrue()
    {
        var commandDispatcher = new CaptureFinalizeCommandDispatcher();
        var queryDispatcher = new CapturePreviewQueryDispatcher();
        var controller = new PersonnelFileEmploymentController(
            commandDispatcher,
            queryDispatcher,
            new NoOpAuditService(),
            new NoOpUnitOfWork());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        _ = await controller.PreviewFinalize(
            Guid.NewGuid(),
            new FinalizePersonnelFilePreviewRequest(CreateUserAccount: null),
            CancellationToken.None);

        Assert.NotNull(queryDispatcher.LastQuery);
        Assert.True(queryDispatcher.LastQuery!.CreateUserAccount);
    }

    private sealed class CaptureFinalizeCommandDispatcher : ICommandDispatcher
    {
        public FinalizePersonnelFileCommand? LastCommand { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            if (command is FinalizePersonnelFileCommand finalizeCommand)
            {
                LastCommand = finalizeCommand;
            }

            var error = new Error("tests.capture", "Synthetic response for command-capture test.", ErrorType.Validation);
            return Task.FromResult(Result<TResponse>.Failure(error));
        }
    }

    private sealed class ThrowingQueryDispatcher : IQueryDispatcher
    {
        public Task<Result<TResponse>> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturePreviewQueryDispatcher : IQueryDispatcher
    {
        public PreviewFinalizePersonnelFileQuery? LastQuery { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            if (query is PreviewFinalizePersonnelFileQuery previewQuery)
            {
                LastQuery = previewQuery;
            }

            var error = new Error("tests.capture", "Synthetic response for query-capture test.", ErrorType.Validation);
            return Task.FromResult(Result<TResponse>.Failure(error));
        }
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
