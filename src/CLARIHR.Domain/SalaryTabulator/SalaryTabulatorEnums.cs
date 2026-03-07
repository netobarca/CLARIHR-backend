namespace CLARIHR.Domain.SalaryTabulator;

public enum SalaryTabulatorChangeRequestStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Canceled = 5
}

public enum SalaryTabulatorChangeType
{
    Create = 1,
    Update = 2,
    Inactivate = 3
}
