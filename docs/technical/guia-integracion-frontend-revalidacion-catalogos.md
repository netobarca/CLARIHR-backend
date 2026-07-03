# Guía de integración Frontend — Revalidación de Catálogos del Expediente

| | |
|---|---|
| **Audiencia** | Equipo frontend |
| **Documentos base** | [`analisis-revalidacion-catalogos.md`](../business/analisis-revalidacion-catalogos.md) (v2.1) · [`plan-tecnico-revalidacion-catalogos.md`](plan-tecnico-revalidacion-catalogos.md) |
| **Alcance** | Fases 0–4 completas (17 PRs): 14 catálogos del expediente creados/ampliados/estandarizados, todos con seed inicial (SV) |
| **Fecha** | 2026-07-02 |
| **Convenciones** | Prefijo `api/v1` · errores de negocio **422** con `code` en el body · concurrencia por header `If-Match` (faltante→400, obsoleto→409) · enums como strings |

---

## 0. Resumen ejecutivo

Se cerró el entregable de catálogos del expediente. Para el frontend hay **3 tipos de cambio**:

1. **Catálogos nuevos para combos** (todos ya sembrados en todos los ambientes — no hay listas vacías):
   títulos personales, tipos de direcciones, hobbies, asociaciones, tipos de beneficios, niveles educativos y el maestro AFP.
2. **Campos nuevos** en persona (`personalTitleCode`, `afpCode`) y dirección (`addressTypeCode`), y columnas enriquecidas en contratos, tipos de estudio, carreras y rubros salariales.
3. **Cambios BREAKING** (ratificados, drop & recreate): hobbies y asociaciones ahora exigen **código de catálogo**; el parentesco del contacto de emergencia, el tipo de beneficio y el número de documento ahora **se validan** (422); las **carreras cambiaron de códigos y de `publicId`** y ahora requieren `countryCode` en la lectura.

> Regla transversal: cualquier `xxxCode` que se envíe y no exista **activo** en su catálogo produce **422** con `errors.<campo>` (validación) — antes varios de estos campos aceptaban texto libre.

---

## 1. Catálogos nuevos — endpoints de lectura para combos

### 1.1. Vía `GET /api/v1/general-catalogs/{key}?countryCode=SV`

| Key | Contenido sembrado (SV) | Consumido por |
|---|---|---|
| `hobbies` | DEPORTE, LECTURA, MUSICA, CINE, VIAJES, COCINA, ARTE, TECNOLOGIA, FOTOGRAFIA, JARDINERIA, VOLUNTARIADO, OTRO | `hobbyCode` (hobbies del expediente) |
| `associations` | SINDICATO, COLEGIO_PROF, CAMARA, ONG, CLUB, RELIGIOSA, COOPERATIVA, OTRA | `associationCode` (tipo de asociación) |
| `additional-benefit-types` | SEGURO_VIDA, SEGURO_MEDICO, BONO_ALIMENTACION, VALE_DESPENSA, AYUDA_TRANSPORTE, GIMNASIO, BECA_CAPACITACION, PLAN_TELEFONO, VEHICULO, OTRO | `benefitTypeCode` (beneficios adicionales) |
| `education-levels` | BASICO, MEDIO, TECNICO, SUPERIOR, POSGRADO | informativo (nivel del tipo de estudio) — catálogo **global**, ignora `countryCode` |
| `afps` | CONFIA, CRECER, OTRA | `afpCode` (afiliación de la persona) — versión thin code/name; la enriquecida está en §1.3 |

Respuesta estándar (thin): `[{ id, category, code, name, isSystem, isActive, sortOrder }]`.

### 1.2. Vía `GET /api/v1/reference-catalogs/{key}?countryCode=SV`

| Key | Contenido sembrado (SV) | Consumido por |
|---|---|---|
| `personal-titles` | ING, LIC, ARQ, DR, DRA, MSC, TEC, PROF, SR, SRA, SRTA, OTRO | `personalTitleCode` (persona) |
| `address-types` | CASA, TRABAJO, FACTURACION, TEMPORAL, OTRA | `addressTypeCode` (dirección) |

Respuesta estándar: `[{ id, code, name, sortOrder }]`.

### 1.3. Endpoints dedicados (catálogos enriquecidos)

Los DTOs genéricos solo llevan code/name; estos endpoints exponen las columnas extra:

