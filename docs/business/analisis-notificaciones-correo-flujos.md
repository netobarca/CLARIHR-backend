# Análisis — Envío de correos (y notificaciones) : la necesidad diferida por decisión de negocio, flujo por flujo

| | |
|---|---|
| **Estado** | ANÁLISIS — corte 2026-07-16 |
| **Qué es** | Inventario, **flujo por flujo**, de la necesidad de envío de correos que quedó **fuera de esta fase por decisión de negocio**. No trata de la boleta de nómina (eso ya está documentado como F2 de REQ-012): trata de todos los demás puntos del sistema — empezando por el caso canónico: **al crear un empleado, debe recibir su correo de afiliación y sus credenciales para ingresar**, y hoy no lo recibe. |
| **Método** | Verificado contra el código real (interfaces, stubs, puntos de invocación, endpoints de ciclo de vida) + los diferidos documentados en los análisis de negocio. Referencias `archivo:línea`. |
| **Documento hermano** | `docs/pendientes-proyecto-16072026.md` (pendientes de despliegue/negocio); §8 de ese doc lista las demás necesidades diferidas — aquí se profundiza la de correo. |

---

## 0. Resumen ejecutivo

1. **No existe envío de correos NI notificaciones in-app en todo el sistema.** La búsqueda de infraestructura de notificaciones en `src/` devuelve **cero** resultados. El único canal por el que alguien "se entera" de algo hoy es **entrar a consultar una bandeja** (pull) o leer el log de auditoría.
2. **La necesidad ya está reconocida en el diseño**: existen **2 contratos de correo** con **6 correos ya cableados** en los flujos — pero ambos contratos están implementados por **stubs que solo escriben al log** (`LoggingEmailService`, `LoggingAuthEmailService`). El día que se conecte un proveedor real, esos 6 correos salen **sin tocar ningún flujo**.
3. **El caso canónico está roto de punta a punta para usuarios locales**: al publicar el expediente se aprovisiona el usuario, se genera contraseña temporal y un token de invitación con **72 horas de vida** — y el token muere en el log (que además solo guarda un *preview* de 8 caracteres). El empleado nunca recibe nada; el endpoint de aceptación existe y es inalcanzable.
4. **Hoy se sobrevive por dos vías**: (a) el login federado — `AuthProvider` soporta **Google/Microsoft/Apple** (`src/CLARIHR.Domain/Auth/AuthProvider.cs:3-9`), así que un empleado con `institutionalEmail` de Google entra por OAuth **sin necesitar la invitación**; (b) operación manual de RRHH. Los usuarios **locales** (contraseña propia) quedan varados: ni invitación, ni verificación de email, ni reset de contraseña autoservicio.
5. Más allá de identidad/acceso, el sistema tiene **~85 endpoints de transición de estado** (solicitud → autorización/decisión → cierre/anulación) repartidos en todos los módulos: cada uno es un punto donde hoy **ni el empleado ni el autorizador reciben aviso**. El inventario completo está en §2.

---

## 1. Lo que existe HOY en el código (verificado)

### 1.1 Los dos contratos y sus stubs

| Contrato | Correos que define | Implementación actual | Registro DI |
|---|---|---|---|
| `IEmailService` (`src/CLARIHR.Application/Abstractions/Companies/IEmailService.cs`) | `SendCompanyUserInvitationAsync` — invitación/afiliación con token (kinds: `Invitation`, `ResetInvitation`) | **`LoggingEmailService`** — escribe al log email, kind, expiración y un **preview del token (4…4 chars)**; el token completo NO queda ni en el log (`src/CLARIHR.Infrastructure/Companies/LoggingEmailService.cs:8-23`) | `DependencyInjection.cs:129` |
| `IAuthEmailService` (`src/CLARIHR.Application/Abstractions/Auth/IAuthEmailService.cs`) | `SendPasswordResetAsync` (link de reseteo) · `SendEmailVerificationAsync` (link de verificación) | **`LoggingAuthEmailService`** (`src/CLARIHR.Infrastructure/Auth/LoggingAuthEmailService.cs`) — solo log | (mismo bloque DI) |

