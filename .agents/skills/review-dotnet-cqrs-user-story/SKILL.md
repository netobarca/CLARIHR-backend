---
name: review-dotnet-cqrs-user-story
description: Usa esta skill cuando una historia de usuario o requerimiento backend en .NET ya fue implementado total o parcialmente y necesitas revisar su calidad técnica, alineación arquitectónica, tenant isolation, seguridad, performance, testing y preparación documental antes de darlo por correcto o cerrarlo. No usar para implementar desde cero ni para cierres documentales exclusivamente.
---

# Review Dotnet CQRS User Story

## 1. Propósito

Esta skill existe para revisar técnicamente una historia de usuario o requerimiento backend en CLARIHR y determinar si el cambio quedó correctamente implementado desde el punto de vista de:

- arquitectura,
- CQRS,
- tenant isolation,
- seguridad,
- rendimiento,
- testing,
- y trazabilidad documental.

Su objetivo es detectar desviaciones, riesgos, faltantes o decisiones incorrectas antes de considerar una HU como terminada.

Esta skill debe ayudar a responder preguntas como:

- ¿la lógica quedó en la capa correcta?
- ¿se respetó Clean Architecture?
- ¿hay riesgo de acceso cross-tenant?
- ¿faltan controles de permisos o seguridad?
- ¿hay problemas evidentes de performance?
- ¿faltan unit tests importantes?
- ¿falta actualizar documentación viva o el cierre de la HU?

---

## 2. Cuándo usar esta skill

Usar esta skill cuando la HU o cambio backend ya existe y necesitas revisar si está lista para aprobarse, corregirse o cerrarse.

### Casos típicos
- “Revisa técnicamente esta HU”
- “Haz code review arquitectónico de este cambio”
- “Valida si esta implementación respeta CQRS y Clean Architecture”
- “Identifica riesgos de tenant, seguridad y performance”
- “Dime si esta historia está lista para cerrar”
- “Haz una revisión integral del cambio antes de aprobarlo”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- implementar la HU desde cero,
- crear únicamente unit tests,
- cerrar únicamente la documentación,
- hacer una reestructuración masiva de arquitectura del repositorio completo,
- documentar decisiones formales tipo ADR sin un cambio concreto que revisar,
- hacer pruebas de carga reales.

Si la tarea principal es implementar, usar:

- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es testing unitario, usar:

- `.agents/skills/unit-test-dotnet-cqrs-user-story/SKILL.md`

Si la tarea principal es cierre documental, usar:

- `.agents/skills/close-user-story-docs/SKILL.md`

---

## 4. Fuentes de verdad obligatorias

Antes de revisar, tomar como referencia en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. la HU o requerimiento fuente
5. convenciones reales del código del repositorio
6. skills relacionadas al flujo:
   - `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`
   - `.agents/skills/unit-test-dotnet-cqrs-user-story/SKILL.md`
   - `.agents/skills/close-user-story-docs/SKILL.md`

Si encuentras contradicción entre código legado y reglas del proyecto, no asumas que el código legado es correcto por existir. Evalúa el cambio contra la base canónica del proyecto.

---

## 5. Principios no negociables de revisión

## 5.1 Revisar contra el estándar del proyecto
La revisión no debe basarse en preferencias personales arbitrarias, sino en:

- Clean Architecture,
- CQRS,
- tenant-scoped by default,
- seguridad por diseño,
- rendimiento por diseño,
- pruebas mínimas suficientes,
- documentación viva sin duplicación.

## 5.2 Priorizar riesgos reales
Debes priorizar hallazgos que afecten:

- integridad del sistema,
- seguridad,
- aislamiento entre tenants,
- corrección funcional,
- rendimiento,
- mantenibilidad,
- trazabilidad.

## 5.3 No sobrerrevisar detalles irrelevantes
No llenar la revisión con observaciones menores sin impacto real si existen problemas más importantes.

## 5.4 Separar severidad
Clasificar mentalmente cada hallazgo como:

- crítico,
- importante,
- recomendado,
- observación menor.

---

## 6. Entradas mínimas esperadas

Para revisar correctamente, identificar o inferir:

- código HU o requerimiento,
- objetivo funcional,
- módulos afectados,
- capas afectadas,
- artefactos modificados,
- endpoints involucrados,
- reglas de negocio principales,
- permisos esperados,
- comportamiento tenant-scoped esperado,
- impacto en persistencia,
- pruebas agregadas o faltantes,
- documentación impactada.

Si algo no está explícito pero puede inferirse con alta confianza del cambio y del proyecto, infiérelo con criterio técnico.

---

## 7. Flujo de revisión

## Paso 1. Entender qué intentaba resolver la HU
Antes de revisar detalles técnicos, entender:

- cuál era el objetivo funcional,
- qué actor ejecuta el flujo,
- qué cambia en el sistema,
- qué reglas de negocio son críticas,
- qué riesgos de seguridad, tenant, permisos y rendimiento implica.

## Paso 2. Identificar qué cambió realmente
Detectar qué capas y artefactos fueron tocados:

