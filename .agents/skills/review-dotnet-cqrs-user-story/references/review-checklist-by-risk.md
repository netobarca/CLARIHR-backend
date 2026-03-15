# Checklist de revisión técnica por nivel de riesgo

## 1. Propósito

Esta guía ayuda a revisar una historia de usuario o requerimiento backend según el **nivel de riesgo técnico y funcional** del cambio.

Debe usarse junto con:

- `.agents/skills/review-dotnet-cqrs-user-story/SKILL.md`
- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`
- `.agents/skills/unit-test-dotnet-cqrs-user-story/SKILL.md`
- `.agents/skills/close-user-story-docs/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`
- `docs/AGENTS.md`

Su objetivo es ayudar a decidir:

- qué revisar con más profundidad,
- qué hallazgos son realmente importantes,
- cuándo una HU puede aprobarse,
- y cuándo debe bloquearse hasta corregir riesgos críticos.

---

## 2. Regla principal

No todas las HUs tienen el mismo nivel de riesgo.

La revisión debe ser proporcional al impacto del cambio.

### Regla de decisión
- Si el cambio es de **riesgo bajo**, revisar lo esencial sin sobredimensionar observaciones.
- Si el cambio es de **riesgo medio**, revisar arquitectura, funcionalidad, testing y posibles impactos secundarios.
- Si el cambio es de **riesgo alto**, revisar con especial profundidad tenant, seguridad, permisos, persistencia, rendimiento, auditoría y documentación.

---

## 3. Niveles de riesgo

## 3.1 Riesgo bajo
Cambios acotados, con poco impacto estructural y bajo riesgo de seguridad o fuga de datos.

### Ejemplos
- ajuste menor de validación,
- corrección puntual de mensaje o regla simple,
- refactor pequeño sin cambio de comportamiento observable,
- ajuste menor de mapping,
- bug fix local sin impacto en tenant, permisos o contratos.

## 3.2 Riesgo medio
Cambios funcionales relevantes, pero sin tocar zonas extremadamente sensibles o estructurales.

### Ejemplos
- nuevo endpoint simple,
- nuevo query paginado,
- creación o edición básica de entidad no crítica,
- ajustes de flujo con impacto controlado,
- incorporación de validaciones relevantes,
- cambios de comportamiento visibles pero acotados.

## 3.3 Riesgo alto
Cambios con impacto fuerte en seguridad, multi-tenant, datos sensibles, persistencia, arquitectura o rendimiento.

### Ejemplos
- autenticación,
- autorización,
- roles y permisos,
- field permissions,
- tenant isolation,
- salarios,
- datos personales,
- aprobaciones,
- auditoría,
- cambios masivos,
- importaciones / exportaciones,
- reportes pesados,
- cambios estructurales de datos,
- cambios de arquitectura base,
- cambios de estrategia de IDs o diseño multi-tenant.

---

## 4. Cómo clasificar rápidamente una HU

Haz estas preguntas:

1. ¿Toca datos sensibles?
2. ¿Toca permisos o seguridad?
3. ¿Toca tenant isolation?
4. ¿Toca persistencia o estructura de datos?
5. ¿Toca arquitectura base o decisiones duraderas?
6. ¿Toca endpoints públicos o contratos importantes?
7. ¿Toca listados grandes, reportes o performance?
8. ¿Toca auditoría o trazabilidad crítica?

### Regla rápida
- Si casi todas son “no”, probablemente es riesgo bajo.
- Si una o dos son “sí”, probablemente es riesgo medio.
- Si varias son “sí”, o una sola es altamente sensible, tratar como riesgo alto.

---

## 5. Checklist base para toda revisión

Estas validaciones aplican a cualquier nivel de riesgo:

- [ ] El cambio cumple el objetivo funcional
- [ ] La lógica está en la capa correcta
- [ ] No hay violaciones obvias de Clean Architecture
- [ ] El caso de uso está bien modelado como Command o Query
- [ ] Los errores principales están contemplados
- [ ] Hay pruebas suficientes para el alcance
- [ ] La HU tiene o tendrá trazabilidad documental
- [ ] No se introdujo complejidad innecesaria

---

## 6. Checklist para cambios de riesgo bajo

En cambios de riesgo bajo, revisar al menos:

### Funcional
- [ ] El cambio hace lo que debía hacer
- [ ] No rompe comportamiento existente evidente

### Arquitectura
- [ ] No se movió lógica a una capa incorrecta
- [ ] No se introdujo acoplamiento innecesario

### Testing
- [ ] Hay pruebas razonables si el cambio lo amerita
- [ ] No faltan tests obvios para la regla principal

### Documentación
- [ ] La trazabilidad por HU está prevista si aplica
- [ ] No se creó documentación duplicada

### Señales de alerta
- bug corregido pero sin prueba,
- validación cambiada pero sin cubrir error principal,
- cambio pequeño implementado en una capa incorrecta,
- ruido técnico innecesario para algo simple.

---

## 7. Checklist para cambios de riesgo medio

En cambios de riesgo medio, revisar:

### Funcional
- [ ] El flujo principal queda cubierto
- [ ] Los casos alternos importantes están manejados
- [ ] El comportamiento observable es consistente con la HU

### Arquitectura y CQRS
- [ ] La lógica está en Domain / Application cuando corresponde
- [ ] El caso está correctamente modelado como Command o Query
- [ ] Controllers siguen delgados
- [ ] No hay mezcla innecesaria entre lectura y escritura

### Seguridad y tenant
- [ ] El tenant scope fue considerado si aplica
- [ ] Los permisos fueron considerados si aplica
- [ ] No hay exposición innecesaria de datos

### Performance
- [ ] Listados tienen paginación si aplica
- [ ] Lecturas usan proyección a DTO
- [ ] No hay consultas evidentemente ineficientes

### Testing
- [ ] Hay happy path
- [ ] Hay error principal
- [ ] Hay pruebas de validación
- [ ] Hay pruebas de tenant o permisos si aplican

### Documentación
- [ ] Se identificaron documentos vivos impactados
- [ ] El cierre documental de la HU es factible sin ambigüedad

### Señales de alerta
- endpoint nuevo sin paginación en listados,
- Query con side effects,
- permisos implícitos pero no validados,
- tests que solo cubren happy path,
- cambio de API sin referencia prevista.

---

## 8. Checklist para cambios de riesgo alto

En cambios de riesgo alto, revisar con profundidad:

### Funcional
- [ ] El flujo cubre correctamente todos los criterios importantes
- [ ] Los estados inválidos están protegidos
- [ ] Los errores críticos están bien manejados
- [ ] El comportamiento no deja huecos operativos o de negocio

### Arquitectura
- [ ] No hay violaciones de Clean Architecture
- [ ] No hay lógica sensible en controllers
- [ ] Domain protege invariantes importantes
- [ ] Application orquesta correctamente el caso de uso
- [ ] Infrastructure no asumió decisiones que pertenecen al negocio

### CQRS
- [ ] Commands y Queries están claramente separados
- [ ] No hay handlers híbridos innecesarios
- [ ] Las lecturas no mutan estado
- [ ] Las escrituras no se disfrazan de queries

### Tenant isolation
- [ ] Toda lectura está filtrada por tenant cuando corresponde
- [ ] Toda escritura valida tenant correctamente
- [ ] No hay riesgo de cross-tenant leakage
- [ ] No hay búsquedas por id global sin tenant scope
- [ ] Se valida pertenencia de relaciones y recursos al tenant

### Seguridad
- [ ] La autenticación está bien aplicada
- [ ] La autorización está bien aplicada
- [ ] Los permisos por acción o rol están controlados
- [ ] Se protege PII o datos sensibles
- [ ] No se revelan detalles innecesarios en errores
- [ ] Hay auditoría si la acción es crítica

### Performance
- [ ] No hay listados sin paginación
- [ ] No hay consultas pesadas evitables
- [ ] Se usan proyecciones adecuadas
- [ ] Se usan filtros tempranos
- [ ] Se evaluaron índices o impacto en consultas
- [ ] No se metió procesamiento pesado innecesario en el request path

### Persistencia / Data
- [ ] El cambio de datos es consistente
- [ ] Las relaciones son correctas
- [ ] Hay tenant awareness en datos
- [ ] Se consideraron índices o constraints relevantes
- [ ] No se compromete integridad de datos

### Testing
- [ ] Hay happy path
- [ ] Hay validaciones
- [ ] Hay errores importantes
- [ ] Hay permisos
- [ ] Hay tenant scope
- [ ] Hay cobertura sobre reglas críticas
- [ ] Los tests validan Result / ErrorCode cuando aplica

### Documentación
- [ ] El cambio tiene trazabilidad suficiente
- [ ] Se identificaron todos los documentos vivos impactados
- [ ] No falta referencia técnica de API si el contrato cambió
- [ ] No falta análisis de seguridad/performance si hubo impacto real
- [ ] El cierre documental no generará duplicación

### Señales de alerta críticas
- recurso accesible desde otro tenant,
- endpoint sensible sin autorización,
- fuga de datos personales o salariales,
- cambio fuerte sin auditoría cuando era necesaria,
- operación pesada con alto riesgo de rendimiento,
- no existen pruebas sobre permisos o tenant en un flujo sensible,
- contrato de API cambió sin trazabilidad,
- cambio estructural sin documentación ni análisis.

---

## 9. Riesgos que deben bloquear una HU

La HU debe considerarse **bloqueada** si detectas algo como:

- acceso cross-tenant posible,
- omisión grave de permisos,
- exposición seria de datos sensibles,
- corrupción o inconsistencia probable de datos,
- violación grave de arquitectura que compromete mantenibilidad o seguridad,
- falta total de cobertura mínima en flujos sensibles,
- cambio crítico de comportamiento sin manejo correcto de errores,
- contrato expuesto incorrectamente en flujos sensibles.

---

## 10. Riesgos que permiten continuar con corrección posterior

La HU puede quedar como “aprobada con observaciones” solo si los hallazgos:

- no comprometen seguridad,
- no comprometen tenant isolation,
- no comprometen integridad,
- no rompen el flujo principal,
- y pueden corregirse sin rediseñar el cambio.

### Ejemplos
- mejora de naming,
- simplificación menor,
- reorganización pequeña de código,
- test adicional recomendable pero no crítico,
- nota documental secundaria.

---

## 11. Cómo emitir el veredicto

Usa una conclusión proporcional al riesgo.

### Aprobada
Cuando:
- no hay hallazgos críticos,
- no hay hallazgos importantes sin resolver,
- el cambio está listo para cierre.

### Aprobada con observaciones
Cuando:
- el flujo está correcto,
- no hay riesgos críticos,
- pero hay mejoras recomendables o faltantes menores.

### Requiere cambios
Cuando:
- hay hallazgos importantes,
- el cambio aún no debería cerrarse,
- pero no está bloqueado por un riesgo catastrófico.

### Bloqueada
Cuando:
- hay riesgo crítico,
- no debe aprobarse hasta corregirlo.

---

## 12. Matriz rápida de severidad por categoría

| Categoría | Riesgo bajo | Riesgo medio | Riesgo alto |
|---|---|---|---|
| Arquitectura | Observación o importante | Importante | Importante o crítico |
| CQRS | Observación o importante | Importante | Importante o crítico |
| Tenant isolation | Poco frecuente | Importante | Crítico |
| Seguridad | Observación o importante | Importante | Crítico |
| Performance | Observación | Importante | Importante o crítico |
| Testing | Observación o importante | Importante | Importante o crítico |
| Documentación | Observación | Observación o importante | Importante |

---

## 13. Preguntas finales antes de cerrar la revisión

Antes de emitir el resultado, responder internamente:

1. ¿Hay riesgo real para negocio o seguridad?
2. ¿Hay riesgo de datos cruzados entre tenants?
3. ¿El flujo principal está realmente completo?
4. ¿El cambio quedó en la capa correcta?
5. ¿Las pruebas cubren lo esencial?
6. ¿La HU puede pasar a cierre documental sin generar caos?
7. ¿Estoy priorizando los hallazgos que realmente importan?

---

## 14. Checklist final resumida

- [ ] Clasifiqué el nivel de riesgo de la HU
- [ ] Ajusté la profundidad de la revisión al riesgo real
- [ ] Revisé arquitectura
- [ ] Revisé CQRS
- [ ] Revisé tenant isolation
- [ ] Revisé seguridad
- [ ] Revisé performance
- [ ] Revisé testing
- [ ] Revisé trazabilidad documental
- [ ] Emití un veredicto claro y proporcional

---

## 15. Criterio rector

La revisión debe ser proporcional al riesgo del cambio y debe priorizar integridad, tenant isolation, seguridad, rendimiento, mantenibilidad y cierre correcto de la HU por encima de observaciones cosméticas.