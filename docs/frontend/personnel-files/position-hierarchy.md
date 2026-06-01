# Position Hierarchy — Jerarquía del puesto

Sub‑recurso de **empleo** de **solo lectura**: devuelve la jerarquía de la persona **derivada** de la plaza asignada en su perfil de empleado — su jefe inmediato y sus subordinados directos. No se crea ni se edita; se calcula a partir de la estructura organizativa.

> Antes de consumir, leé las [Convenciones](./_conventions.md) (auth, `If-Match`, JSON Patch, paginación, errores). Acá solo se documenta lo específico de este recurso.

> ⚠️ **Solo sobre archivo finalizado.** El `GET` requiere un archivo **finalizado** (empleado, `lifecycleStatus = Completed`). Sobre un archivo en `Draft` responde **422** (todavía no tiene plaza asignada de la cual derivar la jerarquía). Ver [Convenciones §9](./_conventions.md#9-sub-recursos-de-empleado-talent--compensation--employment).

> **Solo `GET`.** No hay `POST`, `PUT`, `PATCH` ni `DELETE`: la jerarquía es derivada, no editable. La asignación de plaza se hace en el [perfil de empleado](./employee-profile.md) / [asignaciones de empleo](./employment-assignments.md).

**Permisos:** `GET` → `PersonnelFiles.Read`.

## Endpoints

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET`  | `/api/v1/personnel-files/{publicId}/position-hierarchy` | Obtener la jerarquía del puesto (jefe + subordinados) |

`publicId` = id del archivo de personal.

---

## `GET` Obtener la jerarquía

`GET /api/v1/personnel-files/{publicId}/position-hierarchy` → `200` con la jerarquía derivada.

**Respuesta `200`** — `PersonnelFilePositionHierarchyResponse`:

| Campo | Tipo | Notas |
|-------|------|-------|
| `personnelFilePublicId` | uuid | El propio archivo. |
| `orgUnitPublicId` | uuid (nullable) | Unidad organizativa del puesto. |
| `immediateSupervisorPersonnelFilePublicId` | uuid (nullable) | Archivo de personal del jefe inmediato. |
| `immediateSupervisorName` | string (nullable) | Nombre del jefe inmediato. |
| `subordinates` | array (nullable) | Subordinados directos (ver abajo). |

Cada elemento de `subordinates`:

| Campo | Tipo |
|-------|------|
| `personnelFilePublicId` | uuid |
| `fullName` | string (nullable) |
| `orgUnitPublicId` | uuid (nullable) |

```bash
curl "$BASE/api/v1/personnel-files/$ID/position-hierarchy" \
  -H "Authorization: Bearer $TOKEN"
```

```jsonc
// 200 OK
{
  "personnelFilePublicId": "3d9e...05",
  "orgUnitPublicId": "0b1d...e9",
  "immediateSupervisorPersonnelFilePublicId": "8c3e...b2",
  "immediateSupervisorName": "Carlos Funes",
  "subordinates": [
    {
      "personnelFilePublicId": "6f2a...d7",
      "fullName": "Ana Rivas",
      "orgUnitPublicId": "0b1d...e9"
    },
    {
      "personnelFilePublicId": "7b8c...e3",
      "fullName": "Diego Cruz",
      "orgUnitPublicId": "0b1d...e9"
    }
  ]
}
```

**Errores:** `401`, `403`, `404`, `422` (archivo en `Draft` / sin plaza asignada).
