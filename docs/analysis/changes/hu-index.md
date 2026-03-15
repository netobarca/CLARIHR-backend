# Índice de historias de usuario y cambios implementados

## 1. Propósito

Este archivo centraliza el seguimiento de las historias de usuario y requerimientos implementados en el proyecto.

Su objetivo es:

- mantener trazabilidad documental por HU,
- identificar rápidamente qué cambios fueron realizados,
- saber qué documentos vivos fueron actualizados,
- y evitar dispersión o duplicación de análisis por historia.

Este archivo funciona como **índice maestro** de los archivos ubicados en `docs/analysis/changes/`.

---

## 2. Reglas de uso

### Regla 1
Cada HU o requerimiento implementado debe tener una entrada en este índice.

### Regla 2
Si existe un archivo específico de cierre para la HU, debe referenciarse aquí.

### Regla 3
Este índice no sustituye el archivo detallado de la HU; solo lo resume.

### Regla 4
No duplicar aquí análisis extensos de arquitectura, seguridad, performance o testing.  
Aquí solo debe registrarse el resumen del cambio y sus referencias.

### Regla 5
Si una HU fue actualizada posteriormente, debe reflejarse en este índice sin perder trazabilidad histórica.

---

## 3. Convención recomendada para archivos HU

Formato recomendado:

- `HU-001.md`
- `HU-002.md`
- `HU-010.md`

Si el proyecto usa otra convención oficial, mantener una sola convención consistente.

---

## 4. Estado sugerido para HU

Usar uno de estos estados:

- **Pendiente**
- **En análisis**
- **En implementación**
- **Implementada**
- **Parcial**
- **En validación**
- **Cerrada**
- **Bloqueada**
- **Descartada**

---

## 5. Índice maestro

| Código HU | Título | Módulo | Estado | Fecha última actualización | Archivo de cambio | Documentos vivos actualizados | Observaciones |
|---|---|---|---|---|---|---|---|
| HU-001 | [Título de la historia] | [Módulo] | [Estado] | [YYYY-MM-DD] | [HU-001.md](./HU-001.md) | [Lista corta] | [Notas breves] |
| HU-002 | [Título de la historia] | [Módulo] | [Estado] | [YYYY-MM-DD] | [HU-002.md](./HU-002.md) | [Lista corta] | [Notas breves] |
| HU-003 | [Título de la historia] | [Módulo] | [Estado] | [YYYY-MM-DD] | [HU-003.md](./HU-003.md) | [Lista corta] | [Notas breves] |

---

## 6. Ejemplo de llenado

| Código HU | Título | Módulo | Estado | Fecha última actualización | Archivo de cambio | Documentos vivos actualizados | Observaciones |
|---|---|---|---|---|---|---|---|
| HU-001 | Crear organización / empresa | Organizations | Implementada | 2026-03-12 | [HU-001.md](./HU-001.md) | `project-foundation.md`, `architecture-analysis.md`, `endpoint-reference.md` | Incluyó endpoints, validaciones, tenant scope y auditoría |
| HU-002 | Crear usuarios de empresa | IAM / Organizations | En implementación | 2026-03-12 | [HU-002.md](./HU-002.md) | `security-analysis.md`, `testing-analysis.md` | Pendiente cierre de permisos por campo |

---

## 7. Regla para documentos vivos actualizados

En la columna **Documentos vivos actualizados**, listar solo los archivos realmente impactados, por ejemplo:

- `current-system-business-flows.md`
- `architecture-analysis.md`
- `security-analysis.md`
- `performance-analysis.md`
- `testing-analysis.md`
- `endpoint-reference.md`
- `openapi.yaml`

No listar documentos que no hayan sido modificados.

---

## 8. Regla para observaciones

La columna **Observaciones** debe contener solo notas cortas y útiles, por ejemplo:

- cambio con impacto en permisos,
- requiere migración SQL,
- pendiente validación QA,
- incluye cambio de contrato API,
- requiere ADR,
- no hubo impacto documental adicional.

Evitar observaciones largas o ambiguas.

---

## 9. Mantenimiento del índice

Este archivo debe actualizarse cuando:

- se crea una nueva HU,
- cambia el estado de una HU,
- cambia la fecha de actualización,
- cambia el archivo detallado asociado,
- cambia el conjunto de documentos vivos impactados,
- se detecta un riesgo o nota relevante de seguimiento.

---

## 10. Criterio de orden

Mantener las HUs ordenadas preferiblemente por:

1. número de HU ascendente, o
2. prioridad funcional definida por el proyecto.

No mezclar varios criterios al mismo tiempo.

---

## 11. Buenas prácticas

- mantener una fila por HU,
- usar títulos cortos y claros,
- no dejar estados ambiguos,
- enlazar siempre el archivo de detalle cuando exista,
- mantener fechas actualizadas,
- usar este índice como punto de entrada al historial documental.

---

## 12. Criterio rector del índice

Este índice debe permitir responder rápidamente:

- qué historia se trabajó,
- en qué estado está,
- dónde está su detalle,
- y qué parte de la documentación viva fue impactada.