# 00000 — CreateCompany · Hallazgos de backend

| | |
|---|---|
| **ID** | 00000-CreateCompany |
| **Documento espejo** | [`ComentariosPruebasFrontend/00000-CreateCompany`](../ComentariosPruebasFrontend/00000-CreateCompany.md) |
| **Paso probado** | Ninguno del asistente — la pantalla anterior al Paso 1 |
| **Pantalla que los destapó** | `/companies/create` |
| **Fecha** | 2026-08-15 · revisado y ampliado 2026-08-15 |
| **Ambiente** | `https://dashboard.clarihr.com` |

---

## 1. Resumen

| ID | Sev. | Hallazgo | Componente | Origen | Alcance | Estado |
|---|---|---|---|---|---|---|
| [B-01](#2-b-01--el-camino-patch-se-salta-la-frontera-de-fechas-diez-lectores-leen-el-jsonelement-en-crudo) | 🔴 Alta | El camino `PATCH` se salta la frontera de normalización de fechas: diez lectores leen el `JsonElement` en crudo | API · Application · 10 *patch appliers* | Revisión de código al medir B-02 | **Transversal** · 10 archivos | 🟢 **Resuelto** |
| [B-02](#3-b-02--tres-fechas-de-calendario-viajan-como-datetime-contra-la-convención-dateonly-del-propio-producto) | 🟡 Media | Tres fechas de calendario se modelan como `DateTime`, contra la convención `DateOnly` que el producto ya aplica en 228 lugares | Domain · `LegalRepresentative` | Espejo F-03 | Una entidad medida · **82 candidatos contados** | 🟢 **Resuelto** |
| [B-03](#4-b-03--la-regla-de-tipos-de-fecha-no-está-escrita-en-las-definiciones-técnicas) | 🟡 Media | La regla día↔instante no está en `definiciones-tecnicas-backend.md` ni tiene guardrail; por eso la entidad derivó y es incoherente consigo misma | Docs · Guardrails | Causa raíz de B-01 y B-02 | **Transversal** | 🟢 **Resuelto** |
| [B-04](#5-b-04--el-representante-inicial-puede-quedar-sin-marcar-como-principal) | 🔵 Baja | El representante legal inicial puede quedar con `is_primary = NULL`: el camino de provisioning no aplica default ni exclusividad | Application · `CompanyProvisioningService` | Pregunta abierta del espejo, ahora comprobada | **Tres caminos** (medidos al ejecutarlo) | 🟢 **Resuelto** |

**Ninguno bloquea al frontend.** B-01 sí producía un `500` alcanzable desde un cliente que usara `PATCH` con una fecha escrita de la forma natural; **está corregido y verificado** (§2.8).

> **Estado al 2026-08-16. Los cuatro hallazgos cerrados.** B-01, B-02, B-03 y B-04 resueltos y verificados.
>
> ⚠️ **B-02 rompió el contrato a propósito** (§3.7): `appointmentDateUtc`/`effectiveFromUtc`/`effectiveToUtc` pasaron a `appointmentDate`/`effectiveFrom`/`effectiveTo`, y de `"2026-08-15T00:00:00Z"` a `"2026-08-15"`. **El frontend tiene que ajustarse**; el detalle para el cliente está en el documento espejo.
>
> Suites: **unit 2968/2968** · **integración dirigida 103/103** · build sin warnings con `TreatWarningsAsErrors` · `openapi.yaml` regenerado y sin drift de modelo EF.

> **Nota de renumeración (2026-08-15).** El hallazgo original de este documento —las tres fechas como `DateTime`— pasó de `B-01` a **`B-02`**: al levantarse un hallazgo de severidad Alta, el README §2 obliga a renumerar en orden de severidad. Las referencias del documento espejo y de `00006-JobProfiles` quedaron actualizadas en el mismo cambio.

### Nota de contraste — lo que esta pantalla hace bien

- **El catálogo de tipos de empresa se resuelve por país correctamente**: deshabilitado hasta que hay país, y entonces carga el juego salvadoreño. Es el comportamiento que faltaba en los catálogos del hallazgo H-21.
- **La autorización está bien planteada y bien documentada**: esta familia no usa RBAC sino propiedad contra el `sub` del JWT, y el propio controlador **explica por qué está excluida de `[AuthorizationPolicySet]`** en vez de dejar la omisión sin justificar. Es el modelo de cómo se documenta una excepción.
- **La empresa nace vacía**: ni un catálogo de conveniencia. Coherente con la regla de no sembrar nada que no fije la ley o la geografía.
- **`concurrencyToken` viaja en el cuerpo** de la respuesta de creación, así que el `PUT` siguiente no depende de la cabecera `ETag` — que el proxy descarta.

---

## 2. B-01 — El camino `PATCH` se salta la frontera de fechas: diez lectores leen el `JsonElement` en crudo

| | |
|---|---|
| **Severidad** | 🔴 Alta |
| **Estado** | 🟢 **Resuelto** — 2026-08-15, verificado (§2.8) |
| **Componente** | `CLARIHR.Application` · 10 *patch appliers* · frontera de `CLARIHR.Api` |
| **Origen** | Revisión de código al medir el alcance de B-02 |
| **Alcance** | **Transversal** — 10 archivos medidos |

### 2.1 Evidencia

H-26 normalizó las fechas **en la frontera**, y cubrió dos puertas de entrada: el cuerpo JSON (`UtcDateTimeJsonConverter`) y los `[FromQuery] DateTime` de los reportes (`UtcDateTimeModelBinder`). El propio `Program.cs` lo dice:

```csharp
// src/CLARIHR.Api/Program.cs:89-92
// H-26 — a date without a zone (`"2026-08-01"`) used to reach Npgsql as Kind=Unspecified and blow up in
// the first SQL comparison that touched it. Normalized here, once, for every endpoint.
options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
```

**Hay una tercera puerta que no se cubrió.** Los *patch appliers* de JSON Patch no reciben un `DateTime` deserializado: reciben un `JsonElement` y lo leen ellos mismos, sin pasar por ningún converter.

```csharp
// src/CLARIHR.Application/Features/LegalRepresentatives/LegalRepresentativeAdministration.cs:1521
return value!.Value.ValueKind == JsonValueKind.String && value.Value.TryGetDateTime(out var parsed)
    ? parsed                                    // ← el Kind sale como venga: Unspecified o Local
    : throw new LegalRepresentativePatchValueException(path, "Value must be an ISO-8601 date-time or null.");
```

El mismo patrón, verbatim, en **diez archivos**:

```
src/CLARIHR.Application/Features/LegalRepresentatives/LegalRepresentativeAdministration.cs:1521
src/CLARIHR.Application/Features/JobProfiles/JobProfileAdministration.cs:1927
src/CLARIHR.Application/Features/PersonnelFiles/Shell/PersonnelFileCore.PatchAppliers.cs:295
src/CLARIHR.Application/Features/PersonnelFiles/Background/Educations.PatchAppliers.cs:298
src/CLARIHR.Application/Features/PersonnelFiles/Background/PreviousEmployments.PatchAppliers.cs:229
src/CLARIHR.Application/Features/PersonnelFiles/Background/Trainings.PatchAppliers.cs:265
src/CLARIHR.Application/Features/PersonnelFiles/PersonalInfo/FamilyMembers.PatchAppliers.cs:292
src/CLARIHR.Application/Features/PersonnelFiles/PersonalInfo/Identifications.PatchAppliers.cs:185
src/CLARIHR.Application/Features/PersonnelFiles/Interests/Associations.PatchAppliers.cs:187
src/CLARIHR.Application/Features/PersonnelFiles/Talent/TalentPatch.cs:62
```

### 2.2 Causa

`JsonElement.TryGetDateTime()` no es el converter: es el parser crudo de `System.Text.Json`. Devuelve

- `Kind=Unspecified` si el texto no trae zona (`"2026-08-15"`, la forma natural), y
- `Kind=Local` si trae offset (`"2026-08-15T18:07:00-06:00"`), ya convertido a la hora local **del servidor**.

Ninguno de los dos es `Utc`, que es lo único que la columna `timestamptz` acepta.

**Nueve de los diez sitios se salvan por el agregado**, que relabela después:

```csharp
// src/CLARIHR.Domain/PersonnelFiles/PersonnelFileNormalization.cs:46-47
public static DateTime NormalizeDate(DateTime value) =>
    DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
```

**`LegalRepresentative.AppointmentDateUtc` no tiene ninguna red.** Se asigna crudo en los dos constructores del agregado, sin `.Date` y sin `SpecifyKind`:

```csharp
// src/CLARIHR.Domain/LegalRepresentatives/LegalRepresentative.cs:35 y :136
AppointmentDateUtc = appointmentDateUtc;
```

Nótese el contraste dentro de **la misma entidad**: `SetEffectiveDates` sí trunca con `.Date` (`:191-192`). Tres campos del mismo tipo semántico, dos tratamientos — la incoherencia se levanta aparte en [B-03](#4-b-03--la-regla-de-tipos-de-fecha-no-está-escrita-en-las-definiciones-técnicas).

### 2.3 Impacto

**Dos daños distintos, según el sitio:**

1. **`500` reproducible** — `PATCH /api/v1/legal-representatives/{publicId}` con `{"op":"replace","path":"/appointmentDateUtc","value":"2026-08-15"}` lleva `Kind=Unspecified` hasta Npgsql sobre una columna `timestamptz`. Es exactamente el mecanismo que H-26 documentó y arregló para las otras dos puertas. **La familia de representantes legales es la única expuesta a esto**, porque es la única sin normalización en el agregado.

2. **Corrimiento de día** — donde el agregado sí relabela, el `500` no ocurre pero el día puede saltar: `TryGetDateTime` entrega la hora local del servidor y `NormalizeDate` hace `.Date` sobre ella. Es literalmente el caso contra el que advierte el comentario del converter hermano:

   > *«An offset is meaningless for a day field, so the day is read as written rather than shifted into UTC — shifting it would move a birth date across midnight.»*
   > — `src/CLARIHR.Api/Configuration/LenientDateOnlyJsonConverter.cs:37-38`

   Y las fechas que se tocan por este camino son precisamente ésas: `birthDate`, `issuedDate`/`expiryDate`, fechas de educación, de empleos anteriores, de capacitaciones.

> **Medido por lectura de código, no por corrida.** El mecanismo está probado en este repo (es el de H-26) y el camino está leído de punta a punta, pero el `500` no se ejecutó. La sonda roja del plan de trabajo es la confirmación, y es **el primer paso** — no se arregla antes de verlo fallar.

### 2.4 Propuesta

Un solo lector compartido que enrute por la frontera que ya existe, y aplicarlo en los diez sitios:

```csharp
// en vez de: value.Value.TryGetDateTime(out var parsed) → parsed
//            value.Value.TryGetDateTime(out var parsed) → UtcDateTimeJsonConverter.ToUtc(parsed)
```

Y, en `LegalRepresentative`, normalizar `AppointmentDateUtc` en el agregado como ya se hace con las otras dos — la defensa en profundidad que los otros nueve sí tienen.

**Se arregla aunque B-02 se apruebe.** Si los tres campos pasan a `DateOnly`, el lector de `appointmentDate` cambia de tipo pero los otros nueve sitios siguen leyendo `DateTime` en crudo. Son hallazgos independientes: éste es el defecto vivo, B-02 es el modelo.

### 2.5 Compatibilidad

✅ **No rompe el contrato.** No cambia tipos ni formas en el wire: cambia qué `Kind` lleva el valor internamente. `openapi.yaml` no se toca. Un cliente que hoy recibe `500` empezará a recibir `200`, que es la única diferencia observable.

### 2.6 Alcance a revisar

Los diez sitios están contados y listados en §2.1 — no queda alcance por medir en esta familia. Lo que sí conviene revisar es si aparecen **lectores nuevos** con el mismo patrón: es lo que cubre el guardrail propuesto en [B-03](#4-b-03--la-regla-de-tipos-de-fecha-no-está-escrita-en-las-definiciones-técnicas).

### 2.7 Vía alterna vigente

El frontend puede evitar el `500` **hoy** enviando siempre la forma de instante explícita en UTC (`"2026-08-15T00:00:00Z"`) en los `PATCH` de fecha. Es la misma convención que el playbook ya documentaba; el problema es que la forma natural, la que cualquiera escribe primero, es la que falla.

### 2.8 Lo que se hizo, y cómo se comprobó

**Rojo antes que verde, en los dos niveles.**

**1. El guardrail, rojo primero.** `CalendarDateTypeGuardrailsTests.PatchAppliers_ShouldNotReadDatesWithRawTryGetDateTime` se escribió antes del arreglo y falló señalando los diez sitios. Los otros dos casos del mismo archivo pasaron desde el principio, lo que confirma que la *allow-list* del inventario estaba exacta.

**2. El comportamiento, rojo verificado mutando.** `CalendarDateReaderTests` se corrió contra el comportamiento viejo (`CalendarDateReader.TryReadDayAsUtcMidnight` sustituido temporalmente por el parseo crudo). **4 de 4 casos en rojo, por las dos razones esperadas:**

```
"2026-08-15"            → Expected: Utc                      Actual: Unspecified      ← el 500
"2026-08-15T00:00:00Z"  → Expected: 2026-08-15T00:00:00Z     Actual: 2026-08-14T18:00:00-06:00
```

> ⚠️ **El segundo resultado corrige otra suposición.** Se esperaba que solo la forma con offset corriera el día. Medido: en una máquina en `CST-0600`, **incluso `T00:00:00Z` retrocedía al 14 de agosto**, porque el parseo crudo convierte a hora local antes de que nadie mire la fecha. El desplazamiento depende de la zona del **servidor**, no solo de la del cliente — y por eso el test asserta medianoche UTC exacta, que es una aserción independiente de dónde corra.

**3. El arreglo.**

- `src/CLARIHR.Domain/Common/CalendarDateReader.cs` — nuevo. Vive en el dominio porque es la única capa que ven a la vez los converters de `CLARIHR.Api` y los *patch appliers* de `CLARIHR.Application`; **tener la regla en dos sitios era el defecto**. Expone `TryReadDay` (día como está escrito), `TryReadDayAsUtcMidnight` (el puente para las columnas aún en `timestamptz`), `NormalizeDay` (etiquetar, no convertir) y `ToUtcInstant` (los instantes sí se convierten).
- Los **10 lectores** pasan por él. `TryGetDateTime` ya no aparece en `CLARIHR.Application`.
- `UtcDateTimeJsonConverter` y `LenientDateOnlyJsonConverter` delegan en el mismo, para que no vuelva a haber dos copias que puedan divergir.
- `LegalRepresentative` normaliza ahora sus **tres** fechas —`AppointmentDateUtc` no tenía red, y `SetEffectiveDates` conservaba el `Kind` de entrada al truncar con `.Date`—. Esto cierra también la incoherencia interna que señalaba B-03 §4.2.

**4. Verde, end-to-end.** `LegalRepresentatives_PatchAppointmentDate_ShouldStoreTheDayAsWritten` (integración, contra API y base reales) prueba las tres formas del mismo día: `"2026-08-15"`, `"2026-08-15T00:00:00Z"` y `"2026-08-15T18:07:00-06:00"`. **Las tres devuelven `200` y almacenan el 15 de agosto.**

| Suite | Resultado |
|---|---|
| Unit (`CLARIHR.Application.UnitTests`) | **2967 / 2967** |
| Integración dirigida (frontera de fechas · representantes legales · perfiles de puesto) | **110 / 110** |
| Build | 0 errores · 0 warnings (con `TreatWarningsAsErrors`) |

**No requiere migración ni cambio de `openapi.yaml`**, como anticipaba §2.5.

### 2.9 Una trampa que apareció al arreglarlo

Aplicar la lectura de **día** a los diez lectores por igual era lo natural, y era incorrecto para uno.

`JobProfile.NormalizeOptionalUtc` (`JobProfile.cs:480-493`) trata `EffectiveFromUtc`/`EffectiveToUtc` como **instantes**: convierte `Local` y **no trunca**. Su `PUT` guarda la hora que le manden. Al darle al *patch applier* semántica de día, `PATCH` habría empezado a guardar medianoche mientras `PUT` seguía guardando la hora — **una divergencia nueva entre dos caminos de escritura del mismo campo**, introducida por el propio arreglo.

Se resolvió con un lector aparte, `CalendarDateReader.TryReadInstant`, que replica exactamente las tres reglas del converter del cuerpo JSON. **La regla que quedó:** cada lector de `PATCH` usa la semántica de **su propio agregado**, no la que parezca más correcta en abstracto.

> Que esos dos campos **deberían** ser días es cierto —están en el inventario de [B-02](#3-b-02--tres-fechas-de-calendario-viajan-como-datetime-contra-la-convención-dateonly-del-propio-producto) §3.8—, pero eso se arregla **cambiando el tipo**, que corrige los dos caminos a la vez. Divergirlos por adelantado habría cambiado el comportamiento de `PUT` sin que ningún hallazgo lo pidiera, y habría dejado el sistema en un estado intermedio peor que el original.
>
> A diferencia de `PositionSlot`, cuya condición de instante **sí** es deliberada (decisión de H-26), la de `JobProfile` parece accidental: nadie decidió que un perfil de puesto entrara en vigor a una hora concreta, simplemente nunca se truncó. Anotado para que B-02 lo trate como conversión, no como excepción.

### 2.10 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al medir el alcance de B-02: buscando cuántas entidades comparten el tipo, apareció que el camino `PATCH` nunca pasó por la frontera de H-26. **Sobrevivió al arreglo de H-26 porque el arreglo cubrió las dos puertas que el hallazgo original había visto, y JSON Patch es una tercera.** Lección repetida: un arreglo de frontera vale lo que valga el inventario de fronteras |
| 2026-08-15 | 🟢 Resuelto | Arreglado y verificado (§2.8). **El `500` estaba bien diagnosticado y el corrimiento de día resultó peor de lo estimado**: no hacía falta offset en el cliente para perderlo, bastaba con que el servidor no corriera en UTC |

---

## 3. B-02 — Tres fechas de calendario viajan como `DateTime`, contra la convención `DateOnly` del propio producto

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-16, verificado (§3.11) |
| **Componente** | Domain · `LegalRepresentative` · y su cadena hasta el DTO |
| **Origen** | Espejo **F-03** — el valor por defecto de la fecha salía con un día de más |
| **Alcance** | Una entidad medida · **el resto del dominio ya contado: 82 candidatos** |

> Este hallazgo era `B-01` hasta la revisión del 2026-08-15.

### 3.1 Evidencia

Las tres fechas del representante legal son instantes:

```csharp
// src/CLARIHR.Domain/LegalRepresentatives/LegalRepresentative.cs
public DateTime? AppointmentDateUtc { get; private set; }   // :66
public DateTime  EffectiveFromUtc   { get; private set; }   // :68
public DateTime? EffectiveToUtc     { get; private set; }   // :70
```

Y llegan así hasta el contrato público, sin cambiar de tipo:

```csharp
// AccountCompaniesController.cs:253-255
DateTime? AppointmentDateUtc,
DateTime  EffectiveFromUtc,
DateTime? EffectiveToUtc,
```

**Las tres responden a «¿qué día?», no a «¿en qué momento?»**: el día en que se nombró al representante, el día desde el que su nombramiento tiene efecto y el día en que deja de tenerlo. Ninguna tiene hora que signifique nada.

### 3.2 El producto ya decidió lo contrario, en 228 lugares

`DateOnly` no es una propuesta nueva: **es la convención vigente**, aplicada de forma consistente en el resto del dominio.

```bash
$ grep -rho 'DateOnly' src/CLARIHR.Domain/ | wc -l
228
```

Y no en un rincón, sino en los módulos donde las fechas tienen consecuencia legal y económica:

```
PayrollRun.cs · PersonnelFileEmployee.cs · PersonnelFileRecurringIncome.cs
PersonnelFileRecurringDeduction.cs · PersonnelFilePersonnelTransactions.cs
PersonnelFileOvertimeRecord.cs · PersonnelFileOneTimeDeduction.cs
PersonnelFileCompensatoryTime.cs   (…y más)
```

**Esto cambia la naturaleza del hallazgo.** No es «convendría usar otro tipo»: es **una entidad que quedó fuera de una decisión que el producto ya tomó**. El costo de discutirlo es cero — está discutido.

### 3.3 Causa

El sufijo `Utc` en los tres nombres delata el razonamiento: se trató la fecha como un instante y se resolvió la ambigüedad de zona horaria fijándola en UTC. **Es la solución correcta para un instante y la equivocada para un día**, porque un día no tiene zona horaria que fijar.

### 3.4 Impacto

**El cliente está obligado a inventarse una hora**, y no todas las horas son equivalentes. Medido contra el código de la frontera (`UtcDateTimeJsonConverter.cs:34-39`, que convierte `Local → ToUniversalTime()`) y el truncado del agregado (`LegalRepresentative.cs:191`), desde El Salvador (−06:00):

| Enviado | Instante UTC resultante | Día almacenado | ¿Se corre? |
|---|---|---|---|
| `2026-08-15` *(sin zona)* | `2026-08-15T00:00:00Z` | 15 de agosto | No |
| `2026-08-15T00:00:00Z` | `2026-08-15T00:00:00Z` | 15 de agosto | No |
| `2026-08-15T00:00:00-06:00` | `2026-08-15T06:00:00Z` | 15 de agosto | No |
| `2026-08-15T17:59:00-06:00` | `2026-08-15T23:59:00Z` | 15 de agosto | No |
| **`2026-08-15T18:07:00-06:00`** | **`2026-08-16T00:07:00Z`** | **16 de agosto** | **Sí** |

**La ventana de falla son las últimas seis horas del día local, no el día entero.** Es lo que el espejo F-03 describe con precisión («durante seis horas de cada día»), y es el caso exacto que se observó: el frontend precargó **el 16 de agosto a las 18:07 del 15**.

> ⚠️ **Corrección respecto de la primera redacción de este hallazgo.** La versión original afirmaba que «si envía medianoche local, se corre igual» y que «solo medianoche UTC funciona». **Es falso para El Salvador**: medianoche local (−06:00) es 06:00 UTC del mismo día y no se corre. La afirmación solo vale para husos **al este** de Greenwich. Se corrige aquí porque una sobreestimación del impacto es la clase de imprecisión que hace desconfiar del hallazgo entero — y el hallazgo es correcto.

**Y el riesgo no acaba en el cliente.** Un `timestamptz` convertido a fecha en consulta usa el `TimeZone` de la **sesión** de base de datos, no el de UTC. Cualquier informe que agrupe representantes por fecha de vigencia puede correr un día según cómo esté configurada la sesión que lo ejecute — **una trampa ya medida en este proyecto**. Con `date` en la columna, el riesgo desaparece.

**Hoy el daño es acotado**: son fechas de un dato documental, no de nómina. Por eso es Media y no Alta.

### 3.5 Qué decisión previa toca este hallazgo

Hay que decirlo explícitamente para que no parezca que se reabre algo cerrado. Al cerrar **H-26/H-28** se convirtieron los campos de día del camino de contratación y se dejó anotado que los ~166 campos restantes

> *«ya no fallan porque la frontera los normaliza; convertirlos es expresividad, no arreglo».*

**Esa conclusión es cierta para el `500` y falsa para el día.** La frontera evita que Npgsql rechace el parámetro, pero **no** preserva el día del calendario cuando el cuerpo trae offset — y el propio repositorio lo sabe, porque el converter de `DateOnly` documenta justamente lo contrario para su tipo (`LenientDateOnlyJsonConverter.cs:37-38`, citado en B-01 §2.3).

Es decir: **los campos `DateOnly` están protegidos del corrimiento; los campos `DateTime` que son días, no.** Este hallazgo mide el riesgo residual que H-26 dejó abierto conscientemente, con información que entonces no se tenía. No re-litiga la decisión: la completa.

### 3.6 Propuesta

Migrar las tres a `DateOnly` en el dominio, `date` en la columna y `"2026-08-15"` en el JSON, alineándolas con las otras 228.

**Y quitarles el sufijo `Utc` del nombre**, que es la parte que induce el error de lectura: `AppointmentDate`, `EffectiveFrom`, `EffectiveTo`.

> ⛔ **Se descarta la «mitigación barata»** que proponía la primera redacción (documentar en el endpoint que se espera medianoche UTC). El propio hallazgo la calificaba de mitigación y no de solución; y sin producción, la migración no tiene coste de clientes que la justifique. Además `LenientDateOnlyJsonConverter` ya acepta la forma de instante, así que **el frontend actual no se rompe el día del despliegue**: se ajusta después, sin prisa.

### 3.7 Compatibilidad

⚠️ **Rompe el contrato.** `"2026-08-15T00:00:00Z"` pasa a `"2026-08-15"`. Requiere:

- regenerar `docs/technical/api/openapi.yaml`;
- ajustar el frontend en las tres fechas de esta pantalla y en las de la pantalla de representantes legales del Paso 1;
- renombrar el CHECK `ck_legal_representatives__effective_dates` y el índice `ix_legal_representatives__tenant_effective_dates`;
- migración de esquema `timestamptz → date`. **Anclar la conversión con `AT TIME ZONE 'UTC'`**, o la migración misma correrá las fechas un día en los registros existentes — que es la trampa que este hallazgo intenta eliminar:

```sql
ALTER TABLE legal_representatives
  ALTER COLUMN effective_from TYPE date USING (effective_from_utc AT TIME ZONE 'UTC')::date;
```

**No hay producción**, así que no hay clientes que migrar ni datos históricos que preservar. Es el momento barato para hacerlo.

**Alcance del cambio, medido:** ~88 referencias en 10 archivos —
`LegalRepresentativeAdministration.cs` (32) · `LegalRepresentativesController.cs` (13) · `LegalRepresentativeRepository.cs` (8) · `LegalRepresentative.cs` (8) · `LegalRepresentativeCommon.cs` (6) · `LegalRepresentativeConfiguration.cs` (4) · `CompanyProvisioningService.cs` (3) · `AccountCompaniesController.cs` (3) · y 11 en dos proyectos de test.

Técnica recomendada, la que ya funcionó en H-26: **borrar el tipo y dejar que el compilador enumere.** No hay conversión implícita `DateOnly`↔`DateTime`, así que ningún cruce queda silencioso.

### 3.8 Alcance a revisar — **montaje ejecutado**

La primera redacción dejaba el conteo pendiente con el montaje definido. **Ejecutado el 2026-08-15, sin ambiente:**

```bash
$ grep -rhoE 'public\s+DateTime\??\s+\w+' src/CLARIHR.Domain/ | wc -l
164
```

Clasificando por lo que el nombre denota:

| | Cuenta | Ejemplos |
|---|---|---|
| **Instantes genuinos** — correctos como `DateTime` | **82** | `CreatedUtc` · `AnnulledUtc` (14) · `DecidedUtc` (7) · `RevokedUtc` · `ExpirationUtc` · `SourceSyncedUtc` (6) |
| **Días disfrazados de instante** — candidatos | **82** | `EffectiveFromUtc`/`EffectiveToUtc` (8 pares) · `StartDate`/`EndDate` (5+5) · `BirthDate` (3) · `HireDate` · `RetirementDate` (4) |

De los 82 candidatos hay que **descontar los que el proyecto ya decidió dejar como instante a propósito** al cerrar H-26: `PositionSlot.EffectiveFrom/To`, `TransactionDateUtc`, el historial de contrato, los conceptos de compensación, el motor de finiquitos (certificado) y el snapshot de la baja. Quedan **≈68 campos abiertos**.

**Esto cambia el tamaño del hallazgo.** No es «una entidad residual»: los tres campos de `LegalRepresentative` son el 4 % del problema. El patrón dominante son los **ocho pares `EffectiveFromUtc`/`EffectiveToUtc`** repartidos en ocho entidades distintas.

Orden sugerido para el resto, por consecuencia legal y económica: `PersonnelFileEmployee` (ancla de antigüedad) → `PersonnelFile` (nacimiento, documentos) → suscripciones y addons (`EffectiveDateUtc`) → catálogos con vigencia.

> ✅ **El inventario ya no es una lista en un documento: es un test.** Desde el cierre de [B-03](#4-b-03--la-regla-de-tipos-de-fecha-no-está-escrita-en-las-definiciones-técnicas), las entradas viven en la *allow-list* de `CalendarDateTypeGuardrailsTests`. Cada conversión borra su línea, un campo nuevo mal tipado rompe CI, y un segundo test impide que la lista se quede con entradas muertas que oculten el progreso. **El avance de este hallazgo se mide vaciando esa lista.**

#### Seguimiento — estado al 2026-08-16

```
allow-list total ......... 79
  ├─ [H-26] instantes deliberados ... 15   ← NO son deuda, se quedan
  └─ deuda real pendiente ........... 64
convertidos hasta hoy ..... 3  (LegalRepresentative, este hallazgo)
```

⚠️ **La cifra que importa es 64, no 79.** Los 15 marcados `[H-26]` son instantes por decisión tomada y documentada; contarlos como deuda haría que el trabajo pareciera imposible de terminar.

**Los 15 que se quedan como instante** — `PositionSlot.EffectiveFrom/To` · `PersonnelFileOffPayrollTransaction.TransactionDateUtc` · `PersonnelFilePayrollTransaction.TransactionDateUtc` · `PersonnelFileContractHistory.ContractDate/ContractEndDate` · `PersonnelFileCompensationConcept.StartDate/EndDate` · `PersonnelFileSettlement.PlazaStartDate/RequestDate/RetirementDate/SeniorityStartDate` · `PersonnelFileRetirementRequest.RequestDate/RetirementDate` · `RetirementRequestClosedRecord.PreviousEndDate`.

**Los 64 pendientes, por olas de consecuencia:**

| Ola | Entidad | Campos | Por qué esta prioridad |
|---|---|---|---|
| **1 — antigüedad y expediente** (16) | `PersonnelFileEmployeeProfile` | `HireDate`, `RetirementDate` | 🔴 **`HireDate` es el ancla de la antigüedad** (vacaciones Art. 177 y finiquito). Un corrimiento aquí es un día de vacaciones o dinero |
| | `PersonnelFile` | `BirthDate` | Fecha de nacimiento: el caso que el converter documenta explícitamente |
| | `PersonnelFileFamilyMember` | `BirthDate`, `DeceasedDate` | |
| | `PersonnelFileInsuranceBeneficiary` | `BirthDate` | |
| | `PersonnelFileIdentification` | `IssuedDate`, `ExpiryDate` | Vigencia de documentos de identidad |
| | `PersonnelFileEducation` | `StartDate`, `EndDate` | |
| | `PersonnelFilePreviousEmployment` | `EntryDate`, `RetirementDate` | |
| | `PersonnelFileTraining` | `StartDate`, `EndDate` | |
| | `PersonnelFileAssociation` | `JoinedDate`, `LeftDate` | |
| **2 — acciones de personal y vigencias** (11) | `PersonnelFilePersonnelAction` | `ActionDateUtc`, `EffectiveFromUtc`, `EffectiveToUtc` | Alimentan la bandeja y el arrastre a planilla |
| | `JobProfile` | `EffectiveFromUtc`, `EffectiveToUtc` | ⚠️ Su `PUT` los trata como **instante** (no trunca). Convertirlos alinea `PUT` y `PATCH` — ver §2.9 |
| | `SalaryTabulatorLine` | `EffectiveFromUtc`, `EffectiveToUtc` | |
| | `SalaryTabulatorChangeRequest` | `EffectiveFromUtc`, `EffectiveToUtc` | |
| | `IncomeTaxWithholdingBracket` | `EffectiveFromUtc`, `EffectiveToUtc` | Tramos de Renta: vigencia legal |
| **3 — solicitudes del empleado** (20) | `PersonnelFileCertificateRequest` | `RequestDateUtc`, `NeededByDateUtc`, `IssuedDateUtc`, `DeliveredDateUtc` | |
| | `PersonnelFileRetirementRequest` | `ResolutionDateUtc`, `CancellationDateUtc`, `ExecutionDateUtc`, `ReversalDateUtc` | ⚠️ Sus otros dos campos son `[H-26]`: **la entidad queda mixta** y hay que decidirlo entidad por entidad |
| | `PersonnelFileEconomicAidRequest` | `RequestDateUtc`, `ResolutionDateUtc`, `DisbursementDateUtc` | |
| | `PersonnelFileMedicalClaim` | `ClaimDateUtc`, `ResolutionDateUtc` | |
| | `PersonnelFileAssetAccess` | `StartDateUtc`, `EndDateUtc`, `DeliveryDateUtc` | |
| | `PersonnelFileVacationRequest` | `DecisionDateUtc` | ⚠️ Discutible: ¿el día de la decisión o el momento? Clasificar antes de convertir |
| | `PersonnelFileInsurance` | `StartDateUtc`, `EndDateUtc` | |
| | `VacationReturn` | `ReturnDateUtc` | |
| **4 — evaluación y otros** (8) | `PersonnelFilePerformanceEvaluation` | `EvaluationDateUtc` | |
| | `PersonnelFilePositionCompetencyResult` | `EvaluationDateUtc` | |
| | `PersonnelFileSelectionContest` | `ContestDateUtc` | |
| | `PersonnelFileAdditionalBenefit` | `StartDate`, `EndDate` | |
| | `PersonnelFileAuthorizationSubstitution` | `StartDate`, `EndDate` | |
| | `ExitInterviewAnswer` | `ValueDate` | |
| **5 — comercial y suscripciones** (9) | `CompanySubscription` | `StartDateUtc`, `EndDateUtc` | Sin consecuencia laboral; el más barato de dejar para el final |
| | `CompanySubscriptionPlanChange` | `EffectiveDateUtc` | |
| | `CompanySubscriptionStatusChangeRequest` | `EffectiveDateUtc` | |
| | `CompanyCommercialAddon` | `StatusEffectiveDateUtc` | |
| | `CompanyCommercialAddonChange` | `EffectiveDateUtc` | |
| | `CommercialPlanVersion` | `EffectiveFromUtc`, `EffectiveToUtc` | |
| | `Company` | `BillableSinceUtc` | |

**Tres cosas aprendidas en la conversión de B-02, aplicables a todas las olas:**

1. **EF genera la migración destructiva** (`DropColumn`+`AddColumn`) o el cast desnudo. Reescribir a mano con `AT TIME ZONE 'UTC'` y **medirla contra datos** antes de darla por buena (§3.12).
2. **Cada conversión rompe contrato** si el campo lleva sufijo `Utc` en el wire. Presupuestar openapi + aviso al frontend.
3. **Antes de convertir, comprobar si el agregado trata el campo como instante** (como `JobProfile`): si `PUT` no trunca, convertir el tipo alinea los dos caminos; hacerlo solo en el applier los diverge (§2.9).

> **Antes de cada ola: reclasificar, no asumir.** El nombre sugiere que es un día pero no lo prueba. Los `*ResolutionDateUtc`, `*DecisionDateUtc` y `*EvaluationDateUtc` son los más discutibles: pueden ser el momento del acto y no el día.

### 3.9 Vía alterna vigente

El frontend puede acertar **hoy** enviando siempre **medianoche UTC del día local elegido**. Funciona en ambos sentidos y no depende de este hallazgo. Es lo que se pidió en el ajuste de F-03.

### 3.11 Lo que se hizo, y cómo se comprobó

**Rojo primero.** `LegalRepresentatives_Dates_ShouldRoundTripAsPlainDays` se escribió contra el contrato objetivo y falló 3/3 con `400 · "'Effective From Utc' must not be empty"` — el contrato no conocía `effectiveFrom`.

**El cambio, capa por capa.**

| Capa | Qué cambió |
|---|---|
| Dominio | `AppointmentDate`, `EffectiveFrom`, `EffectiveTo` → `DateOnly`. `SetEffectiveDates` **perdió toda la normalización**: un `DateOnly` no tiene `Kind` que etiquetar ni hora que truncar. La comparación del rango quedó en `to < from`, sin `.Date` |
| Application · Api · Infrastructure | 132 referencias en 16 archivos. Técnica de H-26: **borrar el tipo y dejar que el compilador enumere** — 12 cruces en `src`, 12 en `tests`, ninguno silencioso |
| Patch applier | `ReadNullableDateTime` → `ReadNullableDate`, que usa `CalendarDateReader.TryReadDay`. El puente a medianoche UTC de B-01 ya no hace falta aquí |
| Esquema | Migración `20260816132935_LegalRepresentativeDatesAsDate` |
| Contrato | 5 esquemas de `openapi.yaml` (14 campos, `format: date-time` → `date`), la descripción del `PATCH`, y 6 menciones en `endpoint-reference.md` |
| Guardrail | Las 3 entradas salieron de la *allow-list*: **82 → 79**. El test de entradas muertas es lo que obliga a que salgan |

**Verde, end-to-end.** `LegalRepresentatives_Dates_ShouldRoundTripAsPlainDays` prueba las tres formas del mismo día contra API y base reales; las tres devuelven `201` y responden **`"2026-12-01"` exacto**, sin parte de hora.

| Suite | Resultado |
|---|---|
| Unit (`CLARIHR.Application.UnitTests`) | **2968 / 2968** |
| Integración dirigida (representantes legales · frontera de fechas · account-companies · provisioning · exports) | **92 / 92** |
| Build | 0 errores · 0 warnings (con `TreatWarningsAsErrors`) |
| Modelo EF | Sin drift (`has-pending-model-changes` limpio) |
| `openapi.yaml` | YAML válido; 5 esquemas con `format: date` |

> ⚠️ **Nota de método.** La primera corrida de integración se descartó: se recompiló con la corrida viva, que es justo lo que la regla del proyecto prohíbe. Los 92/92 son de una corrida limpia, sin tocar el árbol mientras corría.

### 3.12 La migración: EF la generó destructiva

`dotnet ef migrations add` escaffoldeó **`DropColumn` + `AddColumn`** —y avisó: *«An operation was scaffolded that may result in the loss of data»*—. Eso no convierte los datos: **los borra**. Cada representante existente habría perdido su fecha de nombramiento y habría quedado con `effective_from = 0001-01-01`.

Reescrita a mano como `RENAME` + `ALTER COLUMN … TYPE date USING (col AT TIME ZONE 'UTC')::date`.

**Y la segunda trampa se midió, no se supuso.** Contra una base de prueba con tres filas y la sesión en `America/El_Salvador`:

| Fila | Valor real | Cast desnudo (lo que genera EF) | Anclado con `AT TIME ZONE 'UTC'` |
|---|---|---|---|
| 1 | `2026-12-01` | ❌ `2026-11-30` | ✅ `2026-12-01` |
| 2 | `2026-01-01` | ❌ **`2025-12-31`** — cruza el año | ✅ `2026-01-01` |
| 3 | `2024-02-29` | ❌ `2024-02-28` — pierde el bisiesto | ✅ `2024-02-29` |

**3 de 3 filas corruptas** con el cast desnudo. La versión escrita a mano conserva las tres, con `effective_from` manteniendo su `NOT NULL`, y el `Down` devuelve exactamente medianoche UTC.

> La memoria del proyecto registraba esta trampa como «corre las fechas un día». **Es peor que eso**: cuando la fecha es un 1 de enero el desplazamiento cambia el **año**, y sobre un 29 de febrero destruye una fecha que no existe en el año anterior.

### 3.14 Un defecto preexistente que salió al pasar por aquí

Al revisar la cadena del export apareció que `ReportExportFileWriter.FormatValue` **no tenía caso para `DateOnly`**: caía en la rama genérica `IFormattable`, que en cultura invariante produce **`08/15/2026`**.

Medido antes de tocarlo, con un test que lo fija:

```
EffectiveFrom,EffectiveTo
08/15/2026,12/01/2026        ← ambiguo al abrir el CSV, y distinto de lo que devuelve el endpoint
```

**No lo introdujo B-02**: `DateOnly` ya se usaba en los export rows de planilla, tiempos no trabajados y vacaciones, así que esos exports vienen emitiendo `MM/dd/yyyy` desde antes. Lo que hacía B-02 era **extenderlo** a los representantes legales.

Corregido con un caso explícito que emite ISO, alineado con el contrato de la API, y fijado por `WriteAsync_WhenRowHasADayField_ShouldExportItAsIsoDate` (rojo verificado antes del arreglo).

> **Se arregla aquí y no se levanta como hallazgo aparte** porque §3.7 ya declaraba el export dentro del alcance de B-02. Pero conviene saber que **el beneficio es más ancho que esta entidad**: arregla también los exports de planilla y vacaciones, que nadie había reportado.

### 3.13 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Detectado al medir por qué el asistente precargaba el día siguiente. El síntoma es del frontend; **la causa de fondo es el tipo del contrato**. El conteo de cuántas entidades más comparten el patrón queda con montaje definido (§3.8) |
| 2026-08-15 | 🔲 Propuesto | **Revisión.** Renumerado `B-01`→`B-02`. **Montaje de §3.8 ejecutado**: 164 propiedades, 82 instantes correctos, 82 candidatos, ≈68 abiertos tras descontar las decisiones de H-26. **§3.4 corregido**: el impacto estaba sobreestimado — la ventana son 6 horas al día, no el día entero. **§3.5 añadido**: se explicita qué conclusión de H-26 completa este hallazgo. **§3.6: mitigación descartada** en vez de ofrecida |
| 2026-08-16 | 🟢 Resuelto | Ejecutado completo (§3.11). **EF generó la migración destructiva y hubo que reescribirla**; el cast desnudo corrompía 3/3 filas de la prueba, una de ellas cruzando el año (§3.12). *Allow-list* del guardrail 82 → 79. De paso salió un defecto **preexistente** del export (`DateOnly` → `08/15/2026`), corregido: beneficia también a planilla y vacaciones (§3.14) |

---

## 4. B-03 — La regla de tipos de fecha no está escrita en las definiciones técnicas

| | |
|---|---|
| **Severidad** | 🟡 Media |
| **Estado** | 🟢 **Resuelto** — 2026-08-15 (§4.8) |
| **Componente** | `docs/technical/definiciones-tecnicas-backend.md` · Guardrails (§9) |
| **Origen** | Causa raíz común de B-01 y B-02 |
| **Alcance** | **Transversal** — gobierna todo el dominio |

### 4.1 Evidencia

La regla día↔instante se fijó el 2026-08-12 al cerrar H-26/H-28 y se aplicó a ~40 sitios. **No está en el catálogo de definiciones técnicas:**

```bash
$ grep -n -i "DateOnly\|timestamptz\|fecha" docs/technical/definiciones-tecnicas-backend.md
$ # (sin resultados)
```

Ni §1.2 «Tipos» ni §3 «Persistencia» la mencionan. Vive únicamente en comentarios de código (`UtcDateTimeJsonConverter`, `LenientDateOnlyJsonConverter`, `ApiIntegrationTests.DateBoundary.cs`) y en la memoria de sesión.

Y **no hay guardrail**: §9 enumera dieciséis fitness functions en CI —AUTHZ, RATE LIMIT, OPENAPI, PAGINACIÓN, CONCURRENCY TOKEN, PUBLIC ID EN EL WIRE…— y ninguna vigila el tipo de las fechas.

### 4.2 Causa

Una decisión que solo vive en el código que ya la cumple no gobierna el código que se escribe después. `LegalRepresentative` se escribió sin infringir nada visible: no había regla que leer ni test que fallara.

La consecuencia se ve **dentro de una sola entidad**: `SetEffectiveDates` trunca con `.Date` (`LegalRepresentative.cs:191-192`) y `AppointmentDateUtc` se asigna crudo (`:35`, `:136`). Tres campos del mismo tipo semántico, dos tratamientos, en cuarenta líneas de distancia. Eso no es descuido: es ausencia de regla.

### 4.3 Impacto

**Es el hallazgo que impide que los otros dos vuelvan.** Sin él, B-01 y B-02 son limpieza que se vuelve a ensuciar con la próxima entidad: quedan ≈68 campos por convertir (B-02 §3.8) y cada módulo nuevo puede añadir más sin que nada lo note.

Hoy nadie lo sufre directamente. Se levanta bajo el criterio del README §1: *«es una mejora que reduce deuda transversal, aunque hoy nadie la esté sufriendo»*.

### 4.4 Propuesta

**a) Escribir la regla** en `definiciones-tecnicas-backend.md`, en §1.2 (Tipos) y §3 (Persistencia):

| Lo que dice el negocio | Tipo en dominio | Columna |
|---|---|---|
| «el **día** en que pasó» | `DateOnly` | `date` |
| «el **momento** en que pasó» | `DateTime` (UTC) | `timestamptz` |

Con las dos trampas ya medidas: la conversión de migración anclada con `AT TIME ZONE 'UTC'`, y el `Kind` crudo de `JsonElement` en los *patch appliers*.

**b) Añadir un guardrail** siguiendo la receta obligatoria de §9 —reflexión sobre el ensamblado, filtro de familia **por regex** (no lista a mano), agregación de todas las violaciones en **un solo `Assert`**, y **centinela zero-match**—:

- reflexión sobre `CLARIHR.Domain`;
- regex de familia sobre nombres que denotan día (`*Date`, `EffectiveFrom`/`EffectiveTo`, `*StartDate`, `*EndDate`);
- *allow-list* explícita con las excepciones que H-26 decidió dejar como instante, cada una con su motivo;
- rojo→verde del regex ampliado antes del PR, como exige §9.

### 4.5 Compatibilidad

✅ **No toca el contrato ni el esquema.** Es documentación más un test. El guardrail nacerá **rojo** con los ≈68 campos pendientes: hay que arrancarlo con la *allow-list* poblada con el inventario actual y vaciarla a medida que avancen las conversiones, o no se podrá mergear nada hasta terminar B-02 entero.

### 4.6 Alcance a revisar

El inventario ya está hecho en B-02 §3.8 y es la semilla de la *allow-list*. Lo que falta decidir, campo por campo entre los ≈68, es cuáles son día y cuáles instante — el nombre lo sugiere pero no lo prueba.

### 4.7 Vía alterna vigente

Ninguna. Mientras no exista, la única defensa es que alguien recuerde la regla en la revisión de código.

### 4.8 Lo que se hizo

**a) La regla, escrita** en `docs/technical/definiciones-tecnicas-backend.md`:

| Sección | Qué se añadió |
|---|---|
| **§1.2 Tipos** | La tabla día↔instante; que el sufijo `Utc` es para instantes y en un campo de día delata el error; que guardar un día como instante lo corre; y la técnica de conversión (borrar el tipo y dejar que el compilador enumere) |
| **§3 Persistencia** | La columna según el tipo, más **dos trampas medidas**: la migración `timestamptz → date` sin `AT TIME ZONE 'UTC'` retrocede las fechas un día, y un `timestamptz` leído como fecha usa el `TimeZone` de la sesión |
| **§4.5 Verbos y formatos** | **Que la frontera tiene TRES puertas**, con la tabla de quién normaliza cada una — y que `JsonElement.TryGetDateTime()` no es la frontera. Es lo que faltaba escrito y produjo B-01 |
| **§9 Guardrails** | La fila **TIPO DE FECHA**, con la advertencia de que vaciar la *allow-list* es el avance |

**b) El guardrail**, en `tests/CLARIHR.Application.UnitTests/CalendarDateTypeGuardrailsTests.cs`, siguiendo la receta obligatoria de §9 — reflexión, familia por regex, un solo `Assert`, centinela zero-match:

| Test | Qué fija |
|---|---|
| `DomainDayFields_ShouldBeDateOnly_NotDateTime` | Toda propiedad de dominio con nombre de día es `DateOnly`, salvo *allow-list*. Centinela: el regex debe seguir matcheando ≥80 propiedades |
| `CalendarDateAllowList_ShouldNotContainStaleEntries` | La *allow-list* no puede pudrirse: una entrada que ya no existe es progreso sin registrar |
| `PatchAppliers_ShouldNotReadDatesWithRawTryGetDateTime` | Ningún lector de JSON Patch usa `TryGetDateTime` en crudo. Centinela: >100 archivos escaneados |

La *allow-list* nace con las **82** entradas del inventario, separadas en dos bloques: **15 marcadas `[H-26]`** (instantes deliberados, no son deuda) y **67 pendientes**, cada conversión de B-02 borra su línea.

> **El regex de familia se validó contra el inventario real**: matchea exactamente las 82 y ninguno de los 82 instantes, porque ningún nombre de instante termina en `Date`/`DateUtc`. Esa separación limpia es lo que permite que el filtro sea un regex y no una lista a mano.

**c) La incoherencia interna** que §4.2 señalaba —`AppointmentDateUtc` crudo frente a `SetEffectiveDates` truncado— quedó cerrada en el arreglo de B-01 (§2.8): las tres fechas de la entidad se normalizan igual.

### 4.9 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Levantado al preguntar por qué `LegalRepresentative` quedó fuera de una convención de 228 usos. La respuesta no es descuido: **la regla no estaba escrita en ninguna parte que se lea antes de escribir código** |
| 2026-08-15 | 🟢 Resuelto | Regla escrita en §1.2/§3/§4.5/§9 y guardrail en verde con los tres tests. **El guardrail nació rojo en su tercer caso** —los diez lectores de B-01— y quedó verde al arreglarlos, que es la comprobación de que no es decoración |

---

## 5. B-04 — El representante inicial puede quedar sin marcar como principal

| | |
|---|---|
| **Severidad** | 🔵 Baja |
| **Estado** | 🟢 **Resuelto** — 2026-08-16, verificado (§5.9) |
| **Componente** | `CLARIHR.Application` · `CompanyProvisioningService` |
| **Origen** | Pregunta abierta del espejo (**F-06**), ahora comprobada |
| **Alcance** | Un camino — el de provisioning |

> La primera redacción dejaba esto como pregunta abierta porque *«no se comprobó qué hace el handler con ese `null`»*. **Comprobado: no hace nada.** Es un hallazgo.

### 5.1 Evidencia

El servicio de provisioning pasa el valor **crudo**, sin default y sin lógica de exclusividad:

```csharp
// src/CLARIHR.Application/Features/Provisioning/CompanyProvisioningService.cs:89-104
var initialLegalRepresentative = request.InitialLegalRepresentative;
var legalRepresentative = LegalRepresentative.Create(
    …
    initialLegalRepresentative.IsPrimary);      // ← null si el cliente lo omitió
```

Contra la API dedicada, que en el mismo dominio hace lo contrario — `bool` **no nullable** y limpieza transaccional del primario anterior:

```csharp
// src/CLARIHR.Application/Features/LegalRepresentatives/LegalRepresentativeAdministration.cs:154
bool IsPrimary)                                  // no nullable

// …:672-683
if (command.IsPrimary)
{
    var currentPrimary = await repository.GetActivePrimaryAsync(…);
    if (currentPrimary is not null) { currentPrimary.ClearPrimary(); … }
}
```

Y el validador de la entrada inicial (`LegalRepresentativeCommon.cs:197-249`) no tiene ninguna regla sobre `IsPrimary`.

### 5.2 Causa

Dos caminos de escritura para la misma entidad, con contratos distintos para el mismo campo: `bool?` en provisioning, `bool` en administración. El `= null` del parámetro posicional no ayuda —el espejo ya anota que el valor por defecto de un `record` no se aplica cuando el campo falta en el JSON— pero aquí el default declarado **es** `null`, así que ni siquiera hay sorpresa: el contrato pide un tri-estado para un concepto binario.

### 5.3 Impacto

Una empresa recién creada puede quedar con su **único** representante legal en `is_primary = NULL`.

Hoy el daño es limitado porque el flag se usa solo como criterio de orden, en cuatro sitios:

```
LegalRepresentativeRepository.cs:105, :289, :331   →  OrderByDescending(x => x.IsPrimary == true)
CompanyRepository.cs:69                             →  OrderByDescending(x => x.IsPrimary == true)
```

Con un solo representante el orden no cambia nada. El problema es de integridad, no de síntoma: el índice único parcial `ux_legal_representatives__tenant_primary_active` ya trata el campo como binario (`WHERE is_primary = true and is_active = true`), y `Inactivate()` lo pone en `false`, nunca en `null`. El `null` es un tercer estado que nadie más reconoce.

### 5.4 Propuesta

**Que el servidor marque como principal al primer representante activo de la empresa.** Es la respuesta a la pregunta que dejó abierta el espejo, y la recomiendo por tres razones: el frontend no expone el campo (F-06), el dominio ya garantiza exclusividad, y una empresa sin representante principal no es un estado que el negocio quiera.

Concretamente:

- `IsPrimary` → `bool` no nullable de punta a punta, alineando provisioning con la API dedicada;
- limpieza destructiva de los `NULL` existentes (permitida: no hay producción);
- guardrail: toda empresa con ≥1 representante activo tiene exactamente uno primario.

**Decisión de negocio pendiente**, no técnica: si se prefiere que la empresa pueda no tener principal, entonces el arreglo es el opuesto —quitar el tri-estado dejando `bool` con default `false`— y sigue cerrando el hallazgo.

### 5.5 Compatibilidad

⚠️ **Cambio menor de contrato.** `isPrimary` pasa de `bool?` a `bool` en `InitialLegalRepresentativeRequest`; un cliente que hoy manda `null` explícito recibiría `400`. El frontend **no lo manda** (F-06: el campo está ausente del formulario), así que el impacto real es nulo. Requiere regenerar `openapi.yaml`.

### 5.6 Alcance a revisar

Revisar si el mismo desdoble provisioning-vs-administración existe en otros campos de `InitialLegalRepresentativeInput`: es el mismo *record* duplicado en dos contratos, y `IsPrimary` puede no ser el único que divergió.

### 5.7 Vía alterna vigente

El frontend puede enviar `isPrimary: true` explícito en el cuerpo de creación. Funciona hoy y no depende de este hallazgo — pero le traslada al cliente una regla que es del servidor.

### 5.8 Decisión de negocio

**Tomada el 2026-08-16:** el servidor marca como principal al primer representante. Se ejecutó la opción recomendada en §5.4, no la alterna.

### 5.9 Lo que se hizo, y cómo se comprobó

**El hallazgo estaba corto: eran tres agujeros del mismo invariante, no uno.** Al escribir los rojos aparecieron dos caminos más que dejan a una empresa sin ningún principal:

| # | Camino | ¿Estaba en el hallazgo? |
|---|---|---|
| 1 | **Provisioning** omite el flag → el único representante nace con `NULL` | ✅ Sí, es el reportado |
| 2 | **`Create`** del primero con `isPrimary: false` → nadie lo promueve. El handler sabía *degradar* al anterior, nunca *promover* | ❌ No |
| 3 | **`Inactivate`** del principal con otros activos → `Inactivate()` limpia el flag y **nadie hereda** | ❌ No |

El rojo del tercero fue el más elocuente: `Assert.Single() Failure: The collection was empty` — la empresa quedaba literalmente sin principal.

**El cambio:**

| Capa | Qué cambió |
|---|---|
| Dominio | `bool? IsPrimary` → **`bool`**. Desaparece el tri-estado que ningún otro punto reconocía |
| `InitialLegalRepresentativeInput` | **`IsPrimary` eliminado del contrato.** Es el único representante de la empresa: sólo hay un valor válido, y un campo con un solo valor válido es ruido. La garantía pasa a ser del servidor |
| `CompanyProvisioningService` | Crea con `isPrimary: true` |
| `CreateLegalRepresentativeCommandHandler` | Si no hay principal activo, el que entra lo es — lo haya pedido o no |
| `InactivateLegalRepresentativeCommandHandler` | Promueve al **activo más antiguo** (`GetPromotionCandidateAsync`, orden determinista con desempate por `Id`) |
| Respuestas | `isPrimary` deja de ser anulable en 4 DTOs. ⚠️ Los **filtros** de `search`/`export` siguen siendo `bool?`: ahí `null` significa «sin filtrar», no «sin valor» |
| Esquema | Migración `20260816143505_LegalRepresentativePrimaryNotNull` |

**Verificación.** Rojo antes que verde en los cinco tests, incluidos **dos que afirmaban lo contrario y estaban fijando el defecto**: `…ShouldPersistNullPrimaryFlag` exigía que el representante naciera con `NULL`. Se invirtieron y se renombraron.

Se añadió además el guardrail que pedía §5.4, pero como **recorrido del ciclo de vida** en lugar de comprobación estática: crear → crear → promover → dar de baja → reactivar, verificando tras **cada paso** que hay exactamente un principal activo. Las combinaciones eran donde se escondía el defecto; los caminos por separado ya funcionaban.

| Suite | Resultado |
|---|---|
| Unit (`CLARIHR.Application.UnitTests`) | **2968 / 2968** |
| Integración dirigida (representantes legales · provisioning · account-companies · fechas · exports) | **103 / 103** |
| Build | 0 errores · 0 warnings (con `TreatWarningsAsErrors`) |
| Modelo EF | Sin drift |

### 5.10 La trampa: el fixture hacía falsa la premisa

Los tres tests nuevos fallaron **después** del arreglo, y no por el código: `IntegrationTestSeeder` ya siembra un representante **principal** en el tenant del escenario. Es decir, «el primero» nunca lo era, y la promoción elegía al sembrado por ser el más antiguo.

> Es exactamente la **quinta pregunta** de la regla rojo-antes-de-verde: *¿el fixture acopla lo que la regla distingue?* Aquí acoplaba «empresa nueva» con «empresa que ya tiene principal». Se resolvió con `ResetWithoutLegalRepresentativesAsync()`, que arranca de una empresa vacía para que la premisa sea cierta **y** para que se sepa a quién debe tocarle el puesto.

### 5.11 El backfill que EF habría hecho mal

EF escaffoldeó `AlterColumn(nullable: false, defaultValue: false)`: convierte todos los `NULL` en `false`. Eso deja a las empresas cuyo único representante tenía `NULL` **sin ningún principal** — el estado exacto que este hallazgo elimina. La columna habría quedado conforme y el negocio violado.

Reescrito a mano en tres pasos y medido contra cuatro formas de empresa:

| Empresa de prueba | Antes | Después |
|---|---|---|
| **A** — única representante, `NULL` | sin principal | ✅ promovida |
| **B** — dos activas, ninguna principal (`NULL` + `false`) | sin principal | ✅ promovida **la más antigua**; la reciente queda `false` |
| **C** — ya tiene principal | correcta | ✅ intacta; la otra pasa a `false` |
| **D** — sólo inactivas | sin principal | ✅ **no se inventa** un principal |

La consulta del invariante —empresas con activos y distinto de un principal— devuelve **0 filas**.

### 5.12 Bitácora

| Fecha | Estado | Nota |
|---|---|---|
| 2026-08-15 | 🔲 Propuesto | Promovido de «pregunta abierta» a hallazgo tras comprobar el handler, que era exactamente lo que la primera redacción no había hecho. **El servicio de provisioning no aplica default ni exclusividad**, a diferencia de la API dedicada del mismo dominio |
| 2026-08-16 | 🟢 Resuelto | Decisión de negocio tomada (§5.8) y ejecutado (§5.9). **El hallazgo estaba corto: eran tres agujeros, no uno** — `Create` sin promoción y `Inactivate` sin sucesor no estaban reportados. Dos tests existentes **fijaban el defecto** y se invirtieron. El fixture hacía falsa la premisa de los rojos (§5.10) y EF habría hecho mal el backfill (§5.11) |
