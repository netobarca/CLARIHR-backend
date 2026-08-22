using System.Globalization;
using CLARIHR.Application.Common.Validation;
using CLARIHR.Infrastructure.Localization;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// 00002 / B-04 — <b>las etiquetas de negocio dentro de los mensajes de validación.</b>
/// <para>
/// El punto del diseño es que sea <b>inerte sin etiqueta</b>: un campo no catalogado tiene que seguir
/// comportándose exactamente como antes. Sin esa garantía, poblar el catálogo de forma incremental
/// dejaría el producto a medio traducir en vez de mejorarlo poco a poco.
/// </para>
/// </summary>
public sealed class ValidationDisplayNameTests
{
    [Fact]
    public void ResolvePropertyName_WhenSpanishAndLabelled_ShouldReturnTheBusinessLabel()
    {
        ConCultura("es", () =>
        {
            Assert.Equal("Orden", ResourceBackendMessageLocalizer.ResolvePropertyName("SortOrder"));
            Assert.Equal("Código", ResourceBackendMessageLocalizer.ResolvePropertyName("Code"));
            Assert.Equal("Nombre", ResourceBackendMessageLocalizer.ResolvePropertyName("Name"));
        });
    }

    /// <summary>
    /// El contrapeso que sostiene todo lo demás: sin etiqueta no hay cambio. Si esto se rompiera, el
    /// mecanismo dejaría de ser incremental y pasaría a ser una migración a medias.
    /// </summary>
    [Fact]
    public void ResolvePropertyName_WhenNotLabelled_ShouldReturnNull()
    {
        ConCultura("es", () =>
        {
            Assert.Null(ResourceBackendMessageLocalizer.ResolvePropertyName("ConcurrencyToken"));
            Assert.Null(ResourceBackendMessageLocalizer.ResolvePropertyName("PersonnelFileId"));
        });
    }

    /// <summary>
    /// La salida en inglés no puede cambiar. La paridad de claves obliga a tener la etiqueta también en
    /// el recurso neutro, así que su valor tiene que ser <b>exactamente</b> el nombre partido que
    /// FluentValidation produce por su cuenta. Un descuido aquí traduciría el inglés sin querer.
    /// </summary>
    [Theory]
    [InlineData("SortOrder", "Sort Order")]
    [InlineData("Code", "Code")]
    [InlineData("CountryCode", "Country Code")]
    [InlineData("PayrollPeriodLabel", "Payroll Period Label")]
    public void ResolvePropertyName_WhenEnglish_ShouldMatchFluentValidationsOwnSplit(string propiedad, string esperado)
    {
        ConCultura("en", () =>
            Assert.Equal(esperado, ResourceBackendMessageLocalizer.ResolvePropertyName(propiedad)));
    }

    /// <summary>
    /// El mismo contrato, para TODAS las etiquetas inglesas del catálogo a la vez: cada valor debe ser
    /// el PascalCase partido de su clave. Cubre las que se añadan mañana sin tocar esta prueba.
    /// </summary>
    [Fact]
    public void EnglishLabels_ShouldAllBeTheDefaultSplit()
    {
        var ruta = Path.Combine(RaizDelRepositorio(), "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx");
        var etiquetas = System.Xml.Linq.XDocument.Load(ruta).Root!
            .Elements("data")
            .Where(d => d.Attribute("name")!.Value.StartsWith("validation.property.", StringComparison.Ordinal))
            .ToDictionary(
                d => d.Attribute("name")!.Value["validation.property.".Length..],
                d => d.Element("value")!.Value,
                StringComparer.Ordinal);

        Assert.NotEmpty(etiquetas);

        var ofensores = etiquetas
            .Where(par => !string.Equals(
                par.Value.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant(),
                par.Key,
                StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            ofensores.Length == 0,
            "Etiquetas inglesas que NO son el nombre partido de su propiedad (cambiarían la salida en inglés):\n" +
            string.Join('\n', ofensores.Select(p => $"  validation.property.{p.Key} = «{p.Value}»")));
    }

    private static string RaizDelRepositorio()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "CLARIHR.slnx")))
        {
            d = d.Parent;
        }

        return d?.FullName ?? throw new InvalidOperationException("No se encontró CLARIHR.slnx.");
    }

    [Fact]
    public void ResolveDisplayName_WithoutAnInstalledResolver_ShouldReturnNull()
    {
        ValidationDisplayNames.ClearResolver();
        try
        {
            Assert.Null(ValidationDisplayNames.ResolveDisplayName(
                typeof(ValidationDisplayNameTests),
                typeof(ValidationDisplayNameTests).GetMethod(nameof(ResolveDisplayName_WithoutAnInstalledResolver_ShouldReturnNull)),
                null));
        }
        finally
        {
            ValidationDisplayNames.UseResolver(ResourceBackendMessageLocalizer.ResolvePropertyName);
        }
    }

    private static void ConCultura(string cultura, Action accion)
    {
        var original = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultura);
        try
        {
            accion();
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
