namespace CLARIHR.Application.Common.Errors;

public enum ErrorType
{
    Validation = 1,
    UnprocessableEntity = 2,
    Unauthorized = 3,
    Forbidden = 4,
    NotFound = 5,
    Conflict = 6,
    TooManyRequests = 7,
    Failure = 8,
    Unexpected = 9
}
