using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Common.CQRS;

public interface IQueryDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}