**`GET /api/v1/afps?countryCode=SV`** (authn-only)
```json
[
  {
    "id": "…", "code": "CONFIA", "name": "AFP Confía",
    "abbreviation": "CONFIA",
    "address": null, "phone": null, "fax": null, "contactName": null,
    "isActive": true, "sortOrder": 10
  }
]
```
> `address/phone/fax/contactName` se entregan `null` en el seed inicial y se completarán por administración (DP-03). Muestren el dato solo si viene.

**`GET /api/v1/contract-types?countryCode=SV`** (authn-only)
```json
[
  { "id": "…", "code": "PLAZO_FIJO", "name": "Contrato a plazo fijo",
    "abbreviation": "PF", "isTemporary": true, "isActive": true, "sortOrder": 20 }
]
```
> `isTemporary=true` para PLAZO_FIJO, POR_OBRA, EVENTUAL, APRENDIZAJE y TEMPORAL. Úsenlo para condicionar la fecha de fin de contrato en el formulario de historial. El key genérico `general-catalogs/contract-types` sigue disponible para combos simples.

**`GET /api/v1/compensation-concept-types?countryCode=SV`** — el response ganó 3 campos:
- `isBaseSalary` (bool): `true` solo en `SALARIO_BASE`. La regla de "un solo salario base activo por plaza" ahora se basa en este flag (el código deja de ser mágico).
- `defaultPensionedEmployerRate` (decimal?): fila `AFP` = **8.75** (LISP 2022 — tasa patronal para pensionado que sigue trabajando; igual a la ordinaria).
- `minContributionBase` (decimal?): filas `AFP`/`ISSS` = **365.00** (IBC mínimo = salario mínimo vigente, default editable). El `contributionCap` de la fila `AFP` ahora trae **7045.06** (IBC máximo 2026).

---

## 2. Campos nuevos en la persona (expediente)

### 2.1. `personalTitleCode` (título personal / tratamiento — opcional)

- **Escritura:** `POST /api/v1/companies/{companyId}/personnel-files` y `PUT /api/v1/personnel-files/{publicId}` aceptan `personalTitleCode`; `PATCH` acepta `{"op":"replace","path":"/personalTitleCode","value":"ING"}` (y `remove`).
- **Lectura:** `personalTitleCode` + `personalTitleName` (nombre resuelto server-side) en el response completo, el personal-info y el **listado**.
- **Validación:** código inexistente/inactivo → 422 con `errors.personalTitleCode`.

### 2.2. `afpCode` (afiliación AFP — opcional, a nivel persona)

- Mismos endpoints y patch path `/afpCode`. La afiliación es **de la persona** (cuenta vitalicia), no de la plaza ni del período laboral.
- **Lectura:** `afpCode` en response completo, personal-info y listado. **No** se resuelve `afpName` server-side: resuelvan el nombre con el catálogo de §1.3 (que ya necesitan para el combo).
- **Validación:** 422 `errors.afpCode` si no es un código activo del catálogo AFP.

---

## 3. Direcciones — `addressTypeCode` (opcional)

- `AddAddressRequest`/`UpdateAddressRequest`/`PatchAddressRequest` y el response de dirección ganan `addressTypeCode` (nullable). Patch path: `/addressTypeCode`.
- Validado contra `reference-catalogs/address-types` cuando viene informado → 422 `errors.addressTypeCode` si es inválido.
- Direcciones existentes quedan con `addressTypeCode = null` (no hubo backfill; el tipo es opcional por D-03).

---

## 4. Cambios BREAKING en sub-entidades

### 4.1. Hobbies — contrato NUEVO

| Antes | Ahora |
|---|---|
| `{ "hobbyName": "Leer" }` (texto libre requerido) | `{ "hobbyCode": "LECTURA", "hobbyName": "Novela histórica" }` — **`hobbyCode` requerido** (catálogo `hobbies`), `hobbyName` pasa a **etiqueta libre opcional** |

- Response: `{ hobbyPublicId, hobbyCode, hobbyName (nullable), concurrencyToken }`.
- Patch paths: `/hobbyCode` (requerido — remove lo rechaza la validación) y `/hobbyName` (removible).
- **Datos:** los hobbies de texto libre existentes **se eliminaron** en la migración (RT-06, sin backfill). Las pantallas parten vacías.

