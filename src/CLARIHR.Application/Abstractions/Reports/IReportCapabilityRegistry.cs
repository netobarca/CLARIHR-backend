using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Abstractions.Reports;

public interface IReportCapabilityRegistry
{
    bool TryGet(string resourceKey, out ReportCapabilityDefinition definition);
}
