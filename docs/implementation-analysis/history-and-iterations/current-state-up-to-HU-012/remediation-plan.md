# Remediation Plan

## Status

No se detecto un blocker critico nuevo de arquitectura, seguridad o testing introducido por HU-011/HU-012.

## Follow-up Candidates (Non-Infrastructure)

1. Partir `JobProfileAdministration.cs` en slices por caso de uso para reducir complejidad ciclomatica y facilitar mantenimiento.
2. Optimizar lecturas de detalle de JobProfiles con proyecciones especificas para `get`, `print` y `vacancy-template` evitando sobre-hidratacion cuando no se requiere todo el grafo.
3. Extender pruebas de integracion negativas para endpoints por `{id}` en JobCatalogs y OrgUnits, cubriendo consistentemente `NotFound` vs `TenantMismatch`.
4. Agregar pruebas de reglas limite:
   - profundidad maxima de OrgUnits (`15`)
   - escenarios de publicacion incompleta y estado `Archived` en JobProfiles
5. Mantener sincronizacion documental por HU en:
   - `docs/technical/api-reference/api-endpoints-reference.md`
   - `docs/technical/api-output/*.md`
   - `docs/technical/postman-collection/*`
