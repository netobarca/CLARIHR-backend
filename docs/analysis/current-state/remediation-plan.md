# Plan de remediacion y madurez

## 1. Objetivo

Este plan prioriza mejoras tecnicas y documentales visibles a partir del estado actual del proyecto.

## 2. Prioridades

| Prioridad | Iniciativa | Motivo | Estado |
|---|---|---|---|
| Alta | Automatizar export de OpenAPI desde Swagger runtime | Hoy existe Swagger en `Development`, pero no contrato versionado en docs | Pendiente |
| Alta | Introducir rate limiting para auth y endpoints costosos | La postura de seguridad actual no muestra proteccion anti abuso explicita | Pendiente |
| Alta | Definir criterio para sacar exportes grandes del request path | Hay multiples exportes y diagramas sin background processing | Pendiente |
| Media | Formalizar observabilidad por modulo y endpoint | Hace falta medir latencia, volumen y errores por rutas criticas | Pendiente |
| Media | Agregar pruebas de contrato API o snapshots de Swagger | Reduciria drift entre codigo y documentacion tecnica | Pendiente |
| Media | Revisar estrategia de cache distribuido para permisos por campo | El cache actual existe, pero su modo distribuido requiere decision operativa formal | Pendiente |
| Media | Documentar estrategia de migraciones y validarla en CI | Los integration tests actuales usan `EnsureCreatedAsync()` | Pendiente |
| Baja | Endurecer set de security headers segun entorno y superficie real | El set actual es minimo y correcto, pero no exhaustivo | Pendiente |

## 3. Orden recomendado

1. OpenAPI versionado
2. rate limiting
3. observabilidad y endpoints costosos
4. pruebas de contrato
5. cache y migraciones

## 4. Criterio de cierre

Cada iniciativa deberia cerrarse solo cuando:

- el codigo este implementado
- la documentacion viva este actualizada
- haya validacion automatizada o evidencia operativa minima
- el cambio quede trazable en `docs/analysis/changes/`