### 1.2 Los 6 correos ya cableados (hoy mueren en el log)

| # | Correo | Se dispara desde | Referencia |
|---|---|---|---|
| 1 | **Invitación de afiliación** (token 72 h) | Publicar expediente (`PATCH personnel-files/{id}/finalize`) y **recontratación** (`POST …/rehire`) → `PersonnelFileFinalizationService` → provisioning | `CompanyUserProvisioningService.cs:196` |
| 2 | Invitación al **crear usuario de empresa manualmente** | `POST company-users` | `CreateCompanyUser.cs:196` |
| 3 | **Re-invitación** (re-emite token, revoca los previos) | `POST company-users/{userId}/reset-invitation` | `ResetInvitation.cs:131` |
| 4 | **Verificación de email** al registrarse | `RegisterUser` (2 rutas internas) | `RegisterUserCommand.cs:151,176` |
| 5 | Re-envío de **verificación de email** (administración) | `EmailVerificationAdministration` | `EmailVerificationAdministration.cs:159` |
| 6 | **Reset de contraseña** (link con expiración) | `PasswordResetAdministration` | `PasswordResetAdministration.cs:125` |

### 1.3 Anatomía del caso canónico: alta de empleado → afiliación y credenciales

Flujo real verificado (`PersonnelFileFinalizationService.cs` + `CompanyUserProvisioningService.cs`):

1. RRHH publica el expediente: `PATCH api/v1/personnel-files/{publicId}/finalize` (`PersonnelFileEmploymentController.cs:56`) con `createUserAccount=true` y un rol resuelto. La recontratación (`POST …/rehire`, `:84`) pasa por el MISMO servicio y reusa la membresía anterior (D-09).
2. El provisioning crea el `User` con **contraseña temporal** de 16 caracteres criptográficamente aleatoria (`CreateTemporaryPassword`, `CompanyUserProvisioningService.cs:352-390`), la persiste hasheada, crea membresía + usuario IAM con su rol, y emite un **`InvitationToken`** hasheado con expiración **`InvitationExpirationHours = 72`** (`CompanyUserConstants.cs:5`), revocando los anteriores.
3. Llama `SendCompanyUserInvitationAsync(email, nombre, empresa, token, expiración)` → **`LoggingEmailService` solo loguea** (con el token truncado a preview).
4. Existe el endpoint público de aceptación — `POST auth/company-user-invitations/accept` (`AuthController.cs:291`) — que es donde el empleado activaría su cuenta… **pero el token nunca viaja a ninguna parte**: no está en la respuesta del API (solo `invitationExpiresUtc`), no está completo en el log, y no hay correo.
5. Se audita `UserInvited` correctamente (`CompanyUserProvisioningService.cs:212-225`) — la trazabilidad existe; la **entrega** no.

**Consecuencias operativas hoy:**
- Empleado con email federado (Google/Microsoft/Apple): entra por OAuth y el problema pasa desapercibido — **por eso el sistema es usable en pruebas**.
- Empleado con cuenta **Local**: no puede activarse jamás por autoservicio. El token expira a las 72 h sin que nadie lo haya visto. La re-invitación (`reset-invitation`) re-emite otro token… que muere igual.
- **Reset de contraseña autoservicio: imposible** para usuarios locales (el link solo va al log). Workaround actual: intervención manual de un admin.
- **Verificación de email: imposible** por la misma razón.
- Si se edita el `institutionalEmail` del expediente, el login se re-sincroniza (comportamiento ya construido) — pero tampoco dispara ninguna notificación al empleado.

### 1.4 Lo que NO existe (y habrá que decidir/construir cuando se priorice)

- Proveedor real de correo (SMTP/API) y su configuración por entorno.
- Plantillas de correo (HTML/texto, ES/EN).
- Despachador asíncrono (cola/outbox) — hoy los 6 envíos se invocan inline en el flujo.
- Notificaciones in-app (campana), digest, y preferencias de notificación por empresa/usuario.
- Cualquier notificación en los ~85 flujos de decisión de §2 (bloques B–F): ahí **ni siquiera hay llamada a un stub** — el punto de enganche habría que agregarlo.

