# Guía Frontend — Tipos de centro de costo

**Fecha:** 2026-08-04 · **Estado:** pantalla construida pero **inalcanzable** — hay un bug de despliegue

La pantalla `CostCenterTypesComponent` existe y está desplegada, pero **no carga**. Este documento tiene
el diagnóstico, el contrato del endpoint y lo que falta para integrarla.

---

## 1. El bug: falta el archivo de traducciones

Navegar a `https://dashboard.clarihr.com/cost-center-types` cae en **Home** con la consola mostrando:

```
Error: Unable to load translation and all the fallback languages, did you misspelled the scope name?
```

La causa:

```
/assets/i18n/en/costCenterTypes.json   →  NO existe (el servidor devuelve index.html)
/assets/i18n/es/costCenterTypes.json   →  NO existe (el servidor devuelve index.html)
/assets/i18n/en/costCenters.json       →  existe, 555 bytes  ✅
```

El componente declara el scope de traducción `costCenterTypes`, pero **ese archivo nunca se publicó**.
Transloco recibe HTML donde espera JSON, falla en el idioma pedido y en el de respaldo, y lanza. El
componente no llega a montarse y el router cae a la ruta por defecto.

### Lo que se descartó

| Hipótesis | Por qué no era |
|---|---|
| Falta de permiso | El guard es `CostCenters.Read`, el mismo de `/cost-centers`, que sí carga |
| El plan no incluye el módulo | `COST_CENTER_ADMINISTRATION` está en `effectiveCapabilities` con `grantedByPlan: true` |
| La pantalla no existe | `CostCenterTypesComponent` está compilado en `chunk-TT36BKIV.js` |
| El chunk no está desplegado | Responde `200` con 7.565 bytes |
| Error de la API | **Nunca hubo llamada a la API** — falla antes de montarse |

### Un problema de fondo que conviene arreglar

**El servidor devuelve `200` + `index.html` para cualquier asset inexistente.** Por eso en la pestaña de
red parecía que `costCenterTypes.json` cargaba bien (tres veces, `200`). Un `404` real en `/assets/**`
habría hecho evidente el problema en segundos.

Vale revisar **todos** los scopes de traducción contra los archivos publicados: si este se escapó, puede
haber más pantallas con el mismo fallo silencioso. Se detecta pidiendo cada `assets/i18n/en/*.json` y
verificando que la respuesta sea JSON y no HTML.

---

## 2. Falta enlazar dos pantallas en el menú

Ninguna de las dos está en la navegación de Configuración, que hoy lista 18 entradas:

| Ruta | Componente | Estado |
|---|---|---|
| `cost-center-types` | `CostCenterTypesComponent` | Sin entrada de menú |
| `cost-centers` | `CostCentersComponent` | Sin entrada de menú |

**El orden importa: tipos antes que centros.** Un centro de costo exige `costCenterTypePublicId`, así que
quien llegue primero a `/cost-centers` va a encontrar el desplegable **Tipo** vacío y sin forma de
resolverlo desde la interfaz. Es exactamente donde se trabó la configuración que originó este documento.

---

## 3. Contrato

```
GET    /api/v1/companies/{companyPublicId}/cost-center-types
POST   /api/v1/companies/{companyPublicId}/cost-center-types
GET    /api/v1/cost-center-types/{publicId}
PUT    /api/v1/cost-center-types/{publicId}
PATCH  /api/v1/cost-center-types/{publicId}
PATCH  /api/v1/cost-center-types/{publicId}/activate
PATCH  /api/v1/cost-center-types/{publicId}/inactivate
```

Permiso: `CostCenters.Read` para leer, `CostCenters.Admin` para administrar. Capacidad comercial
`COST_CENTER_ADMINISTRATION` (incluida en el plan Free).

### Listado

`GET /companies/{companyPublicId}/cost-center-types` acepta `isActive`, `q`, `page`, `pageSize`,
`includeAllowedActions`. Respuesta paginada de `CostCenterTypeListItemResponse`:

```jsonc
{
  "publicId": "…",
  "code": "OPERATIVO",
  "name": "Operativo",
  "isActive": true,
  "concurrencyToken": "…",
  "createdAtUtc": "…",
  "modifiedAtUtc": null,
  "allowedActions": { … },      // solo si includeAllowedActions=true
  "normalizedCode": "OPERATIVO"
}
```

El detalle (`CostCenterTypeResponse`) agrega `description`.

### Crear — `POST`

