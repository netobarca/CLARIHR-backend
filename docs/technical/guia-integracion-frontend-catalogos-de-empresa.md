# Guía Frontend — Cambio en los catálogos de empresa

**Fecha:** 2026-08-04 · **Tipo:** cambio de comportamiento + 2 endpoints eliminados

Una empresa recién creada ya **no** recibe catálogos de ejemplo. Antes nacía con tipos de unidad
`GERENCIA`/`DEPARTAMENTO`/`UNIDAD`, áreas funcionales `ADMIN`/`OPS`/`SALES`, seis justificaciones de horas
extra y un juego de reconocimientos y amonestaciones. Ahora nace **vacía** en todo eso.

---

## 1. Por qué

Ninguno de esos catálogos se puede borrar: la API expone `activate` / `inactivate`, **no** `DELETE`. Una
empresa que no usa `GERENCIA` no puede eliminarla — solo desactivarla, y la fila queda en la base para
siempre. Sembrar una conjetura sobre cómo se organiza cada empresa dejaba ruido permanente en todos los
tenants que no coincidían con ella, que son casi todos.

El criterio quedó así: **se siembra lo que fija la ley o la geografía; lo que la empresa define, lo crea la
empresa.** Y como no hay clientes en producción, se hizo ahora, sin migración.

---

## 2. Qué trae una empresa nueva

### Sigue viniendo sembrado

| Catálogo | Cantidad | Por qué |
|---|---|---|
| Jerarquía de ubicaciones del país | 3 niveles + departamentos y municipios | Geografía real |
| Tipos de horas extra `HED` `HEN` `HEDF` `HENF` | 4 | Factores 2.00 / 2.50 / 4.00 / 5.00 — Art. 168/169/171/175 CT |
| Tipos y riesgos de incapacidad | 6 | El marcador de riesgo profesional cambia el cálculo del subsidio |
| Asuetos nacionales | 11 (año en curso) | Ley |
| Jornada ordinaria 44 h | 1 | Art. 161 CT |
| Escala de calificación de competencias | 1 | El módulo reporta `isConfigured = false` sin ella |

### Ya no viene nada

| Catálogo | Antes | Ahora |
|---|---|---|
| Tipos de unidad organizativa | 3 | **0** |
| Áreas funcionales | 4 | **0** |
| Justificaciones de horas extra | 6 | **0** |
| Tipos de reconocimiento | varios | **0** |
| Tipos de amonestación | 4 | **0** |
| Causas de amonestación | 6 | **0** |

---

## 3. Endpoints eliminados — cambio incompatible

```diff
- POST /api/v1/companies/{companyPublicId}/employee-relations/load-template
- POST /api/v1/companies/{companyPublicId}/overtime-configuration/load-template
```

Ambos devolvían un resumen de filas creadas/omitidas. **Si el frontend los llama, hay que quitar esas
llamadas**: ahora responden `404`.

No se reemplazan por nada. No hay "cargar catálogo de ejemplo" — la decisión fue no ofrecer los genéricos
ni siquiera como opción.

### Endpoints `load-template` que SÍ siguen existiendo

```
POST /api/v1/companies/{companyPublicId}/leave-configuration/load-template
POST /api/v1/companies/{companyPublicId}/payroll-configuration/load-template
POST /api/v1/companies/{companyPublicId}/not-worked-time-configuration/load-template
```

No se tocaron. Los dos primeros cargan estructura legal; el tercero nunca se sembró solo y sigue siendo la
única vía para los tiempos no trabajados.

### El CRUD no cambió

Los seis catálogos que dejaron de sembrarse **conservan sus rutas completas** (`GET`, `POST`, `PUT`,
`PATCH` + `activate`/`inactivate`). Lo único que cambió es que arrancan vacíos:

```
GET,POST /api/v1/companies/{companyPublicId}/organization-structure-catalogs/unit-types
GET,POST /api/v1/companies/{companyPublicId}/organization-structure-catalogs/functional-areas
GET,POST /api/v1/companies/{companyPublicId}/overtime-justification-types
GET,POST /api/v1/companies/{companyPublicId}/recognition-types
GET,POST /api/v1/companies/{companyPublicId}/disciplinary-action-types
GET,POST /api/v1/companies/{companyPublicId}/disciplinary-action-causes
```

