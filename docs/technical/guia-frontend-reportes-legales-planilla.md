# Guía Frontend — Reportes legales de planilla (REQ-016: F-14, Planilla Única, Planilla Patronal)

> **Fuente de verdad del contrato**: `docs/technical/api/openapi.yaml` (volcado del swagger real — **pendiente de regenerar tras este cierre**, ver §5). Esta guía mapea las superficies, los flujos y las trampas; los esquemas exactos viven en el contrato.

## 0. Convenciones (heredadas del resto de la API — nada nuevo aquí)

- Base `api/v1`. Identidades públicas: todo id en el wire es `publicId`/`xxxPublicId`.
- **Concurrencia**: `If-Match` con el `concurrencyToken` vigente en cada mutación (falta → `400`, viejo → `409`). El token viaja en el body y en el header `ETag`.
- **Errores**: ProblemDetails con el código estable en `code`. Mensajes bilingües EN/ES vía `Accept-Language`.
- Los 3 reportes se descargan como archivo (`GET .../export?format=xlsx|csv|json`), igual que las bandejas ya existentes de REQ-013 — mismo mecanismo, mismo límite de filas síncronas.

## 1. Perfil legal patronal (RF-006) — prerequisito de los 3 reportes Y de generar nómina

```
GET   companies/{companyId}/legal-profile           ← 404 si la empresa no lo ha configurado todavía
POST  companies/{companyId}/legal-profile           ← crea (409 COMPANY_LEGAL_PROFILE_ALREADY_EXISTS si ya existe)
PUT   companies/{companyId}/legal-profile           ← reemplaza (If-Match obligatorio)
```

Body (POST/PUT): `{ legalName, employerNitNumber, isssEmployerRegistrationNumber, fiscalAddress, economicActivityDescription?, legalRepresentativePublicId? }`.

Permiso: el mismo de `company-preferences` (administrador de la empresa) — no es un permiso nuevo.

**Importante para el flujo de onboarding**: mientras el interruptor interno de activación (ver §4) esté apagado —que es el estado por defecto en este cierre—, la ausencia de este perfil **no bloquea nada todavía**. Cuando el equipo lo active por tenant (tras la campaña de captura), `POST companies/{companyId}/payroll-runs` empezará a responder `422 PAYROLL_RUN_MISSING_LEGAL_PROFILE` si el perfil no existe. La UI de generación de planilla debería, de una vez, saber mostrar este código con un enlace directo a la pantalla de configuración del perfil legal.

## 2. Identificación previsional del empleado (RF-007)

- **NUP ISSS**: no es un endpoint nuevo — es un tipo más dentro de las identificaciones ya existentes del expediente (`GET/POST/PUT/PATCH/DELETE .../personnel-files/{publicId}/identifications`). El código de catálogo es `NUP_ISSS`; debe existir en el catálogo `identification_type_catalog_items` del país (país SV) antes de poder capturarse — verificar con el equipo de datos que ya esté sembrado en el ambiente antes de anunciar la funcionalidad a los usuarios.
- **Cuenta AFP**: campo nuevo `afpAccountNumber`, hermano de `afpCode` (ya existente). **Nota para este cierre**: el dominio y la base de datos ya lo soportan, pero el wiring de este campo en los 4 DTOs de shell/employee del expediente (creación/edición) quedó como un follow-up mecánico — mismo patrón exacto de `afpCode` en esos mismos archivos. Hasta que ese follow-up se complete, el campo solo es escribible por una vía de dominio directa, no por los endpoints públicos de edición de personal-info. **No anunciar esta captura a FE hasta que ese follow-up cierre.**

Igual que el perfil legal, la ausencia de estos datos en un empleado no bloquea nada mientras el interruptor de activación esté apagado. Una vez activado, la nómina de ESE empleado específico no se generará (su línea se excluye, el resto de la corrida procede con normalidad) y la corrida traerá un warning de cabecera nuevo: `PAYROLL_WARNING_EMPLOYEE_EXCLUDED_PREVISIONAL_DATA_MISSING` con el `personnelFilePublicId` del excluido — la UI de la bandeja/detalle de planilla (que ya sabe pintar `warnings[]`, REQ-012/013) solo necesita reconocer este código nuevo, no una superficie distinta.

## 3. Los 3 reportes

### 3.1 Planilla Patronal (RF-003) — el único listo con layout final

