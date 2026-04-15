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
            PersonnelFileResponse response => await ResolvePersonnelFileResponseAsync(response, cancellationToken),
            PersonnelFilePersonalInfoResponse response => await ResolvePersonalInfoResponseAsync(response, cancellationToken),
            FinalizePersonnelFileResponse response => await ResolveFinalizeResponseAsync(response, cancellationToken),
            PersonnelFilePrintResponse response => await ResolvePrintResponseAsync(response, cancellationToken),
            _ => value
        };
    }

    private async Task<PersonnelFileResponse> ResolvePersonnelFileResponseAsync(
        PersonnelFileResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(response.PhotoUrl, cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl };
    }

    private async Task<PersonnelFilePersonalInfoResponse> ResolvePersonalInfoResponseAsync(
        PersonnelFilePersonalInfoResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(response.PhotoUrl, cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl };
    }

    private async Task<FinalizePersonnelFileResponse> ResolveFinalizeResponseAsync(
        FinalizePersonnelFileResponse response,
        CancellationToken cancellationToken)
    {
        var personnelFile = await ResolvePersonnelFileResponseAsync(response.PersonnelFile, cancellationToken);
        return response with { PersonnelFile = personnelFile };
    }

    private async Task<PersonnelFilePrintResponse> ResolvePrintResponseAsync(
        PersonnelFilePrintResponse response,
        CancellationToken cancellationToken)
    {
        var personnelFile = await ResolvePersonnelFileResponseAsync(response.PersonnelFile, cancellationToken);
        return response with { PersonnelFile = personnelFile };
    }
}
