using CLARIHR.Api.Controllers;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit;
using CLARIHR.Application.Features.Audit.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

public sealed class AuditControllerTests
{
    [Fact]
    public async Task List_WhenBoundQueryMissesLegacyEntityId_ShouldDispatchNormalizedEntityId()
    {
        var entityId = Guid.Parse("ec978279-7f00-4fc6-b97d-6f0d7fa5672e");
        var dispatcher = new CapturingQueryDispatcher();
        var controller = CreateController(
            dispatcher,
            $"?Page=1&PageSize=100&EntityId={entityId:D}&EntityType=Role");

        _ = await controller.List(
            new GetAuditLogsQuery(EntityType: AuditEntityTypes.Role, Page: 1, PageSize: 100),
            CancellationToken.None);

        Assert.NotNull(dispatcher.CapturedAuditLogsQuery);
        Assert.Equal(entityId, dispatcher.CapturedAuditLogsQuery!.EntityId);
        Assert.Equal(AuditEntityTypes.Role, dispatcher.CapturedAuditLogsQuery.EntityType);
    }

    [Fact]
    public async Task List_WhenBoundQueryMissesPublicEntityIdAlias_ShouldDispatchNormalizedEntityId()
    {
        var entityId = Guid.Parse("acf9c078-cbd6-499a-8ed3-a73e857cb030");
        var dispatcher = new CapturingQueryDispatcher();
        var controller = CreateController(
            dispatcher,
            $"?Page=1&PageSize=100&EntityPublicId={entityId:D}&EntityType=CostCenter");

        _ = await controller.List(
            new GetAuditLogsQuery(EntityType: AuditEntityTypes.CostCenter, Page: 1, PageSize: 100),
            CancellationToken.None);

        Assert.NotNull(dispatcher.CapturedAuditLogsQuery);
        Assert.Equal(entityId, dispatcher.CapturedAuditLogsQuery!.EntityId);
        Assert.Equal(AuditEntityTypes.CostCenter, dispatcher.CapturedAuditLogsQuery.EntityType);
    }

    [Fact]
    public async Task List_WhenModelBindingAlreadyResolvedEntityId_ShouldNotOverrideBoundValue()
    {
        var boundEntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var conflictingQueryEntityId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var dispatcher = new CapturingQueryDispatcher();
        var controller = CreateController(
            dispatcher,
            $"?Page=1&PageSize=100&EntityId={conflictingQueryEntityId:D}&EntityType=Role");

        _ = await controller.List(
            new GetAuditLogsQuery(EntityId: boundEntityId, EntityType: AuditEntityTypes.Role, Page: 1, PageSize: 100),
            CancellationToken.None);

        Assert.NotNull(dispatcher.CapturedAuditLogsQuery);
        Assert.Equal(boundEntityId, dispatcher.CapturedAuditLogsQuery!.EntityId);
    }

    private static AuditController CreateController(IQueryDispatcher dispatcher, string queryString)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.QueryString = new QueryString(queryString);

        return new AuditController(dispatcher, NullLogger<AuditController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private sealed class CapturingQueryDispatcher : IQueryDispatcher
    {
        public GetAuditLogsQuery? CapturedAuditLogsQuery { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            if (query is not GetAuditLogsQuery auditLogsQuery)
            {
                throw new NotSupportedException($"Unexpected query type {query.GetType().FullName}.");
            }

            CapturedAuditLogsQuery = auditLogsQuery;

            var payload = (TResponse)(object)new PagedResponse<AuditLogSummaryResponse>(
                Array.Empty<AuditLogSummaryResponse>(),
                auditLogsQuery.Page,
                auditLogsQuery.PageSize,
                0);

            return Task.FromResult(Result<TResponse>.Success(payload));
        }
    }
}