- Domain
- Application
- Infrastructure
- API
- Tests
- SQL / Data
- Docs

## Paso 3. Revisar corrección funcional
Confirmar si el cambio realmente cumple el requerimiento y no solo “compila”.

## Paso 4. Revisar arquitectura
Validar si el cambio fue hecho en la capa correcta y sin romper Clean Architecture o CQRS.

## Paso 5. Revisar tenant, seguridad y permisos
Validar si el flujo protege correctamente el acceso, la pertenencia al tenant y los permisos requeridos.

## Paso 6. Revisar rendimiento
Evaluar si hay decisiones claramente riesgosas en queries, listados, persistencia o request path.

## Paso 7. Revisar pruebas
Evaluar si hay pruebas suficientes y si realmente cubren comportamiento crítico.

## Paso 8. Revisar documentación y preparación de cierre
Evaluar si la HU ya está lista para cierre documental o si faltan documentos vivos o trazabilidad.

## Paso 9. Emitir resultado claro
Entregar una revisión concreta con:

- estado general,
- hallazgos,
- severidad,
- acciones requeridas,
- y veredicto final.

---

## 8. Qué debes revisar exactamente

## 8.1 Revisión funcional
Validar:

- si la HU cumple el alcance esperado,
- si faltan casos importantes,
- si hay comportamiento incorrecto,
- si el flujo principal funciona conceptualmente,
- si los errores relevantes están contemplados.

### Señales de alerta
- parte del criterio de aceptación no implementado,
- flujo incompleto,
- comportamiento ambiguo,
- errores importantes no manejados.

---

## 8.2 Revisión arquitectónica
Validar:

- si la lógica de negocio quedó en Domain o Application cuando corresponde,
- si controllers están delgados,
- si API no accede directamente a EF o persistencia,
- si Infrastructure no contiene reglas de negocio puras,
- si DTOs no contaminan Domain,
- si el caso de uso está correctamente modelado como Command o Query.

### Señales de alerta
- lógica en controller,
- lógica de dominio en repositorios o services técnicos,
- Query que modifica estado,
- Command usado para lectura pura,
- acoplamiento incorrecto entre capas,
- dependencias invertidas de forma incorrecta.

---

## 8.3 Revisión CQRS
Validar:

- si la intención del caso de uso está bien clasificada,
- si Commands cambian estado y Queries no lo hacen,
- si los handlers representan un caso de uso claro,
- si las lecturas proyectan a DTOs,
- si no hay mezcla innecesaria de responsabilidades.

### Señales de alerta
- handler híbrido difícil de entender,
- Query con side effects,
- Command retornando lectura compleja que debió separarse,
- lógica mezclada de lectura y escritura sin necesidad.

---

## 8.4 Revisión de multi-tenant
Validar:

- si todas las lecturas y escrituras consideran tenant,
- si el recurso está filtrado correctamente por `TenantId`,
- si no puede haber acceso cross-tenant,
- si el flujo evita filtrar por ids globales sin tenant scope,
- si hay coherencia entre claim `tid`, aplicación y persistencia.

### Señales de alerta
- búsqueda por id sin tenant,
- update/delete sin validar pertenencia,
- query que puede devolver datos de otro tenant,
- join o lookup sin filtro tenant-first,
- permisos revisados pero tenant ignorado.

---

## 8.5 Revisión de seguridad
Validar:

- autenticación requerida cuando aplique,
- autorización correcta por rol / permiso / acción,
- ownership o alcance cuando aplique,
- exposición mínima de datos,
- manejo adecuado de errores,
- protección de datos sensibles,
- auditoría en acciones críticas.

### Señales de alerta
- endpoint sin autorización cuando debería tenerla,
- permiso faltante,
- PII expuesta innecesariamente,
- mensajes que revelan demasiado,
- falta de auditoría en acciones sensibles,
- confiar solo en validación del frontend.

---

## 8.6 Revisión de rendimiento
Validar:

- paginación en listados,
- uso de proyección a DTO,
- `AsNoTracking()` en queries cuando aplica,
- ausencia de N+1 evidente,
- ausencia de includes innecesarios,
- filtros aplicados temprano,
- request path razonable,
- impactos potenciales de índices o consultas.

### Señales de alerta
- endpoint de listado sin paginación,
- carga completa de entidades cuando bastaba un DTO,
- consultas potencialmente costosas sin necesidad,
- proceso pesado dentro del request,
- acceso repetido innecesario a datos.

---

## 8.7 Revisión de persistencia / SQL
Validar:

- si los cambios de datos son coherentes,
- si la estrategia tenant-scoped se mantiene,
- si índices o constraints relevantes fueron considerados,
- si no se rompió integridad,
- si existe trazabilidad del cambio en datos cuando aplica.

### Señales de alerta
- cambio estructural sin reflejo técnico claro,
- constraint faltante,
- índice necesario no considerado,
- persistencia ambigua o inconsistente.

---

## 8.8 Revisión de testing
Validar:

