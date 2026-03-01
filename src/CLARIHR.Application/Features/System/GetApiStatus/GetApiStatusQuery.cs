using CLARIHR.Application.Common.CQRS;

namespace CLARIHR.Application.Features.System.GetApiStatus;

public sealed record GetApiStatusQuery : IQuery<ApiStatusResponse>;
