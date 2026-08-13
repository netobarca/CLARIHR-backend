using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-27 — la cuenta bancaria no tenía ninguna guarda: ni contra duplicados exactos ni contra varias primarias.
/// Reproducido en vivo antes del arreglo: dos <c>POST</c> idénticos (mismo banco, misma moneda, mismo número, mismo
/// tipo) devolvían <c>201</c> los dos, y el empleado quedaba con tres cuentas, las tres marcadas como primaria.
/// <para>
/// Por qué importa: la primaria es la que decide dónde se deposita el sueldo cuando la plaza no designa una cuenta
/// explícita, y el único consumidor la elige con <c>FirstOrDefault(item => item.IsPrimary)</c> sobre una consulta
/// SIN <c>OrderBy</c> — o sea, el orden físico de las filas, que cambia solo.
/// </para>
/// <para>
/// El resto del sistema sí tenía unicidad (representantes legales, empresas del usuario, códigos de catálogo,
/// centros de costo, unidades organizativas). Las cuentas eran la excepción.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static int bankAccountTestDui;

    /// <summary>El DUI lleva formato `^\d{8}-\d$` (regex del catálogo), así que se compone, no se improvisa.</summary>
    private static string NextTestDui() =>
        $"{20000000 + Interlocked.Increment(ref bankAccountTestDui):D8}-1";

    private async Task<(Guid FileId, Guid BankId)> CreateFileWithBankAsync(
        HttpClient client, Guid companyId, string suffix, string bankCode = "BANCO_AGRICOLA")
    {
        var file = await CreatePersonnelFileAsync(client, companyId, "Cuenta", suffix, "DUI", NextTestDui());

        var banks = await ReadJsonArrayAsync(client, "/api/v1/general-catalogs/banks?countryCode=SV");
        var bank = Assert.Single(banks, item => item.GetProperty("code").GetString() == bankCode);
        return (file.Id, bank.GetProperty("publicId").GetGuid());
    }

    private static object BankAccountBody(Guid bankId, string accountNumber, bool isPrimary, string currencyCode = "USD") =>
        new
        {
            bankPublicId = bankId,
            currencyCode,
            accountNumber,
            accountTypeCode = "AHORRO",
            isPrimary,
        };

    /// <summary>El caso del hallazgo: el segundo <c>POST</c> idéntico se rechaza en vez de crear una copia.</summary>
    [Fact]
    public async Task BankAccounts_ExactDuplicate_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var (fileId, bankId) = await CreateFileWithBankAsync(client, scenario.TenantId, "Dup");

        var first = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "0001-1111-2222", isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "0001-1111-2222", isPrimary: true));

        await AssertProblemDetailsAsync(second, HttpStatusCode.UnprocessableEntity, "PERSONNEL_FILE_BANK_ACCOUNT_DUPLICATE");
        Assert.Single(await ReadBankAccountsAsync(client, fileId));
    }

    /// <summary>
    /// El duplicado se juzga por el número NORMALIZADO: los separadores no hacen una cuenta distinta. Sin esto, el
    /// mismo número con guiones y sin guiones convive, que es el duplicado disfrazado.
    /// </summary>
    [Fact]
    public async Task BankAccounts_DuplicateWithDifferentSeparators_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var (fileId, bankId) = await CreateFileWithBankAsync(client, scenario.TenantId, "Sep");

        var first = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "0001-1111-2222", isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "000111112222", isPrimary: false));

        await AssertProblemDetailsAsync(second, HttpStatusCode.UnprocessableEntity, "PERSONNEL_FILE_BANK_ACCOUNT_DUPLICATE");
    }

    /// <summary>
    /// El límite deliberado: la MISMA cuenta en otra MONEDA sí es un caso real (una cuenta en dólares y otra en
    /// colones en el mismo banco), así que la moneda entra en la clave del duplicado. Este test impide que alguien
    /// "endurezca" la regla y rompa el caso legítimo.
    /// </summary>
    [Fact]
    public async Task BankAccounts_SameNumberInAnotherCurrency_IsAllowed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var (fileId, bankId) = await CreateFileWithBankAsync(client, scenario.TenantId, "Mon");

        var usd = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "0001-1111-2222", isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, usd.StatusCode);

        var eur = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts",
            BankAccountBody(bankId, "0001-1111-2222", isPrimary: false, currencyCode: "EUR"));
        Assert.Equal(HttpStatusCode.Created, eur.StatusCode);

        Assert.Equal(2, (await ReadBankAccountsAsync(client, fileId)).Length);
    }

    /// <summary>
    /// Marcar una cuenta como primaria DESMARCA la anterior, en el mismo commit. Es el patrón de la casa
    /// (representantes legales, asignaciones de plaza) y es lo que el usuario espera: no querés un error por
    /// marcar tu cuenta nueva como principal.
    /// </summary>
    [Fact]
    public async Task BankAccounts_MarkingASecondPrimary_DemotesThePrevious()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var (fileId, bankId) = await CreateFileWithBankAsync(client, scenario.TenantId, "Pri");

        var first = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "1111-0000-0001", isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "2222-0000-0002", isPrimary: true));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var accounts = await ReadBankAccountsAsync(client, fileId);
        Assert.Equal(2, accounts.Length);
        var primary = Assert.Single(accounts, item => item.GetProperty("isPrimary").GetBoolean());
        Assert.Equal("2222-0000-0002", primary.GetProperty("accountNumber").GetString());
    }

    /// <summary>
    /// La PRIMERA cuenta es primaria por definición, aunque el cuerpo diga <c>false</c>: si no, el empleado queda
    /// con cuentas y ninguna principal, y el consumidor cae en un <c>FirstOrDefault()</c> sin criterio.
    /// </summary>
    [Fact]
    public async Task BankAccounts_FirstAccount_IsPrimaryEvenWhenNotRequested()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var (fileId, bankId) = await CreateFileWithBankAsync(client, scenario.TenantId, "Fst");

        var only = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "3333-0000-0003", isPrimary: false));
        Assert.Equal(HttpStatusCode.Created, only.StatusCode);

        var account = Assert.Single(await ReadBankAccountsAsync(client, fileId));
        Assert.True(account.GetProperty("isPrimary").GetBoolean());
    }

    /// <summary>
    /// El <c>PUT</c> también cambia la primaria, así que también tiene que degradar. El precedente de
    /// representantes legales ya aprendió a mantener los TRES caminos idénticos.
    /// </summary>
    [Fact]
    public async Task BankAccounts_PutMarkingPrimary_DemotesThePrevious()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var (fileId, bankId) = await CreateFileWithBankAsync(client, scenario.TenantId, "Put");

        _ = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "4444-0000-0004", isPrimary: true));
        var secondResponse = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{fileId}/bank-accounts", BankAccountBody(bankId, "5555-0000-0005", isPrimary: false));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        using var created = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        var accountId = created.RootElement.GetProperty("bankAccountPublicId").GetGuid();
        var token = created.RootElement.GetProperty("concurrencyToken").GetGuid();

        var put = await SendSettlementAsync(
            client, HttpMethod.Put, $"/api/v1/personnel-files/{fileId}/bank-accounts/{accountId}",
            token, BankAccountBody(bankId, "5555-0000-0005", isPrimary: true));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var accounts = await ReadBankAccountsAsync(client, fileId);
        var primary = Assert.Single(accounts, item => item.GetProperty("isPrimary").GetBoolean());
        Assert.Equal("5555-0000-0005", primary.GetProperty("accountNumber").GetString());
    }

    /// <summary>
    /// H-27 (decisión 1) — las identificaciones tienen el mismo hueco de primaria única. Sus duplicados YA estaban
    /// bloqueados por <c>uq_personnel_file_identifications__tenant_type_number</c> (y por tenant, no por
    /// expediente: el mismo DUI no se registra dos veces en la empresa), así que acá solo se cierra la primaria.
    /// Hoy nadie la lee, pero el hueco es el mismo y el arreglo es el mismo índice.
    /// </summary>
    [Fact]
    public async Task Identifications_MarkingASecondPrimary_DemotesThePrevious()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));
        var file = await CreatePersonnelFileAsync(client, scenario.TenantId, "Ident", "Primaria", "DUI", NextTestDui());

        var second = await client.PostJsonAsync(
            $"/api/v1/personnel-files/{file.Id}/identifications",
            new { identificationTypeCode = "NIT", identificationNumber = "0614-010190-101-2", isPrimary = true });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var identifications = await ReadJsonArrayAsync(client, $"/api/v1/personnel-files/{file.Id}/identifications");
        Assert.Equal(2, identifications.Length);
        var primary = Assert.Single(identifications, item => item.GetProperty("isPrimary").GetBoolean());
        Assert.Equal("NIT", primary.GetProperty("identificationTypeCode").GetString());
    }

    private static async Task<JsonElement[]> ReadBankAccountsAsync(HttpClient client, Guid fileId) =>
        await ReadJsonArrayAsync(client, $"/api/v1/personnel-files/{fileId}/bank-accounts");

    private static async Task<JsonElement[]> ReadJsonArrayAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"{url}: {(int)response.StatusCode} {payload}");
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.EnumerateArray().Select(item => item.Clone()).ToArray();
    }
}
