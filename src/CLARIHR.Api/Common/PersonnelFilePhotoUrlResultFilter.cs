using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CLARIHR.Api.Common;

public sealed class PersonnelFilePhotoUrlResultFilter(
    IPersonnelFileProfilePhotoService profilePhotoService) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: not null } objectResult)
        {
            objectResult.Value = await ResolveAsync(objectResult.Value, context.HttpContext.RequestAborted);
        }

        await next();
    }

    private async Task<object> ResolveAsync(object value, CancellationToken cancellationToken)
    {
        return value switch
        {
            PersonnelFileShellResponse response => await ResolveShellResponseAsync(response, cancellationToken),
            PersonnelFileResponse response => await ResolvePersonnelFileResponseAsync(response, cancellationToken),
            PersonnelFilePersonalInfoResponse response => await ResolvePersonalInfoResponseAsync(response, cancellationToken),
            PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse> response => await ResolvePersonalInfoSectionResultAsync(response, cancellationToken),
            FinalizePersonnelFileResponse response => await ResolveFinalizeResponseAsync(response, cancellationToken),
            _ => value
        };
    }

    private async Task<PersonnelFileShellResponse> ResolveShellResponseAsync(
        PersonnelFileShellResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(ParsePhotoFilePublicId(response.PhotoUrl), cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl };
    }

    private async Task<PersonnelFileResponse> ResolvePersonnelFileResponseAsync(
        PersonnelFileResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(ParsePhotoFilePublicId(response.PhotoUrl), cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl };
    }

    private async Task<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>> ResolvePersonalInfoSectionResultAsync(
        PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse> response,
        CancellationToken cancellationToken)
    {
        var data = await ResolvePersonalInfoResponseAsync(response.Data, cancellationToken);
        return response with { Data = data };
    }

    private async Task<PersonnelFilePersonalInfoResponse> ResolvePersonalInfoResponseAsync(
        PersonnelFilePersonalInfoResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(ParsePhotoFilePublicId(response.PhotoUrl), cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl };
    }

    private async Task<FinalizePersonnelFileResponse> ResolveFinalizeResponseAsync(
        FinalizePersonnelFileResponse response,
        CancellationToken cancellationToken)
    {
        var personnelFile = await ResolvePersonnelFileResponseAsync(response.PersonnelFile, cancellationToken);
        return response with { PersonnelFile = personnelFile };
    }

    private static Guid? ParsePhotoFilePublicId(string? photoUrlOrGuid)
    {
        if (string.IsNullOrWhiteSpace(photoUrlOrGuid))
        {
            return null;
        }

        // Support Guid references from the new file management system
        if (Guid.TryParse(photoUrlOrGuid, out var guid))
        {
            return guid;
        }

        // Legacy: if the value is not a Guid, it's a raw URL — pass through
        return null;
    }
}