---

## 2. Inventario flujo por flujo de la necesidad de correo

Convención de las tablas: **Flujo/evento** (endpoint real) → **quién debería recibirlo** → **qué pasa hoy**.

### 2.1 Bloque A — Identidad y acceso (el correo YA está cableado; solo falta el proveedor) — CRÍTICO

| Evento | Destinatario | Qué pasa hoy |
|---|---|---|
| Expediente publicado con cuenta → **correo de afiliación + activación** (token 72 h) | Empleado nuevo | Muere en el log (§1.3) |
| **Recontratación** — cuenta reactivada o re-invitada | Empleado recontratado | Reactivación silenciosa; si estaba pendiente de activar, nueva invitación al log |
| Usuario de empresa creado manualmente / **re-invitación** | Usuario invitado | Igual |
| **Verificación de email** (registro y re-envío) | Usuario registrado | Link al log — inverificable |
| **Reset de contraseña** | Usuario local | Link al log — irrecuperable por autoservicio |

### 2.2 Bloque B — Solicitudes/eventos del EMPLEADO: debería enterarse él (hoy: solo si entra a mirar o RRHH le avisa por fuera)

| Flujo (endpoint) | Evento que amerita correo al empleado |
|---|---|
| Ayuda económica — `PATCH …/economic-aid-requests/{id}/resolution` · `/disbursement` · `/cancel` | Su solicitud fue aprobada/denegada (con motivo) · su desembolso se registró (D-16 del análisis lo dejó explícitamente diferido) |
| Constancias — `PATCH …/certificate-requests/{id}/issue` · `/cancel` | **Su constancia está emitida y lista para descargar** (el PDF ya existe — adjuntarlo es viable) · su solicitud fue cancelada |
| Retiro — `PATCH …/retirement-requests/{id}/resolution` · `/cancel` | Su renuncia/retiro fue autorizado o devuelto (D-17 lo difirió) |
| Liquidación — `PATCH …/settlements/{id}/issuance` | Su finiquito fue **EMITIDO** (boleta PDF ya existe — candidata a adjuntarse) |
| Horas extras — `PATCH …/overtime-records/{id}/resolution` · `/revocation` | Su registro de HE fue autorizado/denegado/revocado |
| Vacaciones — `PATCH …/vacation-requests/{id}/decision` · `/cancellation` · `POST …/returns` | Su solicitud fue decidida/devuelta (el flujo de solicitud EN LÍNEA es F2 — D-01 —, pero la decisión ya existe como endpoint y el aviso aplica desde F1) |
| Incapacidades — `PATCH …/incapacities/{id}/closure` · `/annulment` | Registro/cierre/anulación de su incapacidad (afecta su pago) |
| Reconocimientos — `PATCH …/recognitions/{id}/decision` | Le fue otorgado un reconocimiento |
| Amonestaciones — `PATCH …/disciplinary-actions/{id}/decision` | Se decidió una amonestación en su contra (debido proceso: derecho a saberlo; más aún si `HasPayrollDeduction`) |
| Tiempos no trabajados — `POST/PATCH …/not-worked-times` | Se registró una ausencia que le **descuenta** (incl. séptimo) |
| Tiempo compensatorio — créditos/consumos y anulaciones | Movimientos de su saldo de horas |
| Ingresos/descuentos (REQ-005/006/008/009) — resoluciones y anulaciones sobre su expediente | Se autorizó/anuló un ingreso o descuento recurrente/eventual a su nombre |
| Sustitución de autorizaciones — `POST/PUT/PATCH …/authorization-substitutions` | **El sustituto** debería enterarse de que fue designado (y el sustituido, del registro) — D-11 lo dejó fuera |
| Entrevista de retiro — `PUT …/exit-interview/submission` | Invitación a completar su entrevista al registrarse su retiro |
| Reclamos de seguro — `PATCH …/medical-claims/{id}` | Cambios que RRHH registra sobre su reclamo |
| Seguros/beneficiarios, equipo asignado, etc. | Avisos informativos de cambios en su expediente (segunda prioridad) |

