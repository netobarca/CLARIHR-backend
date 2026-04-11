namespace CLARIHR.Domain.PersonnelFiles;

public enum PersonnelFileRecordType
{
    Candidate = 1,
    Employee = 2
}

public enum PersonnelFileLifecycleStatus
{
    Draft = 1,
    Completed = 2
}

public enum PersonnelCustomFieldType
{
    String = 1,
    Number = 2,
    Date = 3,
    Bool = 4,
    Select = 5
}

public enum PersonnelFamilyMemberSex
{
    Unknown = 0,
    Female = 1,
    Male = 2,
    Other = 3
}