```
GET companies/{companyId}/payroll-runs/{payrollRunId}/employer-cost-report/export?format=xlsx
```

Por **corrida individual** (`CERRADA` únicamente — un run `GENERADA`/`AUTORIZADA` no aparece en ningún selector de este reporte). Filas: `Empleado, CodigoEmpleado, CentroCosto, SalarioBase, IsssPatronal, AfpPatronal, OtrasCargasPatronales, CostoPatronalTotal, Moneda`. El total de la columna `CostoPatronalTotal` cuadra contra `totalEmployerCost` de la cabecera de la corrida — útil como validación cruzada en la UI si se quiere mostrar un check visual.

Permiso: `ViewComplianceReports` (**nuevo, dedicado** — no `ViewPayrollRuns`). Un usuario que hoy ve la bandeja de planilla puede NO tener acceso a este reporte.

### 3.2 F-14 (RF-001) y Planilla Única (RF-002) — layout tabular por ahora

```
GET companies/{companyId}/compliance-reports/income-tax-withholding/export?year=2026&month=7&format=xlsx
GET companies/{companyId}/compliance-reports/social-security-contributions/export?year=2026&month=7&format=xlsx
```

Ambos **consolidan por mes calendario**, no por corrida — si la empresa corre nómina quincenal, un mes trae datos de 2 corridas juntas (o más, si mezcla frecuencias). El selector de la UI debe pedir **año + mes**, no una corrida puntual como en Planilla Patronal. Un mes sin ninguna corrida `CERRADA` responde 200 con el archivo vacío, no un error — mostrar un estado vacío explícito ("sin corridas cerradas en este mes"), no tratarlo como falla.

Filas F-14: `Empleado, CodigoEmpleado, Nit, SalarioGravableMes, RentaRetenidaMes, Advertencias`.
Filas Planilla Única: `Empleado, CodigoEmpleado, NupIsss, SalarioCotizableMes, IsssEmpleado, IsssPatronal, CodigoAfp, CuentaAfp, AfpEmpleado, AfpPatronal, Advertencias`.

`Advertencias` es texto libre por fila (p. ej. "Sin NIT registrado.", "sin NUP ISSS registrado; sin cuenta AFP registrada.") — no un código estructurado; si la UI quiere resaltar filas con advertencia, basta con `advertencias != null`.

**Layout pendiente de una entrega futura**: estos dos archivos hoy son un detalle tabular plano (incluyen encabezados de columna en español, una fila por empleado) — **todavía NO reproducen el formulario oficial de Hacienda/ISSS celda por celda**. Eso llega en una iteración posterior, cuando el negocio entregue los archivos de plantilla oficial reales. No construir en el FE ninguna suposición sobre posiciones fijas de celda contra el archivo de HOY — el layout cambiará.

Permiso: `ViewComplianceReports` en ambos.

## 4. Interruptor de activación de los 2 gates (operativo, no expuesto en la API pública)

`CompanyPreference.PayrollComplianceGatesEnabled` (nullable bool, `null`/`false` por defecto = apagado) gobierna **ambos** gates (perfil legal patronal a nivel empresa, datos previsionales a nivel empleado) juntos. **No existe ningún endpoint público para prenderlo** — es deliberado (ver plan técnico §2.3/§7 R-T1): encenderlo prematuramente, antes de que la empresa haya completado su perfil legal y sus empleados sus datos previsionales, puede impedir generar nómina de golpe. El equipo lo activa por tenant, vía una acción operativa directa, solo cuando la campaña de captura de datos de ese cliente esté lista. La UI no necesita construir nada para esto — es puramente un estado de despliegue/soporte.

## 5. Pendiente de este cierre — léelo antes de anunciar la funcionalidad

- [ ] `openapi.yaml` no se ha regenerado todavía contra el swagger real con los endpoints de este documento — verificar el contrato exacto antes de tipar el cliente FE contra esta guía.
- [ ] El campo `afpAccountNumber` no es editable todavía por los endpoints públicos de expediente (§2) — no ofrecer el campo en la UI hasta que ese follow-up cierre.
- [ ] Confirmar con el equipo de datos que el catálogo `NUP_ISSS` (`identification_type_catalog_items`, país SV) esté sembrado en cada ambiente antes de anunciar la captura de este dato a los usuarios.
- [ ] F-14 y Planilla Única: el layout es tabular plano, no la plantilla oficial — comunicar esta limitación al usuario final si se libera en este estado.
