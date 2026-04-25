using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CLARIHR.Api.Common;

public sealed class PersonnelFilePhotoUrlResultFilter(
    IPersonnelFileProfilePhotoService profilePhotoService,
    IPersonnelFileDocumentStorageService documentStorageService) : IAsyncResultFilter
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
            IReadOnlyCollection<PersonnelFileDocumentMetadataResponse> responses => await ResolveDocumentCollectionAsync(responses, cancellationToken),
            PersonnelFileDocumentMetadataResponse response => await ResolveDocumentResponseAsync(response, cancellationToken),
            PersonnelFileResponse response => await ResolvePersonnelFileResponseAsync(response, cancellationToken),
            PersonnelFilePersonalInfoResponse response => await ResolvePersonalInfoResponseAsync(response, cancellationToken),
            PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse> response => await ResolvePersonalInfoSectionResultAsync(response, cancellationToken),
            FinalizePersonnelFileResponse response => await ResolveFinalizeResponseAsync(response, cancellationToken),
            PersonnelFilePrintResponse response => await ResolvePrintResponseAsync(response, cancellationToken),
            _ => value
        };
    }

    private async Task<PersonnelFileShellResponse> ResolveShellResponseAsync(
        PersonnelFileShellResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(response.PhotoUrl, cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl };
    }

    private async Task<PersonnelFileResponse> ResolvePersonnelFileResponseAsync(
        PersonnelFileResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedPhotoUrl = await profilePhotoService.ResolveForReadAsync(response.PhotoUrl, cancellationToken);
        var resolvedDocuments = await ResolveDocumentCollectionAsync(response.Documents, cancellationToken);
        return response with { PhotoUrl = resolvedPhotoUrl, Documents = resolvedDocuments };
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

    private async Task<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>> ResolveDocumentCollectionAsync(
        IReadOnlyCollection<PersonnelFileDocumentMetadataResponse> responses,
        CancellationToken cancellationToken)
    {
        if (responses.Count == 0)
        {
            return responses;
        }

        var resolved = new List<PersonnelFileDocumentMetadataResponse>(responses.Count);
        foreach (var response in responses)
        {
            resolved.Add(await ResolveDocumentResponseAsync(response, cancellationToken));
        }

        return resolved;
    }

    private async Task<PersonnelFileDocumentMetadataResponse> ResolveDocumentResponseAsync(
        PersonnelFileDocumentMetadataResponse response,
        CancellationToken cancellationToken)
    {
        var resolvedFileUrl = await documentStorageService.ResolveForReadAsync(response.FileUrl, cancellationToken);
        return response with { FileUrl = resolvedFileUrl };
    }
}