### 2.3 Bloque C — Bandejas de AUTORIZACIÓN: debería enterarse el autorizador (hoy: 100 % pull — nadie sabe que tiene trabajo pendiente)

| Bandeja | Disparador del correo |
|---|---|
| Ingresos cíclicos/eventuales (`AuthorizeRecurringIncomes`/`AuthorizeOneTimeIncomes`) | Nueva solicitud PENDIENTE creada → aviso (o digest) al rol autorizador — los análisis de REQ-005/006 lo marcan F2 |
| Descuentos cíclicos/eventuales (`AuthorizeRecurringDeductions`/`AuthorizeOneTimeDeductions`) | Ídem (REQ-008/009) |
| Horas extras (`AuthorizeOvertimeRecords`) | Nuevo registro por autorizar; lote por periodo próximo a vencer |
| Retiros (`AuthorizeRetirement`) / Recontratación (`AuthorizeRehire`) | Nueva solicitud de retiro / solicitud de rehire |
| Reconocimientos/amonestaciones (REQ-003, anti-self) | Nuevo registro por decidir |
| Ayuda económica | Nueva solicitud por resolver |
| Reclamos de seguro — `POST …/medical-claims` (gate self-service) | **El empleado creó un reclamo** → RRHH debería enterarse sin entrar a la bandeja |
| Constancias — `POST …/certificate-requests` | Nueva solicitud (con su `ResponseTimeDays` corriendo — las alertas de SLA son D-14/F2) |
| Endeudamiento | Registro que **excede el límite** configurado (advertir-nunca-bloquear): hoy el warning solo viaja en la respuesta HTTP de quien registró; el analista no se entera |
| Planilla (`AuthorizePayrollRuns`) | Corrida GENERADA lista para autorizar · corrida DEVUELTA con motivo (avisar al generador) — ya documentado como F2 de REQ-012 |

### 2.4 Bloque D — Entregables y trabajos asíncronos

| Evento | Destinatario | Nota |
|---|---|---|
| Export listo (`report-export-jobs`, Worker) | Quien lo pidió | Hoy el FE hace polling; correo con link de descarga = mejora natural (la infraestructura Worker ya existe y es el molde para el despacho de correos) |
| Constancia emitida / finiquito emitido con PDF adjunto | Empleado | Los PDFs ya se generan (QuestPDF/DocumentModel) |
| Boletas de planilla por correo | Empleado | **F2 explícita de REQ-012 (P-12) — fuera del foco de este análisis, se lista por completitud** |

### 2.5 Bloque E — Recordatorios y vencimientos (requieren además un scheduler)

- Token de invitación **por expirar / expirado sin aceptar** → re-emitir y avisar a RRHH (hoy: nadie lo sabe; el usuario queda varado).
- SLA de constancias (`ResponseTimeDays`) por vencer → RRHH (D-14, F2).
- Incapacidades por vencer / prórrogas; fin de periodo de lactancia.
- Fin de vigencia de sustituciones de autorización.
- Rezagos de planilla detectados en pre-flight (`PAYROLL_WARNING_CARRYOVER_INPUT`) → analista (documentado F2 en REQ-014).
- Nota: hoy **no existe scheduler de negocio** (el Worker procesa colas de export); los recordatorios exigen ese componente además del correo.

---

## 3. Qué implica implementarlo (el delta técnico, cuando el negocio lo priorice)

