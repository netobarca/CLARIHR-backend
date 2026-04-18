using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CLARIHR.Application.Abstractions.Localization;
using CLARIHR.Infrastructure.Localization;

namespace CLARIHR.Application.UnitTests;

public sealed class BackendMessageLocalizationTests
{
    private static readonly Regex ErrorCodeRegex = new(
        "new(?:\\s+Error)?\\(\\s*\"(?<code>[A-Za-z0-9_.-]+)\"\\s*,\\s*\"[^\"]*\"\\s*,\\s*ErrorType\\.[A-Za-z]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex InlineErrorCodeRegex = new(
        "new\\s+Error\\(\\s*\"(?<code>[A-Za-z0-9_.-]+)\"\\s*,\\s*\"[^\"]*\"\\s*,\\s*ErrorType\\.[A-Za-z]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ValidationWithMessageRegex = new(
        "\\.WithMessage\\(\"(?<message>[^\"]+)\"\\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ValidationAddFailureRegex = new(
        "AddFailure\\([^\\n]*?\"(?<message>[^\"]+)\"\\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ValidationKeyNormalizer = new(
        "[^a-z0-9]+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string RepositoryRoot = ResolveRepositoryRoot();
    private static readonly string ApplicationPath = Path.Combine(RepositoryRoot, "src", "CLARIHR.Application");
    private static readonly string EnglishResourcePath = Path.Combine(RepositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx");
    private static readonly string SpanishResourcePath = Path.Combine(RepositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.es.resx");

    [Fact]
    public void Localize_WhenCompanyNotFoundAndCultureIsSpanish_ShouldReturnSpanishMessage()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var localizer = new ResourceBackendMessageLocalizer();

        try
        {
            var culture = CultureInfo.GetCultureInfo("es");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;

            var localized = localizer.Localize(
                "COMPANY_NOT_FOUND",
                "The requested company could not be found.");

            Assert.NotEqual("The requested company could not be found.", localized);
            Assert.False(string.IsNullOrWhiteSpace(localized));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void ResourceCatalog_ShouldContainAllApplicationErrorCodes_InEnglishAndSpanish()
    {
        var applicationFiles = GetApplicationFiles();
        var englishKeys = LoadResourceKeys(EnglishResourcePath);
        var spanishKeys = LoadResourceKeys(SpanishResourcePath);
        var errorCodes = ExtractErrorCodes(applicationFiles);

        var missingInEnglish = errorCodes
            .Where(code => !englishKeys.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var missingInSpanish = errorCodes
            .Where(code => !spanishKeys.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        AssertNoMissing("Error codes missing in BackendMessages.resx", missingInEnglish);
        AssertNoMissing("Error codes missing in BackendMessages.es.resx", missingInSpanish);
    }

    [Fact]
    public void ResourceCatalog_ShouldContainAllValidationMessages_InEnglishAndSpanish()
    {
        var applicationFiles = GetApplicationFiles();
        var englishKeys = LoadResourceKeys(EnglishResourcePath);
        var spanishKeys = LoadResourceKeys(SpanishResourcePath);
        var validationKeys = ExtractValidationMessageKeys(applicationFiles);

        var missingInEnglish = validationKeys
            .Where(key => !englishKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var missingInSpanish = validationKeys
            .Where(key => !spanishKeys.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        AssertNoMissing("Validation keys missing in BackendMessages.resx", missingInEnglish);
        AssertNoMissing("Validation keys missing in BackendMessages.es.resx", missingInSpanish);
    }

    [Fact]
    public void InlineHandlerErrors_ShouldAlsoBeCoveredByResources()
    {
        var applicationFiles = GetApplicationFiles();
        var englishKeys = LoadResourceKeys(EnglishResourcePath);
        var spanishKeys = LoadResourceKeys(SpanishResourcePath);
        var inlineErrorCodes = ExtractInlineErrorCodes(applicationFiles);

        Assert.NotEmpty(inlineErrorCodes);

        var missingInEnglish = inlineErrorCodes
            .Where(code => !englishKeys.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var missingInSpanish = inlineErrorCodes
            .Where(code => !spanishKeys.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        AssertNoMissing("Inline error codes missing in BackendMessages.resx", missingInEnglish);
        AssertNoMissing("Inline error codes missing in BackendMessages.es.resx", missingInSpanish);
    }

    [Fact]
    public void EnglishAndSpanishResources_ShouldHaveTheSameKeys()
    {
        var englishKeys = LoadResourceKeys(EnglishResourcePath);
        var spanishKeys = LoadResourceKeys(SpanishResourcePath);

        var missingInSpanish = englishKeys
            .Except(spanishKeys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        var missingInEnglish = spanishKeys
            .Except(englishKeys, StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        AssertNoMissing("Keys missing in BackendMessages.es.resx", missingInSpanish);
        AssertNoMissing("Keys missing in BackendMessages.resx", missingInEnglish);
    }

    private static IReadOnlyCollection<string> GetApplicationFiles() =>
        Directory.GetFiles(ApplicationPath, "*.cs", SearchOption.AllDirectories);

    private static HashSet<string> LoadResourceKeys(string path)
    {
        var document = XDocument.Load(path);
        return document.Root?
            .Elements("data")
            .Select(static element => element.Attribute("name")?.Value)
            .OfType<string>()
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
    }

    private static HashSet<string> ExtractErrorCodes(IEnumerable<string> files)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in ErrorCodeRegex.Matches(content))
            {
                var code = match.Groups["code"].Value;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    result.Add(code);
                }
            }
        }

        return result;
    }

    private static HashSet<string> ExtractInlineErrorCodes(IEnumerable<string> files)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in InlineErrorCodeRegex.Matches(content))
            {
                var code = match.Groups["code"].Value;
                if (!string.IsNullOrWhiteSpace(code))
                {
                    result.Add(code);
                }
            }
        }

        return result;
    }

    private static HashSet<string> ExtractValidationMessageKeys(IEnumerable<string> files)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in ValidationWithMessageRegex.Matches(content))
            {
                result.Add(BuildValidationKey(match.Groups["message"].Value));
            }

            foreach (Match match in ValidationAddFailureRegex.Matches(content))
            {
                result.Add(BuildValidationKey(match.Groups["message"].Value));
            }
        }

        return result;
    }

    private static string BuildValidationKey(string message)
    {
        var normalized = ValidationKeyNormalizer
            .Replace(message.Trim().ToLowerInvariant(), "_")
            .Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "generic";
        }

        return $"validation.message.{normalized}";
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var hasSrc = Directory.Exists(Path.Combine(directory.FullName, "src"));
            var hasTests = Directory.Exists(Path.Combine(directory.FullName, "tests"));
            if (hasSrc && hasTests)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root for localization tests.");
    }

    private static void AssertNoMissing(string title, IReadOnlyCollection<string> missingItems)
    {
        Assert.True(
            missingItems.Count == 0,
            $"{title}:{Environment.NewLine}{string.Join(Environment.NewLine, missingItems)}");
    }
}