---

## 4. Impacto en los flujos

### 4.1 · Asistente de configuración (`/setup`) — requiere cambio

Hoy el paso **"Tipos de unidad"** aparece como **Completado** apenas se crea la empresa, porque existían las
tres filas sembradas. Con este cambio la lista viene vacía, así que:

- El paso debe pasar a **Disponible** (no bloqueado: no depende de ningún otro).
- Si la lógica de "completado" se apoya en *"existe al menos una fila"*, ahora funciona correctamente sin
  tocarla. **Conviene verificarlo**: si en vez de eso está marcado como completado de forma fija, hay que
  corregirlo.

Este cambio es en realidad una mejora del asistente: antes daba por configurado algo que nadie había
configurado.

### 4.2 · Pantallas de catálogo — necesitan estado vacío

Las seis pantallas van a abrir sin filas en una empresa nueva. Si hoy asumen que siempre hay al menos una,
hay que agregar el estado vacío con su llamada a la acción ("Creá tu primer tipo de unidad").

### 4.3 · Selectores dependientes — el orden ahora importa

Formularios que ofrecen estos catálogos en un desplegable pueden quedar sin opciones:

| Pantalla | Selector que puede venir vacío |
|---|---|
| Crear unidad organizativa | Tipo de unidad *(obligatorio)*, área funcional *(opcional)* |
| Registrar horas extra | Justificación |
| Registrar reconocimiento | Tipo de reconocimiento |
| Registrar amonestación | Tipo y causa |

**El caso que bloquea es el primero**: `orgUnitTypePublicId` es obligatorio para crear una unidad
organizativa. Sin tipos, no se puede armar el organigrama. Conviene que la pantalla detecte el desplegable
vacío y enlace directo a crear el catálogo, en vez de dejar un formulario que no se puede enviar.

### 4.4 · Lo que no cambia

- Horas extra: los 4 tipos con sus factores legales siguen viniendo. Solo faltan las justificaciones.
- Vacaciones, incapacidades, asuetos y jornada: igual que antes.
- Ubicaciones: igual que antes.
- Competencias: la escala sigue viniendo.

---

## 5. Recordatorio: estos catálogos no se borran

Aplica a todo lo de la sección 3, y conviene reflejarlo en la interfaz:

- **No hay `DELETE`.** La acción es `inactivate`, y la fila sigue existiendo. Si la UI dice "Eliminar",
  conviene cambiarlo por "Desactivar" para que nadie espere que desaparezca.
- **Inactivar falla si el catálogo está en uso.** Tipos de unidad y áreas funcionales devuelven
  `ORG_STRUCTURE_CATALOG_IN_USE`; centros de costo, `COST_CENTER_TYPE_IN_USE`.
- **El tipo de una unidad organizativa no es parcheable.** El `PATCH` solo admite `/name`, `/sortOrder` y
  `/description`. Para reasignar `orgUnitTypePublicId` hay que usar el `PUT` completo, que es reemplazo
  total. Y `parentPublicId` no viaja en el `PUT`: el padre se cambia solo con `/move`.

> **Asimetría a revisar juntos:** inactivar un `work-center-type` que tiene centros de trabajo usándolo
> **no** está bloqueado, a diferencia de sus equivalentes. Puede ser deliberado o un descuido; está
> pendiente de confirmar.

---

## 6. Qué hacer

- [ ] Quitar las llamadas a los dos endpoints eliminados (si existían)
- [ ] Verificar el paso "Tipos de unidad" del asistente `/setup`
- [ ] Estado vacío en las seis pantallas de catálogo
- [ ] Selector de tipo de unidad: detectar vacío y enlazar a crearlo
- [ ] Revisar los textos "Eliminar" → "Desactivar" en estos catálogos
- [ ] Regenerar el cliente tipado contra el `openapi.yaml` actualizado (558 rutas)

> **Las empresas ya creadas conservan sus genéricos.** Este cambio solo afecta a las nuevas. Para limpiar
> las existentes haría falta un `DELETE` para filas nunca referenciadas, que quedó fuera de alcance.
