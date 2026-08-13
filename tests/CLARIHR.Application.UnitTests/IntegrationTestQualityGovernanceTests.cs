using System.Text.RegularExpressions;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// H-33 guardrails. Three 🔴/🟠 findings of the sections 3–4 run survived a green suite because the
/// integration coverage <b>exercised the endpoint without ever testing the condition that mattered</b>. The
/// extreme case: a permanent <c>500</c> on <c>POST /api/v1/company/users</c> lived for weeks behind a test
/// that hit that endpoint <b>eleven times per run</b>.
/// <para>
/// These pin only the mechanical smells. The other three failure modes of H-33 — a fixture that makes the
/// condition under test constant, an assert on the current instead of the correct behaviour, and a middleware
/// layer that hides the one you meant to test — are indistinguishable from a correct test to any static
/// analysis. They are covered by the four questions in <c>AGENTS.md</c> §5 Paso 5, deliberately as practice
/// and not as a promise this file cannot keep.
/// </para>
/// </summary>
public sealed partial class IntegrationTestQualityGovernanceTests
{
    // Non-interpolated on purpose: the sample contains the braces of a real interpolated string.
    private const string LoopFixHint = """
        Add, inside the loop and right after the break:
            Assert.True((int)response.StatusCode < 500,
                $"call {index} returned {(int)response.StatusCode} before the limit engaged.");

        Not "IsSuccessStatusCode": some loops send invalid requests on purpose, so a 4xx can be correct
        there. A 5xx never is.
        """;