1. **Decisión de proveedor** (no hay producción aún, la decisión está abierta): Azure Communication Services / SendGrid / SMTP corporativo. Configuración en appsettings **base** + secretos por entorno (mismo trato que `Storage:Purposes`).
2. **Implementar los 2 contratos existentes** con el proveedor real y cambiar 1 línea de DI por cada uno (`DependencyInjection.cs:129`). Con solo esto, **el Bloque A completo queda resuelto sin tocar ningún flujo** — el seam ya está diseñado para eso.
3. **Plantillas** ES/EN (la convención resx EN/ES del repo ya existe) con los datos que los mensajes ya transportan (`CompanyUserInvitationEmailMessage`, `PasswordResetEmailMessage`, `EmailVerificationEmailMessage`).
4. **Despacho post-commit** para los bloques B–F: el envío NUNCA debe viajar dentro de la transacción del flujo (un proveedor caído no puede tumbar una autorización). Patrón outbox/cola sobre el molde ya existente (`ReportExportJob` + `CLARIHR.Worker`), con reintentos y auditoría de envíos.
5. **Puntos de enganche** para B–F: los ~85 endpoints de transición ya identificados (§2) — cada uno emite hoy su evento de auditoría, que es exactamente el catálogo de eventos notificables; se priorizan por bloque, no se construyen de golpe.
6. **Preferencias por empresa** (molde `CompanyPreference`) para encender/apagar familias de correos, y a futuro preferencias por usuario.
7. Consideración de producto: decidir si además del correo se quiere **notificación in-app** (campana) — hoy no existe nada; el mismo outbox serviría a ambos canales.

## 4. Priorización propuesta (a ratificar con el negocio)

| Prioridad | Alcance | Por qué |
|---|---|---|
| **P0** | Bloque A (proveedor + los 6 correos ya cableados) | Desbloquea el ciclo de vida completo de usuarios **locales** (afiliación/credenciales, verificación, reset). Es el caso del levantamiento y el de menor esfuerzo: cero cambios en flujos. |
| **P1** | Bloque B en su núcleo: decisiones sobre solicitudes del empleado (ayuda económica, retiro, HE, vacaciones) + entregables con PDF (constancia emitida, finiquito emitido) | Es donde el empleado hoy depende de que "alguien le avise por fuera". |
| **P2** | Bloque C (avisos/digest a autorizadores) + reclamo self-service → RRHH + endeudamiento excedido + export listo | Reduce la latencia operativa de TODAS las bandejas. |
| **P3** | Bloque E (recordatorios/SLA — requiere scheduler) + boletas de planilla por correo (F2 de REQ-012 ya planificada) + in-app | Valor incremental; dependencias técnicas adicionales. |

## 5. Las demás necesidades excluidas por decisión de negocio en esta fase (mismo patrón, otros temas)

El correo es la más transversal, pero el mismo criterio («se registra y decide en el sistema, la parte en línea/automática quedó para después») aplica a: **aprobación multinivel/delegación efectiva** (todos los flujos usan una sola decisión + anti-self; el punto de extensión reservado es Sustitución RF-010), **autoservicio del empleado ampliado** (solicitudes en línea de vacaciones/TC, renuncia, su liquidación, su boleta), **programación a fecha/hora** (planilla P-14 y cualquier job calendarizado — no hay scheduler), **marcación/biometría**, **integraciones externas** (ERP contable, archivo bancario propietario, aseguradoras, IAM/AD real para equipo-acceso Nivel B), y **notificaciones in-app**. El detalle por módulo con sus referencias D-xx/P-xx está en `docs/pendientes-proyecto-16072026.md` §8.

---

## Trazabilidad de la decisión

La exclusión de correos en esta fase está registrada de forma consistente en los análisis ratificados: REQ-001 D-01 (flujo en línea + notificaciones diferidos por el propio levantamiento) · ayuda económica D-16 · retiro definitivo D-17 · sustitución D-11/G-11 · constancias D-14 (SLA) · seguros D-11 · REQ-005/006/007/008 (notificaciones a autorizadores = F2) · REQ-012 («correo real NO existe — stubs `LoggingEmailService`; boletas por email = F2», P-12) · REQ-014 (aviso de rezagos = F2) · entrevista de retiro (F3). Este documento las consolida y las baja al nivel de flujo/endpoint para que la fase de correos se pueda estimar y ratificar completa, de una sola vez.