```jsonc
{
  "code": "OPERATIVO",
  "name": "Operativo",
  "description": null
}
```

`201` creado · `400` validación · `409` código repetido · `422` regla de negocio.

> `normalizedCode` aparece en el esquema del request marcado **`readOnly`** — no enviarlo, lo deriva el
> servidor a partir del `code`.

### Editar — `PUT` / `PATCH`

Ambos exigen `If-Match` con el `concurrencyToken`: ausente → `400`, desactualizado → `409`. El token
refrescado vuelve en el cuerpo y en el `ETag`.

- `PUT` reemplaza `code`, `name`, `description`.
- `PATCH` (RFC 6902) admite `/code`, `/name`, `/description`.

### Activar / inactivar

`PATCH .../activate` y `PATCH .../inactivate`, también con `If-Match`.

---

## 4. Validaciones

| Campo | Regla |
|---|---|
| `code` | Requerido, máx. 50. Formato `^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$` — empieza con alfanumérico; después letras, dígitos, guion y guion bajo. **Sin espacios ni acentos** |
| `name` | Requerido, máx. 150 |
| `description` | Opcional, máx. 500 |

Conviene replicar el formato del `code` en el cliente: es la validación que más se rompe al escribir
códigos con espacios o tildes.

---

## 5. Códigos de error

| Código | HTTP | Cuándo |
|---|---|---|
| `COST_CENTER_TYPE_NOT_FOUND` | `404` | El `publicId` no existe en esta empresa |
| `COST_CENTER_TYPE_CODE_CONFLICT` | `409` | Ya hay un tipo con ese código en la empresa |
| `COST_CENTER_TYPE_IN_USE` | `409`/`422` | Se intenta inactivar un tipo que tiene centros de costo usándolo |
| `COST_CENTER_TYPE_INACTIVE` | `422` | Se intenta crear o editar un **centro** apuntando a un tipo inactivo |
| `COST_CENTERS_FORBIDDEN` | `403` | Falta el permiso |

Los dos últimos son los que más impactan la interfaz:

- **`COST_CENTER_TYPE_IN_USE`**: el botón de inactivar debería advertir que hay centros asociados en vez
  de dejar que falle. No hay endpoint de `usage` para tipos —sí lo hay para centros
  (`GET /cost-centers/{publicId}/usage`)—, así que la alternativa es filtrar el listado de centros por
  tipo antes de ofrecer la acción.
- **`COST_CENTER_TYPE_INACTIVE`**: el desplegable de tipo en la pantalla de centros debe listar **solo
  tipos activos** (`GET .../cost-center-types?isActive=true`). Si muestra los inactivos, el usuario elige
  uno y recibe un `422` sin explicación evidente.

---

## 6. Qué hacer

- [ ] **Publicar `assets/i18n/{en,es}/costCenterTypes.json`** — desbloquea la pantalla
- [ ] Revisar el resto de scopes de traducción contra los archivos publicados
- [ ] Configurar `404` real para `/assets/**` en vez de devolver `index.html` con `200`
- [ ] Enlazar `cost-center-types` y `cost-centers` en el menú de Configuración, **tipos primero**
- [ ] Validación de formato de `code` en el cliente
- [ ] Desplegable de tipo en centros de costo filtrado por `isActive=true`
- [ ] Advertir antes de inactivar un tipo con centros asociados

---

## 7. Datos de arranque sugeridos

Cuatro tipos cubren la mayoría de las estructuras contables:

| `code` | `name` |
|---|---|
| `OPERATIVO` | Operativo |
| `TECNICO` | Técnico |
| `COMERCIAL` | Comercial |
| `ADMINISTRATIVO` | Administrativo |

> **No se siembran automáticamente**, y es deliberado: desde el cambio del 2026-08-04 la empresa define
> sus propios catálogos, porque ninguno de estos se puede borrar después — solo inactivar. Ver
> [guia-integracion-frontend-catalogos-de-empresa.md](guia-integracion-frontend-catalogos-de-empresa.md).

Mientras la pantalla siga bloqueada, se pueden cargar por API:

```bash
for row in "OPERATIVO|Operativo" "TECNICO|Técnico" "COMERCIAL|Comercial" "ADMINISTRATIVO|Administrativo"; do
  IFS='|' read -r code name <<< "$row"
  curl -s -X POST "$API/api/v1/companies/$COMPANY/cost-center-types" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d "{\"code\":\"$code\",\"name\":\"$name\",\"description\":null}" \
    -w " $code → %{http_code}\n" -o /dev/null
done
```
