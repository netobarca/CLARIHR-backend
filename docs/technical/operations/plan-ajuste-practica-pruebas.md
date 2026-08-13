# Plan de ajuste — práctica de pruebas (H-33)

Cuatro hallazgos 🔴/🟠 de la corrida de secciones 3–4 convivieron con una suite verde porque la cobertura
**ejercitaba el endpoint sin probar la condición que importaba**. El caso extremo: un `500` permanente en
`POST /api/v1/company/users` sobrevivió semanas con un test que golpeaba ese endpoint **11 veces por
corrida**.

El detalle de los cuatro mecanismos está en [H-33](hallazgos-corrida-secciones-3-4.md#h-33). Este documento
es solo el ajuste.

## Lo que este plan NO es

**No es "agreguen más asserts".** Barrí los 753 métodos de test de integración: **cero** tests sin
aserciones, y una sola llamada con respuesta descartada. La suite es disciplinada en *tener* aserciones. En
los cuatro casos el assert existía y estaba sobre la cosa equivocada, o sobre un escenario donde la
condición bajo prueba no podía variar.

Por eso el ajuste es 1 regla de práctica + 3 guardarraíles acotados, y no una auditoría de 753 tests.

---

## R1 · La regla · Un test nuevo se corre en rojo antes de arreglar

**Antes de aplicar el fix, correr el test nuevo contra el código sin arreglar y confirmar que falla, y que
falla por la razón esperada.**

Es la única de las cuatro instancias de H-33 que se habría cazado sola, y en las tres correcciones donde se
aplicó encontró algo real cada vez:

| Dónde | Qué encontró |
|---|---|
| H-02 | el guardrail nació en rojo señalando **1 de 91** call sites, con el diagnóstico completo |
| H-04 | los dos tests fallaron con `403 COMPANY_OWNERSHIP_FORBIDDEN`, probando que el escenario sí montaba un no-propietario |
| H-02 | la primera versión de mi propio barrido reportaba **88 falsos positivos** por no modelar los *ambient values* de ASP.NET — un guardrail que se habría desactivado el primer día |

Corolario que vale igual para los guardarraíles: **uno que nace verde no prueba nada.** Es la misma trampa
del hallazgo, aplicada a la herramienta que debería detectarlo.

**Cómo se aplica:** en el PR, el mensaje del commit del test (o el cuerpo del PR) dice qué falló y con qué
error antes del fix. Una línea. No hace falta ceremonia; hace falta que exista.

---

## R2 · Checklist de 4 preguntas, para tests de reglas de autorización o de estado

Las tres que no son automatizables. Se responden al escribir el test, no al revisar:

1. **¿El actor del fixture falla la condición fuerte?** Si el test prueba "basta con ser miembro", el actor
   **no** puede ser además el dueño. El seeder da al actor la propiedad de las dos empresas
   (`IntegrationTestSeeder.cs:33-34`) — quien pruebe autorización de empresa tiene que montar su propio
   escenario o el test pasa por el camino equivocado (H-33 #3).
2. **¿El assert es sobre el comportamiento correcto, o sobre el actual?** Un test que fija el defecto como
   esperado es un candado, no un hueco (H-33 #4).
3. **¿Hay una capa antes de la que quiero probar?** El rate limiter corre antes del authorization; un
   `[Authorize]` corre antes del handler. Llegar a la capa N no prueba nada de la capa N−1 (H-33 #2).
4. **Si el código estuviera roto de la forma obvia, ¿este test se pondría rojo?** El resumen de las otras
   tres.

**Dónde vive:** en `AGENTS.md` / la guía de contribución, junto a las convenciones de tests que ya existen.
Cuatro preguntas, no un documento.

---

## R3 · Los tres guardarraíles que sí se automatizan

Van al proyecto de unitarios, siguiendo el patrón de los 26 tests de governance del repo (lectura de fuente
con `FindRepositoryRoot()`, como `PositionSlotDomainErrorMappingGuardrailsTests` y el
`CreatedAtActionRouteKeyGovernanceTests` que ya existe de H-02).

### G1 · Un loop que corta en un estado terminal debe assertar los intermedios

Pinning de lo ya corregido en H-02. Un `for` que contiene `if (r.StatusCode == X) break;` debe contener
también una aserción sobre `r` dentro del loop.

```
✗  if (r.StatusCode == TooManyRequests) break;          // y nada más
✓  if (r.StatusCode == TooManyRequests) break;
   Assert.True((int)r.StatusCode < 500, $"call {index} returned {(int)r.StatusCode}…");
```

**Invariante elegida: no-5xx, no "éxito".** Algunos loops mandan input inválido a propósito —
`PersonnelFiles_Lifecycle_ShouldRateLimit` usa un `If-Match` obsoleto y sus respuestas previas son `409`
legítimos. Un `4xx` puede ser correcto; un `5xx` nunca lo es.

### G2 · Ningún endpoint puede tener como única cobertura un loop de rate limit

El barrido que destapó H-02, convertido en test. Hoy pasa: era `/api/v1/company/users` y ya tiene su
happy-path.

### G3 · Ninguna llamada HTTP con la respuesta descartada

El olor mecánico que sí existe, con **1** instancia hoy — introducida por mí en
`JobProfiles_Publish_WithAdminButWithoutPublishPermission_ShouldReturn403`, que hace dos `PostJsonAsync`
sin verificar. Se arregla junto con el guardrail.

> **Lo que deliberadamente NO se automatiza:** las preguntas 1, 2 y 3 de R2. Un fixture que vuelve
> constante la condición bajo prueba, o un assert sobre el comportamiento equivocado, son indistinguibles
> de un test correcto para cualquier análisis estático. Prometer un guardrail para eso sería exactamente el
> tipo de falsa señal que este hallazgo describe.

---

## ✅ EJECUTADO — 2026-08-09

`AGENTS.md` §5 Paso 5 lleva R1 y las 4 preguntas de R2. Los tres guardarraíles están en
`tests/CLARIHR.Application.UnitTests/IntegrationTestQualityGovernanceTests.cs`.

**Unitarios: 2870/2870** (2867 + los 3 nuevos). Build limpio.

### Lo que la verificación encontró — y por qué era el paso que importaba

Aplicar R1 a los guardarraíles mismos destapó **dos defectos en ellos**, ambos del tipo que este plan
existe para prevenir:

**G2 nacía en verde y no servía para nada.** Al borrar el happy-path del invite, seguía pasando. Causa: la
clave era solo la ruta, sin el **verbo**, y con el query string descartado `GET /api/v1/company/users` (el
listado) y `POST /api/v1/company/users` (el invite) colapsaban a la misma entrada. O sea: el test del
listado "cubría" el invite, y **el guardrail escrito para cazar H-02 no habría cazado H-02**. Corregido a
`VERBO ruta`; ahora al quitar el happy-path falla señalando exactamente `POST /api/v1/company/users`.

**G2 también dio un falso positivo:** reportaba `/api/v1/companies/{id}/location-groups/tree` como ciego,
porque solo leía cuerpos de `public async Task` y ese endpoint se consume desde un helper privado
(`GetLocationGroupTreeAsync`, que sí llama `EnsureSuccessStatusCode`). Un guardrail con falsos positivos se
desactiva el primer día, así que ahora cuenta como cobertura todo lo que no sea el cuerpo de un test de
rate limit — helpers incluidos.

### Que muerden, comprobado rompiendo a propósito

| | Cómo se verificó | Resultado |
|---|---|---|
| **G1** | quitando el assert de uno de los 9 loops endurecidos | 🔴 lo detecta |
| **G2** | neutralizando el happy-path del invite | 🔴 señala `POST /api/v1/company/users` |
| **G3** | ya nació en rojo: 2 llamadas descartadas, ambas mías en un test de H-01 | 🔴 → arregladas |
| **G4** | quitando el `[Fact]` de un test | 🔴 lo nombra exacto |

G3 es el único que encontró un defecto preexistente. G1, G2 y G4 son pinning: no había nada más roto que lo
que H-02 ya había corregido.

### G4 · el guardrail que agregó un cuarto modo de falla (2026-08-09)

**Un `public async Task` que pierde su `[Fact]` desaparece de la suite en silencio absoluto:** compila, nadie
lo referencia, y ningún conteo lo delata porque nadie sabe cuántos tests *debería* haber. Me pasó al insertar
los tests de H-14 — dejé un `[Fact]` colgando y el test de inactivación del tabulador perdió el suyo. Lo
detecté revisando mi propia edición, no el compilador ni la suite.

La señal es la convención de nombres, y acá es inequívoca: los **757** tests con atributo llevan guion bajo,
y ningún método público legítimamente sin atributo (`InitializeAsync`, `UploadStreamAsync`, helpers de las
factories, miembros de interfaz) lo lleva. Cubre **los dos** proyectos de test: un unitario huérfano es igual
de invisible.

**Y G4 volvió a demostrar la regla en carne propia.** Mi primera versión reportó **4 huérfanos** y anuncié uno
como defecto real. **Los cuatro eran falsos positivos:** los cuatro sí tenían `[Theory]`, pero había
comentarios entre los `[InlineData]` y mi bloque de atributos solo toleraba espacios, así que el regex cortaba
en el comentario. **No hay ningún test huérfano en el repo.** Es la tercera vez en el mismo trabajo que un
guardrail mío nace defectuoso, y las tres veces lo destapó correrlo antes de confiar en él.

### Lo que deliberadamente quedó sin automatizar

Las preguntas 1, 2 y 3 de R2 — fixture que vuelve constante la condición, assert sobre el comportamiento
actual, capa de middleware que oculta otra. Son indistinguibles de un test correcto para cualquier análisis
estático, y prometer un guardrail para eso sería la misma falsa señal que el hallazgo describe.
