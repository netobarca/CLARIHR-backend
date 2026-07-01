# Respuesta backend — Bug `purpose=CompanyLogo` sin reglas configuradas (subsistema de archivos)

| | |
| --- | --- |
| **Para** | Equipo Frontend |
| **De** | Equipo Backend (.NET API) |
| **Endpoint** | `POST /api/v1/files/upload-session` |
| **Código** | `files.purpose_not_configured` (HTTP 422) |
| **Ref. reporte** | "Bug Backend — `purpose=CompanyLogo` sin reglas configuradas" (2026-06-29, traceId `40001ddd-…7967bb`) |
| **Fecha** | 2026-06-30 |
| **Estado** | ✅ **Resuelto en backend** (config del purpose). ⚠️ **1 tarea de infraestructura**: provisionar el contenedor en cada entorno (ver §4). **Sin cambios en el FE.** |

---

## TL;DR

Tenían razón, y el diagnóstico fue exacto: **es el mismo patrón de "el contrato referencia algo que la migración/seed no creó"**, pero aplicado al subsistema de archivos.

El purpose `CompanyLogo` **sí existe** en el enum del backend (`FilePurpose.CompanyLogo`) y **todo el código que lo consume ya estaba escrito** (validación del logo en la configuración de constancias, construcción de la ruta de blob, incrustación en el PDF). Lo único que faltaba eran las **reglas de validación de subida** (`Storage:Purposes`) para ese purpose — el diccionario de configuración tenía los otros 7 purposes (`ProfileImage`, `PersonnelDocument`, `ReportExport`, `MedicalClaimDocument`, `OffPayrollTransactionDocument`, `EconomicAidRequestDocument`, `CertificateRequestDocument`) pero **no** `CompanyLogo`. Sin reglas, el proveedor devolvía `null` → `422 files.purpose_not_configured`.

**Ya registramos las reglas de `CompanyLogo`.** En cuanto se despliegue, `upload-session` con `purpose: "CompanyLogo"` funcionará sin tocar el FE.

---

## 1. Causa raíz

El handler de `upload-session` resuelve las reglas del purpose así:

```csharp
var rule = purposeRuleProvider.GetRule(purpose);   // lee Storage:Purposes[<purpose>]
if (rule is null)
    return Failure(FileErrors.PurposeNotConfigured); // ← 422 que vieron
```

`GetRule` busca la clave `"CompanyLogo"` en la sección `Storage:Purposes` de `appsettings.json`. Esa clave **no existía**, así que devolvía `null`. El resto del expediente ya estaba listo:

- `FilePurpose.CompanyLogo` está en el enum de dominio.
- El constructor de rutas de blob ya mapea `CompanyLogo → .../logos/...`.
- Al guardar la configuración de constancias, el backend ya valida que el logo sea un archivo **activo**, del **tenant** y con **purpose = CompanyLogo**.
- El renderer del PDF ya incrusta el logo (`QuestPDF .Image(logoBytes)`).

Era, en efecto, un purpose "declarado en el contrato pero no aprovisionado en la config". Corregido.

---

## 2. Qué cambiamos

Un solo archivo: **`src/CLARIHR.Api/appsettings.json`**, sección `Storage:Purposes`, nuevo bloque:

```jsonc
"CompanyLogo": {
  "MaxSizeBytes": 5242880,                                   // 5 MB
  "AllowedContentTypes": [ "image/png", "image/jpeg", "image/webp" ],
  "AllowedExtensions":   [ ".png", ".jpg", ".jpeg", ".webp" ],
  "DefaultProvider": "AzureBlob",
  "RequiresMalwareScan": false,
  "ContainerOverride": "clarihr-company-logos"
}
```

