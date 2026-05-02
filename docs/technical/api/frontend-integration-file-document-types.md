# Catálogo de Tipos de Documento (`file-document-types`)

> **Fecha**: 2026-05-01  
> **Controlador**: `GeneralCatalogsController`

---

## Endpoint

```
GET /api/v1/companies/{companyId}/general-catalogs/file-document-types
Authorization: Bearer {token}
```

Usa el mismo endpoint genérico de catálogos generales. No requiere parámetros de paginación ni búsqueda.

---

## Respuesta `200 OK`

```json
[
  {
    "id": "f5e6d7c8-1234-5678-9abc-def012345678",
    "category": "FileDocumentType",
    "code": "DUI",
    "name": "Documento Único de Identidad",
    "isSystem": true,
    "isActive": true,
    "sortOrder": 1
  },
  {
    "id": "a1b2c3d4-5678-9abc-def0-123456789abc",
    "category": "FileDocumentType",
    "code": "CONTRATO_LABORAL",
    "name": "Contrato Laboral",
    "isSystem": true,
    "isActive": true,
    "sortOrder": 2
  }
]
```

---

## Campos de la respuesta

| Campo | Tipo | Descripción |
|---|---|---|
| `id` | `guid` | PublicId del tipo de documento — usar como valor de `documentTypeCatalogItemPublicId` |
| `category` | `string` | Siempre `"FileDocumentType"` |
| `code` | `string` | Código interno del tipo |
| `name` | `string` | Nombre legible para mostrar en el UI |
| `isSystem` | `bool` | Siempre `true` (catálogo de sistema) |
| `isActive` | `bool` | Siempre `true` (el endpoint filtra inactivos) |
| `sortOrder` | `int` | Orden sugerido para el dropdown |

---

## Uso en frontend

```typescript
// Obtener tipos de documento
const response = await api.get(
  `/api/v1/companies/${companyId}/general-catalogs/file-document-types`
);
const documentTypes = response.data; // array plano

// Poblar <select>
documentTypes.forEach(dt => {
  // value: dt.id (UUID) → se envía como documentTypeCatalogItemPublicId
  // label: dt.name
});
```

---

## Dónde se usa

Al crear o actualizar un documento del expediente, el campo `documentTypeCatalogItemPublicId` debe contener el `id` de uno de los items de este catálogo:

```json
{
  "filePublicId": "...",
  "documentTypeCatalogItemPublicId": "f5e6d7c8-1234-5678-9abc-def012345678",
  "observations": "...",
  "concurrencyToken": "..."
}
```

---

## Errores posibles

| HTTP | Causa |
|---|---|
| `400` | `catalogKey` no soportado (si se escribe mal la clave) |
| `401` | Token de autenticación faltante o inválido |
| `403` | Sin permisos para la compañía |
