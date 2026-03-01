using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IFieldSerializationService
{
    string? SerializeString(FieldAccessRule rule, string? value);

    Guid? SerializeGuid(FieldAccessRule rule, Guid value);

    TEnum? SerializeEnum<TEnum>(FieldAccessRule rule, TEnum value)
        where TEnum : struct, Enum;
}