**Por qué esos content-types (y por qué NO `image/svg+xml`):** el logo se incrusta en el PDF de la constancia como **imagen ráster** vía `QuestPDF .Image(bytes)` (backend SkiaSharp), que decodifica PNG/JPEG/WebP pero **no** SVG. Admitir SVG dejaría pasar la subida pero **rompería la generación del PDF**. Si en el futuro necesitan SVG, requiere trabajo adicional en el renderer (ruta `.Svg(string)`, no la de imagen). Los tipos elegidos son los mismos que `ProfileImage`.

- **Límite de tamaño:** 5 MB (idéntico a `ProfileImage`; su PNG de ~10 KB entra de sobra).
- **Contenedor:** `clarihr-company-logos` (dedicado, siguiendo el patrón `clarihr-profile-images`, `clarihr-personnel-documents`, etc.).

---

## 3. Aplica a todos los entornos (dev/staging/prod) — sin tocar `appsettings.Development.json`

La configuración de `Storage:Purposes` **se fusiona por clave** entre `appsettings.json` (base) y `appsettings.{Environment}.json`. Prueba empírica: `MedicalClaimDocument` y `CertificateRequestDocument` están definidos **solo en base** (no en `appsettings.Development.json`, que únicamente lista `ProfileImage`/`PersonnelDocument`/`ReportExport`) y aun así funcionan en dev — que es justo lo que ustedes reportaron.

Por eso basta con haberlo agregado en **base**: `CompanyLogo` queda disponible en dev, staging y prod. Verificado también en el artefacto compilado (`bin/.../appsettings.json` ya lo incluye).

---

## 4. ⚠️ Tarea de infraestructura: provisionar el contenedor `clarihr-company-logos`

Esto **no** es código, es aprovisionamiento de la cuenta de almacenamiento, y aplica **por igual a todos los purposes de subida directa** (no es nuevo de `CompanyLogo`):

- El flujo de **subida directa** (el que usa el FE: `upload-session` → `PUT` a la URL SAS) **NO crea el contenedor**; solo firma una URL apuntando a él. Únicamente la subida *server-side* (reportes, PDF de constancia) hace `CreateIfNotExists`.
- Por tanto, el contenedor **`clarihr-company-logos` debe existir** en la cuenta de almacenamiento de cada entorno (Azurite en dev, y las cuentas de staging/prod), igual que ya existen `clarihr-profile-images`, `clarihr-medical-claim-documents`, etc.

**Síntoma si el contenedor falta:** `upload-session` responde **200 OK** (la config ya está), pero el `PUT` del navegador al blob falla con **`404 ContainerNotFound`**. Si ven eso, no es la config del purpose — es que falta crear el contenedor en ese entorno.

---

## 5. Scope / ACL (respuesta a su punto #3)

Confirmado — el logo es un recurso **a nivel de empresa**, no de expediente:

- **`entityId` NO es obligatorio.** En `upload-session`, `EntityId` es opcional (`Guid?`) y el handler de `CompanyLogo` **no** lo exige ni lo valida contra ningún expediente. Pueden omitirlo o mandarlo `null`.
- **El ownership es por tenant.** Al crear la sesión, el archivo se marca con el `TenantId` del tenant autenticado. Al guardar la configuración de constancias, el backend valida que el archivo referenciado sea: `Status == Active`, `TenantId == empresa` y `Purpose == CompanyLogo`. No hay acoplamiento a `personnel-file`.

---

## 6. FE — sin cambios pendientes

Tienen razón: el FE ya está correcto. Sigan mandando `purpose: "CompanyLogo"` por el flujo estándar `FileUploadService.uploadFile(file, 'CompanyLogo')`. Recordatorio de límites que ahora valida el backend:

- **Tipos:** `image/png`, `image/jpeg`, `image/webp` (extensiones `.png` / `.jpg` / `.jpeg` / `.webp`). **SVG no** (rompería el PDF).
- **Tamaño máximo:** 5 MB (`files.too_large` si se excede).
- Si el tipo/extensión no calza: `files.content_type_not_allowed` / `files.extension_not_allowed`.

En cuanto el backend despliegue el cambio de config (§2) y exista el contenedor (§4), la subida del logo funcionará de punta a punta.