### 4.2. Asociaciones — `associationCode` requerido

- `associationCode` = **TIPO** de asociación (catálogo `associations`); `associationName` sigue siendo el nombre específico de la organización (texto libre requerido). `role/joinedDate/leftDate/payment` sin cambios.
- Requests/response/patch ganan `associationCode` (patch path `/associationCode`).
- **Datos:** las asociaciones existentes **se eliminaron** en la migración (RT-06, sin backfill).

### 4.3. Beneficios adicionales — `benefitTypeCode` ahora validado

- El contrato NO cambia de forma (el campo ya existía), pero ahora **se valida** contra `additional-benefit-types` en Add/Update/Patch → 422 `errors.benefitTypeCode` si no es un código activo.
- **Datos:** filas cuyo código libre no coincidía con el catálogo sembrado fueron eliminadas por la migración.

### 4.4. Contacto de emergencia — `relationship` validado contra Parentesco

- `relationship` ahora debe ser un **código activo del catálogo Parentesco** (`reference-catalogs/kinships`: CONYUGE, PAREJA, PADRE, MADRE, HIJO_A, HERMANO_A, ABUELO_A, NIETO_A, TIO_A, OTRO) → 422 `errors.relationship` si no lo es. Cambien el input libre por un combo de kinships.
- **Datos:** contactos con parentesco no conforme fueron eliminados por la migración (RT-06).

### 4.5. Número de documento — validación de FORMATO por tipo (nuevo 422)

Al crear/editar identificaciones, si el tipo tiene formato configurado y el número no lo cumple:

```json
{ "status": 422, "code": "PERSONNEL_FILE_IDENTIFICATION_NUMBER_FORMAT_INVALID" }
```

Patrones sembrados (SV, anclados, editables):

| Tipo | Regex | Ejemplo válido |
|---|---|---|
| `DUI` | `^\d{8}-\d$` | `01234567-8` |
| `NIT` | `^\d{4}-\d{6}-\d{3}-\d$` | `0614-123456-101-2` |
| `PASSPORT` | `^[A-Z0-9]{6,12}$` | `A1234567` |
| `RESIDENT_CARD` | `^[A-Za-z0-9-]{5,20}$` | (variable) |

> El match se hace sobre el número **trim + mayúsculas**. Tipos sin patrón conservan solo la validación genérica. Recomendado replicar la máscara en el input (el backend es la fuente de verdad y bloquea).

---

## 5. Educación — catálogos reestructurados (BREAKING)

### 5.1. Tipos de estudio (`general-catalogs/education-study-types`) — códigos RENOMBRADOS

| Código anterior | Código nuevo | Abreviatura | Nivel educativo |
|---|---|---|---|
| `BACHELOR` | `UNIVERSITARIA` | UNIV | SUPERIOR |
| `MASTER` | `POSGRADO` | POSG | POSGRADO |
| `TECHNICAL` | `TECNICO` | TEC | TECNICO |
| — (nuevo) | `BASICA` | BAS | BASICO |
| — (nuevo) | `BACHILLERATO` | BACH | MEDIO |

Los `publicId` de las 3 filas renombradas **cambiaron** (derivan del código). Las educaciones existentes conservan su vínculo (rename in-place por id interno). El DTO genérico sigue siendo thin; abreviatura/nivel son datos de seed (DP-03).

### 5.2. Carreras (`general-catalogs/education-careers`) — ahora COUNTRY-SCOPED + enriquecidas

- **La lectura ahora requiere `countryCode`**: `GET /api/v1/general-catalogs/education-careers?countryCode=SV`. Sin país (o país desconocido) devuelve `[]`.
- **Códigos y publicIds NUEVOS** (drop & recreate ratificado RT-02):

| Código | Nombre | Abrev. | Tipo de estudio |
|---|---|---|---|
| `ING_INDUSTRIAL` | Ingeniería Industrial | II | UNIVERSITARIA |
| `ING_SISTEMAS` | Ingeniería en Sistemas/Computación | IS | UNIVERSITARIA |
| `LIC_ADMIN` | Lic. Administración de Empresas | LAE | UNIVERSITARIA |
| `LIC_CONTADURIA` | Lic. Contaduría Pública | LCP | UNIVERSITARIA |
| `LIC_PSICOLOGIA` | Lic. Psicología | LP | UNIVERSITARIA |
| `LIC_DERECHO` | Lic. Ciencias Jurídicas | LCJ | UNIVERSITARIA |
| `TEC_COMPUTACION` | Técnico en Computación | TC | TECNICO |
| `MBA` | Maestría en Administración (MBA) | MBA | POSGRADO |
| `OTRA` | Otra carrera | OTRA | UNIVERSITARIA |

