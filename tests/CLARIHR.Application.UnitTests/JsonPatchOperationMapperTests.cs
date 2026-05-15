using System.Text.Json;
using CLARIHR.Api.Common;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Newtonsoft.Json.Linq;

namespace CLARIHR.Application.UnitTests;

public sealed class JsonPatchOperationMapperTests
{
    private static readonly Guid ExpectedGuid = Guid.Parse("4f48686f-2de8-48d8-a999-e460af509ff4");
    private static readonly DateTime ExpectedDateTime = new(2026, 5, 14, 12, 34, 56, DateTimeKind.Utc);

    public static IEnumerable<object?[]> PatchValues()
    {
        yield return ["null", null];
        yield return ["string", new JValue("Analista senior")];
        yield return ["int", new JValue(42)];
        yield return ["decimal", new JValue(12345.67m)];
        yield return ["guid", new JValue(ExpectedGuid)];
        yield return ["dateTime", new JValue(ExpectedDateTime)];
        yield return ["array", JArray.FromObject(new object[] { 1, "two", true })];
        yield return ["object", JObject.FromObject(new { code = "JP-001", sortOrder = 3 })];
        yield return ["nativeObject", new NativeValue("JP-002", 4)];
    }

    [Theory]
    [MemberData(nameof(PatchValues))]
    public void Map_ShouldTransferOperationMetadataAndConvertNewtonsoftValues(string caseName, object? value)
    {
        var patchDoc = new JsonPatchDocument<TestPatchRequest>();
        patchDoc.Operations.Add(new Operation<TestPatchRequest>(
            "copy",
            "/target",
            "/source",
            value));

        var mapped = JsonPatchOperationMapper.Map(
            patchDoc,
            static (op, path, from, mappedValue) => new MappedPatchOperation(op, path, from, mappedValue));

        var operation = Assert.Single(mapped);
        Assert.Equal("copy", operation.Op);
        Assert.Equal("/target", operation.Path);
        Assert.Equal("/source", operation.From);

        AssertMappedValue(caseName, operation.Value);
    }

    private static void AssertMappedValue(string caseName, JsonElement? value)
    {
        Assert.NotNull(value);
        var element = value!.Value;

        switch (caseName)
        {
            case "null":
                Assert.Equal(JsonValueKind.Null, element.ValueKind);
                break;
            case "string":
                Assert.Equal("Analista senior", element.GetString());
                break;
            case "int":
                Assert.Equal(42, element.GetInt32());
                break;
            case "decimal":
                Assert.Equal(12345.67m, element.GetDecimal());
                break;
            case "guid":
                Assert.Equal(ExpectedGuid, element.GetGuid());
                break;
            case "dateTime":
                Assert.Equal(ExpectedDateTime, element.GetDateTime());
                break;
            case "array":
                var items = element.EnumerateArray().ToArray();
                Assert.Equal(3, items.Length);
                Assert.Equal(1, items[0].GetInt32());
                Assert.Equal("two", items[1].GetString());
                Assert.True(items[2].GetBoolean());
                break;
            case "object":
                Assert.Equal("JP-001", element.GetProperty("code").GetString());
                Assert.Equal(3, element.GetProperty("sortOrder").GetInt32());
                break;
            case "nativeObject":
                Assert.Equal("JP-002", element.GetProperty("code").GetString());
                Assert.Equal(4, element.GetProperty("sortOrder").GetInt32());
                break;
            default:
                throw new InvalidOperationException($"Unexpected case '{caseName}'.");
        }
    }

    private sealed class TestPatchRequest;
    private sealed record NativeValue(string Code, int SortOrder);

    private sealed record MappedPatchOperation(
        string Op,
        string Path,
        string? From,
        JsonElement? Value);
}
