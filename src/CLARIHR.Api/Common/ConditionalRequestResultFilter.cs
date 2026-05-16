using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Net.Http.Headers;

namespace CLARIHR.Api.Common;

public sealed class ConditionalRequestResultFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (!IsConditionalReadCandidate(context) ||
            context.Result is not ObjectResult { Value: not null } objectResult ||
            !TryBuildHeaders(objectResult.Value, out var eTag, out var lastModifiedUtc))
        {
            await next();
            return;
        }

        var response = context.HttpContext.Response;
        response.Headers[HeaderNames.ETag] = eTag;
        if (lastModifiedUtc.HasValue)
        {
            response.Headers[HeaderNames.LastModified] = FormatHttpDate(lastModifiedUtc.Value);
        }

        if (ETagHeader.Matches(context.HttpContext.Request.Headers.IfNoneMatch.ToString(), eTag))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            context.Cancel = true;
            return;
        }

        await next();
    }

    private static bool IsConditionalReadCandidate(ResultExecutingContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
        {
            return false;
        }

        return context.Result switch
        {
            ObjectResult { StatusCode: null } => true,
            ObjectResult { StatusCode: StatusCodes.Status200OK } => true,
            _ => false
        };
    }

    private static bool TryBuildHeaders(object value, out string eTag, out DateTimeOffset? lastModifiedUtc)
    {
        if (TryReadDirectMetadata(value, out var directToken, out lastModifiedUtc) &&
            directToken is Guid token)
        {
            eTag = ETagHeader.Format(token);
            return true;
        }

        if (TryReadPagedItems(value, out var pagedItems, out var pageMetadata))
        {
            return TryBuildAggregateHeaders(pagedItems, pageMetadata, out eTag, out lastModifiedUtc);
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            return TryBuildAggregateHeaders(enumerable, pageMetadata: null, out eTag, out lastModifiedUtc);
        }

        eTag = string.Empty;
        lastModifiedUtc = null;
        return false;
    }

    private static bool TryReadDirectMetadata(object value, out Guid? concurrencyToken, out DateTimeOffset? lastModifiedUtc)
    {
        concurrencyToken = ReadGuidProperty(value, "ConcurrencyToken");
        lastModifiedUtc = ReadDateTimeProperty(value, "ModifiedAtUtc") ?? ReadDateTimeProperty(value, "CreatedAtUtc");

        return concurrencyToken.HasValue;
    }

    private static bool TryReadPagedItems(object value, out IEnumerable items, out string pageMetadata)
    {
        var type = value.GetType();
        var itemsProperty = type.GetProperty("Items", BindingFlags.Instance | BindingFlags.Public);
        var pageNumber = ReadIntProperty(value, "PageNumber");
        var pageSize = ReadIntProperty(value, "PageSize");
        var totalCount = ReadIntProperty(value, "TotalCount");

        if (itemsProperty?.GetValue(value) is IEnumerable enumerable &&
            pageNumber.HasValue &&
            pageSize.HasValue &&
            totalCount.HasValue)
        {
            items = enumerable;
            pageMetadata = FormattableString.Invariant($"page={pageNumber.Value};pageSize={pageSize.Value};totalCount={totalCount.Value}");
            return true;
        }

        items = Array.Empty<object>();
        pageMetadata = string.Empty;
        return false;
    }

    private static bool TryBuildAggregateHeaders(
        IEnumerable items,
        string? pageMetadata,
        out string eTag,
        out DateTimeOffset? lastModifiedUtc)
    {
        var components = new List<string>();
        lastModifiedUtc = null;

        if (!string.IsNullOrWhiteSpace(pageMetadata))
        {
            components.Add(pageMetadata);
        }

        foreach (var item in items)
        {
            if (item is null)
            {
                continue;
            }

            if (TryReadDirectMetadata(item, out var token, out var itemLastModifiedUtc))
            {
                if (token.HasValue)
                {
                    components.Add(token.Value.ToString("D", CultureInfo.InvariantCulture));
                }

                if (itemLastModifiedUtc.HasValue &&
                    (!lastModifiedUtc.HasValue || itemLastModifiedUtc.Value > lastModifiedUtc.Value))
                {
                    lastModifiedUtc = itemLastModifiedUtc.Value;
                }
            }
        }

        if (components.Count == 0)
        {
            eTag = string.Empty;
            return false;
        }

        eTag = ETagHeader.Format(ComputeStableHash(components));
        return true;
    }

    private static Guid? ReadGuidProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(value) is Guid token)
        {
            return token;
        }

        return null;
    }

    private static int? ReadIntProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(value) is int number)
        {
            return number;
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTimeProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        return property?.GetValue(value) switch
        {
            DateTime dateTime => Normalize(dateTime),
            DateTimeOffset dateTimeOffset => Normalize(dateTimeOffset),
            _ => null
        };
    }

    private static DateTimeOffset Normalize(DateTime dateTime)
    {
        var utc = dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : dateTime.ToUniversalTime();

        return Normalize(new DateTimeOffset(utc, TimeSpan.Zero));
    }

    private static DateTimeOffset Normalize(DateTimeOffset dateTimeOffset)
    {
        var utc = dateTimeOffset.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            utc.Second,
            TimeSpan.Zero);
    }

    private static string FormatHttpDate(DateTimeOffset dateTimeOffset) =>
        dateTimeOffset.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);

    private static string ComputeStableHash(IReadOnlyCollection<string> components)
    {
        var joined = string.Join('|', components);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
