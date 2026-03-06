namespace CLARIHR.Domain.JobProfiles;

public enum JobProfileStatus
{
    Draft = 1,
    Published = 2,
    Archived = 3
}

public enum JobCatalogCategory
{
    EducationLevel = 1,
    KnowledgeArea = 2,
    Competency = 3,
    Training = 4,
    SalaryClass = 5,
    BenefitType = 6,
    WorkingCondition = 7,
    RelationType = 8,
    DecisionLevel = 9
}

public enum JobRequirementType
{
    Education = 1,
    Experience = 2,
    Knowledge = 3,
    Certification = 4,
    Other = 5
}

public enum JobFunctionType
{
    General = 1,
    Specific = 2
}

public enum JobRelationType
{
    Internal = 1,
    External = 2
}
