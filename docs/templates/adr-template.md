# ADR-XXXX — [Título corto de la decisión]

- **Estado:** [Propuesto / Aprobado / Rechazado / Reemplazado / Obsoleto]
- **Fecha:** [YYYY-MM-DD]
- **Autores:** [Nombre / equipo / agente]
- **Relacionado con:** [HU-XXXX, épica, requerimiento, incidente, iniciativa]
- **Reemplaza:** [ADR-XXXX, si aplica]
- **Reemplazado por:** [ADR-XXXX, si aplica]

---

## 1. Título

[Escribir un título corto, claro y específico de la decisión.]

Ejemplos:
- Usar BIGINT interno y UUID público para entidades expuestas
- Implementar tenant scoping mediante claim `tid` y filtros globales
- Estandarizar respuestas de error con Result + ProblemDetails

---

## 2. Contexto

Describir el contexto que obliga a tomar esta decisión.

Incluir, según aplique:

- problema a resolver,
- restricciones técnicas,
- restricciones de negocio,
- preocupaciones de seguridad,
- preocupaciones de rendimiento,
- impacto arquitectónico,
- impacto en mantenimiento,
- dependencia con otros módulos o decisiones previas.

### Contexto resumido
[Explicación clara del problema o necesidad.]

### Situación actual
[Qué existe hoy y por qué no es suficiente.]

### Motivadores
- [Motivador 1]
- [Motivador 2]
- [Motivador 3]

---

## 3. Decisión

Describir claramente la decisión tomada.

### Decisión adoptada
[Escribir la decisión de forma directa.]

### Alcance de la decisión
Indicar dónde aplica:
- [ ] Todo el sistema
- [ ] Un módulo específico
- [ ] Una capa específica
- [ ] Una integración específica
- [ ] Un flujo específico

### Reglas derivadas
- [Regla 1]
- [Regla 2]
- [Regla 3]

---

## 4. Alternativas evaluadas

Documentar las principales alternativas consideradas.

### Alternativa 1
**Nombre:** [Nombre corto]

**Descripción:**  
[Resumen de la alternativa.]

**Ventajas:**
- [Ventaja 1]
- [Ventaja 2]

**Desventajas:**
- [Desventaja 1]
- [Desventaja 2]

**Razón de descarte o aceptación parcial:**  
[Explicación.]

---

### Alternativa 2
**Nombre:** [Nombre corto]

**Descripción:**  
[Resumen de la alternativa.]

**Ventajas:**
- [Ventaja 1]
- [Ventaja 2]

**Desventajas:**
- [Desventaja 1]
- [Desventaja 2]

**Razón de descarte o aceptación parcial:**  
[Explicación.]

---

### Alternativa 3
**Nombre:** [Nombre corto]

**Descripción:**  
[Resumen de la alternativa.]

**Ventajas:**
- [Ventaja 1]
- [Ventaja 2]

**Desventajas:**
- [Desventaja 1]
- [Desventaja 2]

**Razón de descarte o aceptación parcial:**  
[Explicación.]

---

## 5. Justificación

Explicar por qué la alternativa elegida es la más adecuada.

### Razones principales
- [Razón 1]
- [Razón 2]
- [Razón 3]

### Factores considerados
- [ ] Simplicidad
- [ ] Mantenibilidad
- [ ] Seguridad
- [ ] Rendimiento
- [ ] Escalabilidad
- [ ] Coste de implementación
- [ ] Tiempo de entrega
- [ ] Compatibilidad con arquitectura actual
- [ ] Multi-tenant
- [ ] Observabilidad
- [ ] Testing
- [ ] Otro: [especificar]

### Resumen de justificación
[Explicación concreta de por qué se toma esta decisión.]

---

## 6. Consecuencias

Describir lo que cambia a partir de esta decisión.

### Consecuencias positivas
- [Consecuencia positiva 1]
- [Consecuencia positiva 2]

### Consecuencias negativas o trade-offs
- [Trade-off 1]
- [Trade-off 2]

### Riesgos
- [Riesgo 1]
- [Riesgo 2]

### Impacto técnico
- [Impacto 1]
- [Impacto 2]

### Impacto operativo o documental
- [Impacto 1]
- [Impacto 2]

---

## 7. Impacto por capa o área

Completar según aplique.

### Domain
[Impacto o “No aplica”.]

### Application
[Impacto o “No aplica”.]

### Infrastructure
[Impacto o “No aplica”.]

### API
[Impacto o “No aplica”.]

### Data / SQL
[Impacto o “No aplica”.]

### Security
[Impacto o “No aplica”.]

### Performance
[Impacto o “No aplica”.]

### Testing
[Impacto o “No aplica”.]

### Documentation
[Impacto o “No aplica”.]

---

## 8. Plan de implementación

Describir cómo se aterriza la decisión en el proyecto.

### Cambios requeridos
- [Cambio 1]
- [Cambio 2]
- [Cambio 3]

### Dependencias
- [Dependencia 1]
- [Dependencia 2]

### Orden sugerido
1. [Paso 1]
2. [Paso 2]
3. [Paso 3]

### Validaciones requeridas
- [Validación 1]
- [Validación 2]

---

## 9. Impacto en documentación

Indicar qué documentos deben actualizarse por esta decisión.

### Documentos a actualizar
- `docs/technical/overview/project-foundation.md`
- `docs/analysis/current-state/architecture-analysis.md`
- `docs/analysis/current-state/security-analysis.md`
- `docs/analysis/current-state/performance-analysis.md`
- `docs/analysis/current-state/testing-analysis.md`
- `docs/technical/api/endpoint-reference.md`
- [Otro, si aplica]

### Observación
[Explicar si la decisión modifica reglas vigentes o solo las complementa.]

---

## 10. Impacto en historias de usuario o roadmap

Documentar si esta decisión afecta trabajo actual o futuro.

### HUs impactadas
- [HU-XXXX]
- [HU-YYYY]

### Iniciativas impactadas
- [Iniciativa / épica / módulo]

### Requerimientos futuros habilitados
- [Capacidad futura 1]
- [Capacidad futura 2]

---

## 11. Criterios de aceptación de la decisión

Definir cómo se sabrá que la decisión fue correctamente aplicada.

### Se considerará aplicada correctamente cuando:
- [Criterio 1]
- [Criterio 2]
- [Criterio 3]

### Evidencias esperadas
- [Evidencia 1]
- [Evidencia 2]

---

## 12. Estado de seguimiento

### Estado actual
[Pendiente / En implementación / Parcial / Adoptada / Revertida]

### Próxima revisión
[YYYY-MM-DD] o [No aplica]

### Responsable de seguimiento
[Nombre / equipo / agente]

---

## 13. Notas adicionales

[Agregar aclaraciones, excepciones, decisiones relacionadas o advertencias.]

---

## 14. Referencias

- Foundation document: `docs/technical/overview/project-foundation.md`
- Documento técnico relacionado: [ruta]
- HU relacionada: [ruta o código]
- PR / commit / branch: [referencia]
- ADR relacionada: [ruta]
- Fuente externa o estándar: [referencia]