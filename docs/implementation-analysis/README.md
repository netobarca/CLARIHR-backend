# Implementation Analysis

## Purpose

Esta carpeta centraliza el analisis detallado posterior al desarrollo para cada historia de usuario e iteracion.

Cada documento debe registrar al menos:

- seguridad
- performance
- arquitectura
- testing
- riesgos residuales
- siguientes pasos

## Structure

- `history-and-iterations/`: documentos finales por HU o iteracion.
- `templates/`: plantillas reutilizables para mantener consistencia.

## Recommended Flow

Despues de cada HU o iteracion:

1. Ejecutar validacion tecnica minima:
   - `dotnet build CLARIHR.slnx`
   - `dotnet test CLARIHR.slnx --no-build`
2. Crear el analisis narrativo usando `templates/post-implementation-analysis-template.md`.
3. Crear el checklist ejecutable usando `templates/post-implementation-validation-checklist-template.md`.
4. Guardar ambos artefactos dentro de `history-and-iterations/`.
5. Dejar riesgos residuales y siguientes pasos explicitamente documentados.

## Naming convention

Usar nombres estables y faciles de rastrear, por ejemplo:

- `HU-006-post-implementation-analysis.md`
- `iteration-04-post-implementation-analysis.md`
- `HU-008-iteration-05-post-implementation-analysis.md`
