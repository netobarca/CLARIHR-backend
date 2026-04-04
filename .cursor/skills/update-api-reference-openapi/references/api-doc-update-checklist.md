# Checklist de actualización de documentación de API

## 1. Propósito

Esta guía ayuda a decidir **qué revisar y qué actualizar** cuando una historia de usuario o requerimiento cambia endpoints, contratos o comportamiento observable de la API.

Debe usarse junto con:

- `.agents/skills/update-api-reference-openapi/SKILL.md`
- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`
- `.agents/skills/review-dotnet-cqrs-user-story/SKILL.md`
- `.agents/skills/close-user-story-docs/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`
- `docs/AGENTS.md`

Su objetivo es evitar:

- contradicciones entre código y documentación,
- omisiones en request/response,
- omisiones en auth o tenant scope,
- duplicación entre OpenAPI y markdown,
- y falta de trazabilidad del cambio en la HU.

---

## 2. Regla principal

Antes de actualizar documentación de API, responder internamente:

1. ¿Qué cambió realmente en el contrato observable?
2. ¿El cambio afecta solo OpenAPI, solo endpoint reference o ambos?
3. ¿La documentación actual ya cubre este endpoint?
4. ¿Estoy por duplicar información ya existente?
5. ¿Este cambio requiere además actualizar seguridad, performance o el cierre de la HU?

### Regla de decisión
- Si cambió el **contrato estructurado**, actualizar `openapi.yaml`.
- Si cambió la **explicación humana resumida**, actualizar `endpoint-reference.md`.
- Si cambió la API por una HU, dejar también trazabilidad en `HU-XXXX.md`.

---

## 3. Cuándo actualizar la documentación de API

Actualizar documentación de API cuando cambie alguno de estos puntos:

- endpoint nuevo,
- endpoint eliminado,
- ruta modificada,
- método HTTP modificado,
- request body,
- response body,
- parámetros de ruta,
- query params,
- headers relevantes,
- autenticación,
- autorización,
- códigos de error,
- paginación,
- filtros,
- sorting,
- comportamiento observable del endpoint.

---

## 4. Cuándo NO actualizar la documentación de API

No actualizar la documentación de API cuando el cambio sea solo interno y no observable, por ejemplo:

- refactor interno sin cambio de contrato,
- mejora interna de repositorio,
- cambio de implementación sin afectar request/response,
- cambio técnico sin impacto en auth, filtros, códigos o shape de datos,
- optimización interna sin cambiar comportamiento del endpoint.

### Regla
Si el consumidor de la API no percibe ningún cambio, probablemente no hace falta tocar `endpoint-reference.md` ni `openapi.yaml`.

---

## 5. Checklist rápida de clasificación del cambio

## 5.1 Endpoint nuevo
- [ ] Se agregó la ruta
- [ ] Se agregó el método HTTP correcto
- [ ] Se documentó el propósito
- [ ] Se documentó request
- [ ] Se documentó response
- [ ] Se documentó auth
- [ ] Se documentaron errores principales
- [ ] Se documentó tenant scope si aplica

## 5.2 Endpoint modificado
- [ ] Se identificó exactamente qué cambió
- [ ] Se actualizó request si cambió
- [ ] Se actualizó response si cambió
- [ ] Se actualizó auth si cambió
- [ ] Se actualizaron errores si cambiaron
- [ ] Se actualizaron filtros/paginación si cambiaron

## 5.3 Endpoint eliminado o reemplazado
- [ ] Se eliminó o marcó como obsoleto en OpenAPI
- [ ] Se eliminó o actualizó la referencia humana
- [ ] Se dejó trazabilidad en la HU si aplica

---

## 6. Qué revisar en el código antes de documentar

Antes de actualizar docs, confirmar directamente en el código:

- método HTTP real,
- ruta real,
- nombre del endpoint o caso de uso,
- request DTO real,
- response DTO real,
- códigos de estado esperados,
- uso de auth / authorize,
- uso de permisos o políticas,
- filtros / paginación / sorting reales,
- comportamiento ante errores esperados.

### Regla
No documentar basándose solo en la HU si el código ya existe.  
La documentación debe alinearse con el comportamiento real implementado.

---

## 7. Qué debe revisarse en `endpoint-reference.md`

Para cada endpoint impactado, confirmar que la referencia humana deje claro:

- método
- ruta
- propósito funcional
- autenticación requerida
- autorización o permiso si aplica
- tenant scope si aplica
- request principal
- response principal
- errores relevantes
- filtros / paginación / sorting si aplican
- observaciones importantes del comportamiento

### Señales de alerta
- endpoint descrito pero sin auth,
- request resumido de forma ambigua,
- response desactualizada,
- errores omitidos,
- endpoint duplicado en otra sección.

---

## 8. Qué debe revisarse en `openapi.yaml`

Confirmar que OpenAPI refleje correctamente:

- `paths`
- `methods`
- `summary`
- `description`
- `tags` si se usan
- `parameters`
- `requestBody`
- `responses`
- `security`
- `schemas`
- ejemplos si el proyecto los usa
- paginación/filtros/sorting si aplican

### Señales de alerta
- path faltante,
- método incorrecto,
- requestBody incompleto,
- response 200 correcta pero errores faltantes,
- security faltante,
- schema viejo que no coincide con el DTO.

---

## 9. Checklist de request

Cuando el request cambie o exista request body, revisar:

- [ ] Campos requeridos
- [ ] Campos opcionales
- [ ] Tipos correctos
- [ ] Formatos relevantes
- [ ] Enumeraciones o valores permitidos
- [ ] Restricciones importantes si deben ser visibles al consumidor
- [ ] Parámetros de query correctamente descritos
- [ ] Parámetros de ruta correctamente descritos

### Regla
No sobrecargar la referencia humana con todas las reglas internas, pero sí documentar lo que el consumidor necesita para usar el endpoint correctamente.

---

## 10. Checklist de response

Cuando el response cambie o exista nuevo contrato, revisar:

- [ ] Shape principal correcto
- [ ] Campos relevantes actualizados
- [ ] Objetos anidados correctos
- [ ] Colecciones correctas
- [ ] Metadatos de paginación correctos si existen
- [ ] Campos sensibles no expuestos innecesariamente
- [ ] Response de éxito alineada con el código real

### Regla
La documentación de response debe reflejar lo que realmente recibe el consumidor, no lo que “debería recibir” idealmente.

---

## 11. Checklist de errores

Cuando el endpoint tenga errores relevantes, confirmar:

- [ ] `400` documentado si hay validaciones visibles
- [ ] `401` documentado si requiere autenticación
- [ ] `403` documentado si requiere autorización
- [ ] `404` documentado cuando aplica
- [ ] `409` documentado si hay conflicto funcional
- [ ] otros errores relevantes documentados si el contrato los expone

### Regla
No listar códigos irrelevantes, pero sí dejar claros los errores que el consumidor debe esperar razonablemente.

---

## 12. Checklist de auth y seguridad

Si el endpoint requiere autenticación o autorización, revisar:

- [ ] Se indicó si requiere usuario autenticado
- [ ] Se indicó permiso o política si aplica
- [ ] Se aclaró tenant scope si aplica
- [ ] No se documentó como público por error
- [ ] No se omitió restricción sensible
- [ ] No se exponen datos innecesarios en ejemplos o responses

### Señales de alerta
- endpoint administrativo sin nota de autorización,
- endpoint tenant-scoped sin aclararlo,
- endpoint sensible documentado como si cualquiera pudiera usarlo.

---

## 13. Checklist de tenant scope

Cuando el endpoint es multi-tenant o restringido por empresa/organización, revisar:

- [ ] Se aclaró que opera dentro del tenant actual
- [ ] La ruta o descripción no sugieren acceso global si no existe
- [ ] Los filtros o lookups documentados no contradicen el aislamiento por tenant
- [ ] No se sugieren ids globales reutilizables fuera del scope permitido

### Regla
Si el endpoint está scoped por tenant, la documentación no debe dejar espacio a interpretaciones globales.

---

## 14. Checklist de paginación, filtros y sorting

Si el endpoint lista o busca información, revisar:

- [ ] Paginación documentada
- [ ] Parámetros de página documentados
- [ ] Parámetros de tamaño documentados
- [ ] Filtros documentados
- [ ] Sorting documentado si aplica
- [ ] Response paginada explicada correctamente

### Señales de alerta
- listado documentado como simple array cuando en realidad es paginado,
- filtros reales no documentados,
- sorting soportado pero omitido,
- endpoint de alto volumen sin aclaración de paginación.

---

## 15. Checklist de consistencia entre OpenAPI y endpoint reference

Antes de cerrar, revisar:

- [ ] Misma ruta en ambos documentos
- [ ] Mismo método HTTP en ambos documentos
- [ ] Misma auth en ambos documentos
- [ ] Mismo request principal en ambos documentos
- [ ] Mismo response principal en ambos documentos
- [ ] Mismos errores relevantes en ambos documentos
- [ ] No hay contradicciones en filtros o paginación

### Regla
Si ambos documentos existen, deben complementarse, no competir ni contradecirse.

---

## 16. Checklist de trazabilidad de HU

Si el cambio viene de una HU, revisar:

- [ ] El archivo `HU-XXXX.md` menciona cambios de API
- [ ] `hu-index.md` podrá reflejar que hubo impacto en API
- [ ] La HU no es el único lugar donde quedó documentado el contrato
- [ ] No se omitió actualizar documentación viva de API

---

## 17. Cuándo actualizar además otros documentos

Además de API docs, evaluar actualización de otros documentos si aplica:

### Seguridad
Actualizar `docs/analysis/current-state/security-analysis.md` si cambió:
- autenticación,
- autorización,
- permisos,
- exposición de datos,
- tenant isolation.

### Performance
Actualizar `docs/analysis/current-state/performance-analysis.md` si cambió:
- paginación,
- patrones de consulta,
- filtros relevantes,
- rutas críticas de alto volumen.

### Cierre HU
Actualizar `docs/analysis/changes/HU-XXXX.md` si hubo impacto en API.

---

## 18. Errores comunes a evitar

Evitar siempre:

- documentar un endpoint sin verificar el código real,
- actualizar solo OpenAPI y olvidar la referencia humana,
- actualizar solo la referencia humana y olvidar OpenAPI,
- copiar todo el schema en markdown innecesariamente,
- dejar auth incorrecta o incompleta,
- omitir errores importantes,
- duplicar endpoints en varias secciones,
- dejar ejemplos que ya no corresponden al contrato real.

---

## 19. Secuencia recomendada

1. Leer la HU o requerimiento.
2. Confirmar el comportamiento real en código.
3. Identificar qué cambió exactamente.
4. Actualizar `openapi.yaml` si corresponde.
5. Actualizar `endpoint-reference.md` si corresponde.
6. Verificar consistencia entre ambos.
7. Reflejar el cambio en `HU-XXXX.md` si aplica.
8. Revisar si seguridad o performance también deben actualizarse.

---

## 20. Checklist final resumida

- [ ] Entendí qué cambió realmente en la API
- [ ] Revisé el código real
- [ ] Actualicé `openapi.yaml` si correspondía
- [ ] Actualicé `endpoint-reference.md` si correspondía
- [ ] Verifiqué auth y tenant scope
- [ ] Verifiqué request y response
- [ ] Verifiqué errores principales
- [ ] Verifiqué filtros/paginación/sorting si aplican
- [ ] Evité duplicación
- [ ] Dejé trazabilidad en la HU si aplicaba

---

## 21. Criterio rector

La documentación de API debe quedar alineada con el código real, clara para el consumidor, consistente entre OpenAPI y referencia humana, y sin duplicación innecesaria.