namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Canonical groups of <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> responses
/// that endpoints may declare via <see cref="ProducesStandardErrorsAttribute"/>.
/// Aliases map intent (e.g. <see cref="Command"/>, <see cref="Read"/>) to the underlying
/// HTTP status code flags so action-level attributes stay declarative.
/// </summary>
[Flags]
public enum StandardErrorSet
{
    None = 0,

    BadRequest = 1 << 0,
    Unauthorized = 1 << 1,
    Forbidden = 1 << 2,
    NotFound = 1 << 3,
    Conflict = 1 << 4,
    UnprocessableEntity = 1 << 5,

    /// <summary>401 + 403 — baseline for any authenticated endpoint.</summary>
    Auth = Unauthorized | Forbidden,

    /// <summary>401 + 403 + 404 — GET-by-id and other single-resource reads.</summary>
    Read = Auth | NotFound,

    /// <summary>400 + 401 + 403 — list/search endpoints with query-string validation.</summary>
    Query = Auth | BadRequest,

    /// <summary>400 + 401 + 403 + 404 + 409 — sub-resource Add/Update/Remove.</summary>
    SubResourceWrite = Read | BadRequest | Conflict,

    /// <summary>400 + 401 + 403 + 404 + 409 + 422 — aggregate-root create/update/patch.</summary>
    Command = SubResourceWrite | UnprocessableEntity,
}
