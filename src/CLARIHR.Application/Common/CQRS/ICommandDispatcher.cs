using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Common.CQRS;

public interface ICommandDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
}
