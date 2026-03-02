namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyOwnershipPolicy
{
    Task<bool> HasCapacityForAnotherActiveCompanyAsync(Guid ownerUserPublicId, CancellationToken cancellationToken);
}