- si existen unit tests suficientes,
- si cubren happy path,
- si cubren errores importantes,
- si cubren permisos cuando aplica,
- si cubren tenant scope cuando aplica,
- si cubren reglas críticas del negocio,
- si los tests son realmente unitarios y legibles.

### Señales de alerta
- no hay tests y el cambio es relevante,
- tests superficiales,
- tenant no probado en un caso sensible,
- permisos no probados cuando son críticos,
- tests que no validan ErrorCode o Result correctamente.

---

## 8.9 Revisión documental
Validar:

- si el cambio requiere actualización documental,
- si la HU ya tiene trazabilidad suficiente,
- si falta actualizar API reference,
- si falta actualizar análisis de arquitectura, seguridad, performance o testing,
- si el cierre documental está pendiente.

### Señales de alerta
- cambio fuerte sin actualización documental prevista,
- API cambió pero no hay referencia,
- flujo sensible cambió y no se prevé actualización de docs,
- se generó o se pretende generar documentación duplicada.

---

## 9. Qué debe producir la revisión

La revisión debe dejar una salida clara y accionable.

## 9.1 Resumen general
Indicar si la HU está:

- lista,
- lista con observaciones menores,
- no lista / requiere correcciones,
- o bloqueada por hallazgos críticos.

## 9.2 Hallazgos
Para cada hallazgo, indicar:

- categoría,
- severidad,
- descripción clara,
- impacto,
- recomendación concreta.

### Categorías sugeridas
- funcional
- arquitectura
- CQRS
- tenant
- seguridad
- performance
- testing
- documentación

## 9.3 Veredicto final
El veredicto debe responder claramente:

- ¿puede aprobarse?
- ¿puede pasar a cierre documental?
- ¿requiere corrección antes de continuar?
- ¿hay riesgo crítico que bloquea el cierre?

---

## 10. Formato recomendado de salida

Usar una estructura como esta al revisar:

### Estado general
- [Aprobada / Aprobada con observaciones / Requiere cambios / Bloqueada]

### Hallazgos críticos
- [si existen]

### Hallazgos importantes
- [si existen]

### Recomendaciones
- [lista concreta]

### Validación de áreas
- Arquitectura: [OK / Observaciones / Falla]
- CQRS: [OK / Observaciones / Falla]
- Tenant isolation: [OK / Observaciones / Falla]
- Seguridad: [OK / Observaciones / Falla]
- Performance: [OK / Observaciones / Falla]
- Testing: [OK / Observaciones / Falla]
- Documentación: [OK / Observaciones / Falla]

### Veredicto final
- [conclusión breve y directa]

---

## 11. Reglas de severidad

## 11.1 Crítico
Usar cuando el problema puede causar:

- fuga cross-tenant,
- omisión grave de permisos,
- corrupción de datos,
- violación fuerte de arquitectura que compromete el sistema,
- error funcional severo,
- riesgo serio de seguridad.

## 11.2 Importante
Usar cuando el problema:

- no bloquea inmediatamente el sistema,
- pero sí afecta mantenibilidad, corrección, testing o performance de forma relevante,
- o deja la HU incompleta para estándares del proyecto.

## 11.3 Recomendado
Usar cuando:

- mejora claridad, consistencia o robustez,
- pero no bloquea el cierre si todo lo demás está correcto.

## 11.4 Menor
Usar solo para detalles secundarios.

No llenes una revisión con observaciones menores si existen hallazgos más relevantes.

---

## 12. Qué evitar durante la revisión

Evitar siempre:

- hacer comentarios vagos sin acción concreta,
- opinar por preferencia personal sin anclar al estándar del proyecto,
- exigir sobreingeniería,
- ignorar tenant o seguridad en módulos sensibles,
- aprobar cambios solo porque compilan,
- convertir una revisión en reimplementación completa del feature,
- pedir documentación duplicada.

---

## 13. Checklist de revisión

Antes de cerrar la revisión, validar:

- [ ] Entendí el objetivo funcional de la HU
- [ ] Revisé el cambio contra `project-foundation.md`
- [ ] Validé arquitectura por capa
- [ ] Validé CQRS
- [ ] Validé tenant isolation
- [ ] Validé permisos y seguridad
- [ ] Validé riesgos básicos de performance
- [ ] Validé testing suficiente
- [ ] Validé necesidades documentales
- [ ] Clasifiqué correctamente la severidad de hallazgos
- [ ] Dejé un veredicto claro

---

## 14. Resultado esperado de esta skill

Al usar esta skill, debes dejar una evaluación clara que permita decidir si la HU:

- puede aprobarse,
- necesita ajustes pequeños,
- requiere correcciones importantes,
- o debe bloquearse hasta resolver riesgos críticos.

La salida debe ser útil tanto para un developer como para una revisión técnica de liderazgo.

---

## 15. Criterio rector

Revisa siempre con este criterio:

**validar si la HU quedó correcta, segura, tenant-scoped, mantenible, testeada y lista para cierre, priorizando riesgos reales sobre observaciones cosméticas.**