    /// <summary>
    /// G1 — a loop that breaks on a terminal status must assert something about the responses it discards.
    /// <para>
    /// <c>CompanyUsers_Invite_ShouldRateLimit</c> issued 11 requests, broke on the first <c>429</c> and
    /// asserted only that. The ten before it returned <c>500</c> and nobody looked. The invariant is
    /// <b>not 5xx</b> rather than "succeeded" on purpose: some of these loops send deliberately invalid
    /// requests — <c>PersonnelFiles_Lifecycle_ShouldRateLimit</c> uses a stale <c>If-Match</c>, so its
    /// pre-limit responses are legitimate <c>409</c>s. A 4xx can be correct; a 5xx never is.
    /// </para>
    /// </summary>
    [Fact]
    public void LoopsThatBreakOnATerminalStatus_MustAssertTheResponsesTheyDiscard()
    {
        var offenders = new List<string>();

        foreach (var (file, source) in IntegrationTestSources())
        {
            foreach (Match loop in LoopBodyRegex().Matches(source))
            {
                var body = loop.Groups["body"].Value;

                var breakMatch = BreakOnStatusRegex().Match(body);
                if (!breakMatch.Success)
                {
                    continue;
                }

                var variable = breakMatch.Groups["var"].Value;

                // Something must be asserted about that same response inside the loop.
                var asserted = Regex.IsMatch(
                    body,
                    $@"Assert\w*\.[\w<>]+\([^;]*\b{Regex.Escape(variable)}\b|await\s+AssertProblemDetailsAsync\([^;]*\b{Regex.Escape(variable)}\b");

                if (!asserted)
                {
                    offenders.Add($"{file} · loop breaking on `{variable}.StatusCode` asserts nothing about `{variable}`");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"""
            {offenders.Count} loop(s) discard every response before the terminal status without checking any
            of them. That is how a permanent 500 stayed green for weeks (H-33).

            {LoopFixHint}

            {string.Join("\n", offenders)}
            """);
    }

    /// <summary>
    /// G2 — no endpoint may have a rate-limit loop as its ONLY coverage. That was literally true of
    /// <c>POST /api/v1/company/users</c>: its only test looped to <c>429</c>, so the endpoint was called
    /// constantly and verified never.
    /// </summary>
    [Fact]
    public void NoEndpoint_MayHaveARateLimitLoopAsItsOnlyCoverage()
    {
        var rateLimitOnly = new HashSet<string>(StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (_, source) in IntegrationTestSources())
        {
            // Everything that is NOT the body of a rate-limit test counts as coverage — including private
            // helpers. Scanning only `public async Task` bodies reported
            // `/api/v1/companies/{id}/location-groups/tree` as blind when it is reached through
            // `GetLocationGroupTreeAsync`, a helper that does call EnsureSuccessStatusCode(). Being
            // permissive here is deliberate: a guardrail with false positives gets switched off.
            var remainder = source;

            foreach (var (name, body) in TestMethods(source))
            {
                var isRateLimitTest = body.Contains("TooManyRequests", StringComparison.Ordinal) &&
                                      name.Contains("RateLimit", StringComparison.OrdinalIgnoreCase);
                if (!isRateLimitTest)
                {
                    continue;
                }

                foreach (var endpoint in EndpointsCalledIn(body))
                {
                    rateLimitOnly.Add(endpoint);
                }

                remainder = remainder.Replace(body, string.Empty, StringComparison.Ordinal);
            }

            foreach (var endpoint in EndpointsCalledIn(remainder))
            {
                covered.Add(endpoint);
            }
        }

        var blind = rateLimitOnly.Except(covered, StringComparer.Ordinal)
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            blind.Length == 0,
            $"""
            {blind.Length} endpoint(s) are only ever reached by a rate-limit loop, so nothing verifies they
            work (H-33). Add a dedicated test that asserts the real behaviour — and if it returns 201, follow
            the Location header: a bare status assertion would not have caught the H-02 defect, which was in
            the URL and not in the status.

            {string.Join("\n", blind)}
            """);
    }

    /// <summary>
    /// G3 — the one mechanical smell that does exist: an HTTP call whose response is thrown away. The
    /// request may have failed and the test proceeds as if it had not.
    /// </summary>
    [Fact]
    public void NoIntegrationTest_MayDiscardAnHttpResponse()
    {
        var offenders = new List<string>();

        foreach (var (file, source) in IntegrationTestSources())
        {
            foreach (var (name, body) in TestMethods(source))
            {
                foreach (Match call in DiscardedCallRegex().Matches(body))
                {
                    offenders.Add($"{file} · {name}() · discards the response of {call.Groups["call"].Value}(…)");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"""
            {offenders.Count} HTTP call(s) have their response discarded entirely. Assign it and assert, or
            call EnsureSuccessStatusCode() — otherwise a failed request looks identical to a successful one
            and the rest of the test runs on a false premise.

            {string.Join("\n", offenders)}
            """);
    }

    /// <summary>
    /// <c>VERB route</c> pairs actually invoked in <paramref name="code"/>. The verb is essential and its
    /// absence is what made the first version of G2 useless: with the query string stripped,
    /// <c>GET /api/v1/company/users</c> (the list test) and <c>POST /api/v1/company/users</c> (the invite)
    /// collapse to the same key, so the list test "covered" the invite and H-02 stayed invisible to the very
    /// guardrail written to catch it. Verified by deleting the happy-path test and watching G2 stay green.
    /// </summary>
    private static IEnumerable<string> EndpointsCalledIn(string code)
    {
        foreach (Match call in TypedHttpCallRegex().Matches(code))
        {
            yield return $"{call.Groups["verb"].Value.ToUpperInvariant()} {Normalize(call.Groups["route"].Value)}";
        }

        // `new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/…")` — the shape used whenever a header
        // (If-Match) has to be set by hand.
        foreach (Match call in HttpRequestMessageRegex().Matches(code))
        {
            yield return $"{call.Groups["verb"].Value.ToUpperInvariant()} {Normalize(call.Groups["route"].Value)}";
        }
    }

    /// <summary>
    /// G4 — a method that looks like a test must actually be one. A <c>public async Task</c> that loses its
    /// <c>[Fact]</c> disappears from the suite in complete silence: it still compiles, nothing references it,
    /// and no count betrays it because nobody knows how many tests there <i>should</i> be.
    /// <para>
    /// The signal is the naming convention, and it is unambiguous here: all 757 attributed tests carry an
    /// underscore, while every legitimately public non-test method (<c>InitializeAsync</c>,
    /// <c>UploadStreamAsync</c>, factory helpers, interface members) does not. Writing this test found one real
    /// orphan that had been silently absent from the suite.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryMethodNamedLikeATest_MustCarryFactOrTheory()
    {
        var orphans = new List<string>();

        foreach (var (file, source) in TestProjectSources())
        {
            foreach (Match method in PublicAsyncTaskRegex().Matches(source))
            {
                var name = method.Groups["name"].Value;
                if (!name.Contains('_', StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Regex.IsMatch(method.Groups["attrs"].Value, @"\[(Fact|Theory)"))
                {
                    orphans.Add($"{file} · {name}");
                }
            }
        }

        Assert.True(
            orphans.Count == 0,
            $"""
            {orphans.Count} method(s) are named like tests but carry no [Fact]/[Theory], so they never run and
            nothing reports them missing.

            Add the attribute — and then actually run it: a test that has been absent for a while may well be
            red, which is precisely the coverage it was supposed to be providing.

            {string.Join("\n", orphans)}
            """);
    }

    /// <summary>
    /// G6 — un <c>(ruta, verbo)</c> no puede tener como única cobertura aserciones de ERROR.
    /// <para>
    /// Es el mecanismo 2 de H-33 —«una capa antes de la que quiero probar»— automatizado. El caso que lo motiva:
    /// el <c>POST /api/v1/company/users</c> del invite estuvo devolviendo <c>500</c> durante semanas con un test
    /// que lo golpeaba once veces por corrida, porque ese test solo afirmaba el <c>429</c> del rate limiter.
    /// Llegar a la capa N no prueba nada de la capa N−1, y un endpoint cuya única cobertura es «me rechaza» nunca
    /// demuestra que funcione cuando debe funcionar.
    /// </para>
    /// <para>
    /// G2 ya cubre el caso particular del loop de rate limit; esto generaliza a cualquier endpoint que solo tenga
    /// tests de <c>401/403/404/409/422</c>. La lista de excepciones es explícita a propósito: hay endpoints cuyo
    /// único camino comprobable ES el de error (los de solo-anónimo, por ejemplo), y esos se nombran uno por uno en
    /// vez de debilitar la regla.
    /// </para>
    /// </summary>
    [Fact]
    public void NoEndpoint_MayHaveOnlyErrorAssertionsAsItsCoverage()
    {
        var withSuccess = new HashSet<string>(StringComparer.Ordinal);
        var withAnyCoverage = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (_, source) in IntegrationTestSources())
        {
            // Mismo truco del «remanente» que G2, y por la misma razón medida: la aserción de éxito suele vivir en
            // un HELPER privado, no en el cuerpo del test. Escaneando solo los cuerpos, este guardrail acusaba 26
            // endpoints y la mayoría eran falsos positivos — `GET /companies/{id}/job-profiles`, por ejemplo, cuyo
            // éxito lo afirma `CreateJobProfileAsync`. Se quitan del texto solo los tests SIN señal de éxito, y
            // todo lo que queda (los demás tests y todos los helpers) cuenta como cobertura en verde.
            var remainder = source;

            foreach (var (_, body) in TestMethods(source))
            {
                var endpoints = EndpointsCalledIn(body).ToArray();
                if (endpoints.Length == 0)
                {
                    continue;
                }

                foreach (var endpoint in endpoints)
                {
                    withAnyCoverage.Add(endpoint);
                }

                if (!SuccessExpectationRegex().IsMatch(body))
                {
                    remainder = remainder.Replace(body, string.Empty, StringComparison.Ordinal);
                }
            }

            // Del lado del ÉXITO se usa una extracción más ancha, que además reconoce los envoltorios genéricos:
            // `PostLeaveMasterAsync(client, $"/api/v1/...", …)` lleva el verbo en su propio nombre y su
            // `EnsureSuccessStatusCode` vive dentro del envoltorio, lejos del literal. Sin esto G6 acusaba
            // `POST /companies/{id}/medical-clinics` teniendo cobertura verde de sobra.
            // El verbo SE MANTIENE en la clave: quitarlo fue el defecto que G2 ya cometió y corrigió — el listado
            // `GET /company/users` habría «cubierto» el `POST` del invite, que es el caso que este guardrail existe
            // para cazar.
            // UNIÓN de los dos extractores, no reemplazo: el estrecho entiende `new HttpRequestMessage(HttpMethod.X,
            // "ruta")` —como el `SendSettlementAsync` que usan casi todos los PATCH— y el ancho entiende los
            // envoltorios con el verbo en el nombre. Usar solo el ancho hizo saltar el reporte de 16 a 73, porque
            // los PATCH dejaban de tener atribución de éxito.
            foreach (var endpoint in EndpointsCalledIn(remainder).Concat(SuccessEndpointsIn(remainder)))
            {
                withSuccess.Add(endpoint);
            }
        }

        var errorOnly = withAnyCoverage.Except(withSuccess, StringComparer.Ordinal)
            .Except(ErrorOnlyByDesign, StringComparer.Ordinal)
            .OrderBy(endpoint => endpoint, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            errorOnly.Length == 0,
            $"""
            {errorOnly.Length} endpoint(s) are only ever exercised with error expectations, so nothing shows they
            work when they should. That is how a permanent 500 on POST /api/v1/company/users survived weeks of
            coverage that hit it eleven times per run.

            Add a test that asserts the SUCCESS path — or, if the endpoint genuinely has no testable success path
            from integration (anonymous-only surfaces, for instance), name it in ErrorOnlyByDesign with the reason.

            {string.Join("\n", errorOnly.Select(endpoint => "  " + endpoint))}
            """);
    }

    /// <summary>
    /// Endpoints que G6 no acusa. Se nombran uno por uno, con su razón, en vez de debilitar la regla — y se
    /// distingue lo que es DISEÑO de lo que es DEUDA, porque no es lo mismo.
    /// </summary>
    private static readonly HashSet<string> ErrorOnlyByDesign = new(StringComparer.Ordinal)
    {
        // ── Por DISEÑO: el endpoint no existe y el test afirma justamente eso ────────────────────
        // H-23 eliminó el PATCH de ocupación (la ocupación se deriva de las asignaciones activas); su test
        // comprueba que la superficie está ida, así que su única aserción posible es de error.
        "PATCH /api/v1/position-slots/{id}/occupancy",

        // ── DEUDA heredada, no diseño ────────────────────────────────────────────────────────────
        // Estos once se ejercitan hoy SOLO con expectativas de error. No es correcto y no se justifica: es la
        // línea base con la que G6 entra en vigor, para que ninguno NUEVO se sume. Cada uno necesita un test
        // que afirme su camino de éxito, y hasta entonces «la suite está verde» no dice nada sobre ellos.
        "GET /api/v1/companies/{id}/job-profiles",
        "GET /api/v1/personnel-files/{id}/position-hierarchy",
        "GET /api/v1/personnel-files/{id}/print",
        "PATCH /api/v1/company/users/{id}/deactivate",
        "PATCH /api/v1/job-profiles/{id}",
        "PATCH /api/v1/personnel-file-documents/{id}/file",
        "PATCH /api/v1/personnel-files/documents/{id}/file",
        "POST /api/v1/companies/{id}/payroll-periods",
        "POST /api/v1/job-profiles/internal-catalogs/{id}/values",
        "POST /api/v1/personnel-files/{id}/compensation-concepts",
        "POST /api/v1/personnel-files/{id}/evaluations",
    };

    /// <summary>
    /// G5 — un helper de siembra compartido no puede alimentar <b>dos campos de fecha distintos</b> con la misma
    /// constante, salvo que exponga un parámetro para desacoplarlos.
    /// <para>
    /// Es el mecanismo 3 de H-33 —«el fixture vuelve constante la condición bajo prueba»— acotado a la única
    /// familia con firma mecánica: las fechas. Y es la familia que importa, porque es la que escondió
    /// <b>H-28</b>: `SeedSettlementCandidateAsync` sembraba la fecha de INGRESO, la del CONTRATO, el inicio de la
    /// PLAZA y la vigencia del SALARIO con el mismo valor, así que ningún test podía distinguir los dos anclajes
    /// de antigüedad — y el defecto era exactamente cuál de los dos se usa. La suite seguía verde.
    /// </para>
    /// <para>
    /// El desacople es <b>opt-in</b>: el parámetro nuevo lleva por defecto el valor acoplado, así que ningún test
    /// existente cambia de comportamiento y quien necesite separarlos lo pide. Por eso basta con exigir que el
    /// helper <i>pueda</i> desacoplarlos, no que lo haga.
    /// </para>
    /// <para>
    /// Deliberadamente limitado a fechas: abrirlo a cualquier constante repetida (montos, códigos de empleado)
    /// trae ruido legítimo que habría que excepcionar una por una, y un guardrail con lista larga de excepciones
    /// deja de leerse. Los otros mecanismos siguen cubiertos por las preguntas de <c>AGENTS.md</c> §5 Paso 5.
    /// </para>
    /// </summary>
    [Fact]
    public void SeedHelpers_MustNotFeedTwoDateFieldsFromOneConstant()
    {
        var offenders = new List<string>();

        // Las constantes se recogen de TODA la suite antes de escanear: la clase de tests es `partial`, así que un
        // helper de un archivo usa constantes declaradas en otro. Mirando solo el archivo propio, este guardrail se
        // perdía los tres helpers de finiquito, que toman `RetirementHireDate` de `ApiIntegrationTests.Retirement.cs`.
        var files = IntegrationTestSources().ToArray();
        var dateConstants = files
            .SelectMany(item => DateConstantRegex().Matches(item.Source).Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (file, source) in files)
        {
            foreach (Match helper in SeedHelperRegex().Matches(source))
            {
                var name = helper.Groups["name"].Value;
                var body = helper.Value;

                // El helper que ya expone cómo desacoplar está cumpliendo: separar es opt-in.
                var exposesAnOverride = DateParameterRegex().IsMatch(helper.Groups["parameters"].Value);

                foreach (var constant in dateConstants)
                {
                    // Solo cuentan los usos INDEPENDIENTES. `X.AddDays(4)` es la misma fecha corrida —el fin del
                    // mismo permiso, por ejemplo—, no un segundo campo que una regla pueda distinguir; contarlo
                    // daba un falso positivo en `SeedNotWorkedTimeRecordAsync`, que usa la constante para el inicio
                    // y su `AddDays` para el fin. Una CONVERSIÓN de tipo (`DateOnly.FromDateTime(X)`) sí cuenta: es
                    // el mismo instante alimentando otro campo, que es justo el acoplamiento de H-28.
                    var uses = Regex.Matches(body, $@"\b{Regex.Escape(constant)}\b(?!\s*\.Add)").Count;
                    if (uses < 2 || exposesAnOverride)
                    {
                        continue;
                    }

                    offenders.Add($"  {file}: {name} feeds {constant} into {uses} fields with no way to decouple them.");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"""
            {offenders.Count} shared seed helper(s) feed one date constant into two or more different fields and
            offer no parameter to separate them, so no test built on them can distinguish those dates.

            This is how H-28 stayed invisible: hire date, contract date, plaza start and salary validity were all
            the same value, and the defect was precisely which of them the seniority is measured from.

            The fix is opt-in and breaks nothing — add optional parameters that DEFAULT to the coupled value, the
            way SeedSettlementCandidateAsync does:

                DateOnly? plazaStartDate = null,
                DateTime? hireDate = null

            …and derive the rest from them. Existing call sites keep their current behaviour.

            {string.Join("\n", offenders)}
            """);
    }

    /// <summary>Route literal with its parameters flattened, so `{a}` and `{b}` compare equal.</summary>
    private static string Normalize(string route) =>
        RouteParameterRegex().Replace(route.Split('?')[0], "{id}").TrimEnd('/');

    private static IEnumerable<(string File, string Source)> IntegrationTestSources() =>
        SourcesUnder("CLARIHR.Api.IntegrationTests");

    /// <summary>G4 scans both suites: an orphaned unit test is just as invisible as an orphaned integration one.</summary>
    private static IEnumerable<(string File, string Source)> TestProjectSources() =>
        SourcesUnder("CLARIHR.Api.IntegrationTests").Concat(SourcesUnder("CLARIHR.Application.UnitTests"));

    private static IEnumerable<(string File, string Source)> SourcesUnder(string testProject)
    {
        var directory = Path.Combine(FindRepositoryRoot(), "tests", testProject);

        foreach (var path in Directory.EnumerateFiles(directory, "*.cs").OrderBy(item => item, StringComparer.Ordinal))
        {
            yield return (Path.GetFileName(path), File.ReadAllText(path));
        }
    }

    /// <summary>Test method bodies, cut at the closing brace of the method (4-space indentation).</summary>
    private static IEnumerable<(string Name, string Body)> TestMethods(string source)
    {
        foreach (Match match in TestMethodRegex().Matches(source))
        {
            var rest = source.AsSpan(match.Index + match.Length);
            var end = rest.IndexOf("\n    }", StringComparison.Ordinal);
            yield return (match.Groups["name"].Value, (end >= 0 ? rest[..end] : rest).ToString());
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CLARIHR.Application")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output path.");
    }

    [GeneratedRegex(@"public\s+async\s+Task\s+(?<name>\w+)\s*\([^)]*\)\s*\{", RegexOptions.Compiled)]
    private static partial Regex TestMethodRegex();

    // Captures the attribute block preceding the declaration so G4 can tell an attributed test from an orphan.
    // Line comments must be allowed INSIDE the block: `[Theory]` followed by `[InlineData(x)] // why` is
    // common here, and a whitespace-only separator stopped at the comment and reported three attributed
    // [Theory] tests as orphans.
    [GeneratedRegex(@"(?<attrs>(?:(?:\[[^\]]*\]|//[^\n]*)\s*)*)public\s+async\s+Task(?:<[^>]*>)?\s+(?<name>\w+)\s*\(", RegexOptions.Compiled)]
    private static partial Regex PublicAsyncTaskRegex();

    // A for/while whose body is delimited by the 8-space closing brace of a method-level loop.
    [GeneratedRegex(@"\n        (?:for|while)\s*\([^)]*\)\n        \{\n(?<body>[\s\S]*?)\n        \}", RegexOptions.Compiled)]
    private static partial Regex LoopBodyRegex();

    [GeneratedRegex(@"if\s*\(\s*(?<var>\w+)(?:!)?\.StatusCode\s*==\s*HttpStatusCode\.\w+\s*\)\s*\n?\s*\{?\s*\n?\s*break;", RegexOptions.Compiled)]
    private static partial Regex BreakOnStatusRegex();

    // `await client.PostJsonAsync(...)` as a bare statement: nothing captures the result.
    [GeneratedRegex(@"\n\s*await\s+\w+\.(?<call>(?:Post|Put|Patch|Delete|Get)(?:Json)?Async)\s*\(", RegexOptions.Compiled)]
    private static partial Regex DiscardedCallRegex();

    // client.PostJsonAsync($"/api/v1/…"  ·  client.GetAsync("/api/v1/…"  ·  client.DeleteAsync(…
    [GeneratedRegex(@"\.(?<verb>Post|Put|Patch|Delete|Get)(?:Json)?Async\s*\(\s*\$?""(?<route>/api/v\d+/[^""]*)""", RegexOptions.Compiled)]
    private static partial Regex TypedHttpCallRegex();

    [GeneratedRegex(@"new\s+HttpRequestMessage\s*\(\s*HttpMethod\.(?<verb>\w+)\s*,\s*\$?""(?<route>/api/v\d+/[^""]*)""", RegexOptions.Compiled)]
    private static partial Regex HttpRequestMessageRegex();

    /// <summary>
    /// Rutas alcanzadas desde código en verde, incluyendo los envoltorios genéricos cuyo nombre empieza por el
    /// verbo (<c>PostLeaveMasterAsync</c>, <c>GetPagedAsync</c>…). El verbo se conserva.
    /// </summary>
    private static IEnumerable<string> SuccessEndpointsIn(string source)
    {
        foreach (Match match in VerbNamedCallRegex().Matches(source))
        {
            yield return $"{match.Groups["verb"].Value.ToUpperInvariant()} {Normalize(match.Groups["route"].Value)}";
        }
    }

    /// <summary>Cualquier señal de que el cuerpo espera un 2xx en alguna de sus llamadas.</summary>
    [GeneratedRegex(@"HttpStatusCode\.(OK|Created|NoContent|Accepted)|EnsureSuccessStatusCode|IsSuccessStatusCode", RegexOptions.Compiled)]
    private static partial Regex SuccessExpectationRegex();

    /// <summary>Cualquier llamada cuyo MÉTODO empieza por un verbo HTTP y recibe un literal de ruta.</summary>
    [GeneratedRegex(@"\b(?<verb>Post|Put|Patch|Delete|Get)\w*Async(?:<[^>()]*>)?\s*\([^;]{0,200}?\$?""(?<route>/api/v\d+/[^""]*)""", RegexOptions.Compiled)]
    private static partial Regex VerbNamedCallRegex();

    /// <summary>Constantes de fecha del archivo: `private static readonly DateTime/DateOnly Xxx = …`.</summary>
    [GeneratedRegex(@"private\s+static\s+readonly\s+(?:DateTime|DateOnly)\??\s+(?<name>\w+)\s*=", RegexOptions.Compiled)]
    private static partial Regex DateConstantRegex();

    /// <summary>Helper de siembra compartido, con su lista de parámetros y su cuerpo.</summary>
    [GeneratedRegex(@"private\s+(?:static\s+)?async\s+Task<[^>]*>\s+(?<name>Seed\w+|Create\w+)\s*\((?<parameters>[^)]*)\).*?\n    \}", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex SeedHelperRegex();

    /// <summary>¿La firma ya deja pasar una fecha propia? Entonces el desacople está disponible.</summary>
    [GeneratedRegex(@"(?:DateTime|DateOnly)\?\s+\w+\s*=\s*null", RegexOptions.Compiled)]
    private static partial Regex DateParameterRegex();

    [GeneratedRegex(@"\{[^}]*\}", RegexOptions.Compiled)]
    private static partial Regex RouteParameterRegex();
}
