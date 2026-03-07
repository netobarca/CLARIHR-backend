# Remediation Plan

## Status

No se detecto un blocker critico nuevo de arquitectura, seguridad o testing introducido por HU-014.

## Follow-up Candidates (Non-Infrastructure)

1. Partir `SalaryTabulatorAdministration.cs` por casos de uso para reducir complejidad.
2. Extraer generacion de exportes `csv/xlsx` desde controller a servicio de aplicacion dedicado.
3. Endurecer reglas de vigencia para escenarios complejos de traslape historico/futuro con mayor cobertura de pruebas.
4. Extender auditoria de detalle para asociar explicitamente cada item de solicitud con lineas afectadas.
5. Preparar extension de workflow multinivel sin romper contratos de API v1.