- Cada carrera lleva además `increment` (decimal % 0–100, sembrado 0 — % de incremento salarial por grado, lo consumirá Nómina) e `isRecognized` (bool). Estos extras son de seed; el DTO genérico sigue thin.
- El **flujo de educación del expediente no cambia de contrato**: se siguen enviando `studyTypePublicId` y `careerPublicId` (Guids). Solo deben refrescar los ids desde los catálogos (los viejos ya no existen).

### 5.3. Backoffice educación

- `api/platform/education-catalogs/levels` — nuevo key con CRUD completo para Nivel educativo.
- `api/platform/education-catalogs/careers` — **RETIRADO** (404): la administración de carreras es solo por seed en esta fase (country-scoped + columnas extra no caben en el contrato plano; DP-03/DP-06). La lectura para combos sigue en `general-catalogs/education-careers`.

---

## 6. Backoffice — nuevos keys en `api/platform/system-catalogs/{key}` (CRUD thin)

`personal-titles`, `address-types`, `hobbies`, `associations`, `additional-benefit-types` — todos con el CRUD genérico existente (code/name/sortOrder/activate/inactivate, país por query). Los catálogos **enriquecidos** (AFP, contratos, rubros, tipos de documento, carreras) solo administran code/name por esta vía; sus columnas extra son de seed (RT-01/DP-03).

---

## 7. Formas de pago y rubros (cambios menores)

- `general-catalogs/payment-methods` ganó **`BOLETA`** ("Boleta de pago").
- `general-catalogs/compensation-concept-types` / endpoint dedicado: ver §1.3 (`isBaseSalary` + parámetros de pensión). Sin filas nuevas; `SALARIO_BASE` marcado.

---

## 8. Errores nuevos / códigos a manejar

| Situación | HTTP | Identificación |
|---|---|---|
| Código de catálogo inválido/inactivo (título, AFP, dirección, hobby, asociación, beneficio, parentesco de emergencia) | 422 | `errors.<campo>` (p.ej. `errors.hobbyCode`) con mensaje "Catalog code 'X' is not active…" |
| Número de documento no cumple el formato del tipo | 422 | `code = PERSONNEL_FILE_IDENTIFICATION_NUMBER_FORMAT_INVALID` |
| Formato sintáctico inválido de un código nuevo (`personalTitleCode`, `afpCode`, `addressTypeCode`, `hobbyCode`, `associationCode`) | 400 | `errors.<campo>` ("… format is invalid") |

---

## 9. Checklist de adopción FE

1. Combos nuevos: cargar `personal-titles`, `address-types`, `hobbies`, `associations`, `additional-benefit-types`, `afps` (dedicado), `education-levels`.
2. Persona: agregar `personalTitleCode` y `afpCode` a create/edit/patch + mostrar `personalTitleName`/nombre de AFP.
3. Dirección: agregar combo `addressTypeCode` (opcional).
4. Hobbies/Asociaciones: migrar formularios al nuevo contrato con código requerido (los datos viejos ya no existen).
5. Beneficios: convertir `benefitTypeCode` de input libre a combo.
6. Contacto de emergencia: convertir `relationship` a combo de kinships.
7. Identificaciones: aplicar máscaras DUI/NIT/pasaporte y manejar el 422 de formato.
8. Educación: refrescar los ids/códigos de tipos de estudio y carreras; agregar `countryCode=SV` a la lectura de carreras.
9. Contratos: usar `GET /api/v1/contract-types` para `abbreviation`/`isTemporary`.
10. Rubros: leer `isBaseSalary` en lugar de comparar contra el string `SALARIO_BASE`.

---

> **Estado backend:** build limpio, 2065 tests unitarios verdes, 8 migraciones (`20260702032449` → `20260702044855`) aplicadas y verificadas contra PostgreSQL real con todos los seeds presentes. Valores legales AFP (LISP 2022) sembrados como defaults editables.
