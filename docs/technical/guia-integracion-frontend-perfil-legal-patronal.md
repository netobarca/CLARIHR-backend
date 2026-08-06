# Guía Frontend — Perfil legal patronal

Identidad legal del patrono (razón social, NIT patronal, registro patronal ISSS, dirección fiscal).
Es la cabecera de los tres reportes legales de planilla —F-14, Planilla Única y Planilla Patronal— y,
cuando se active el interruptor de cumplimiento, prerequisito para generar nómina.

> Todo lo que sigue está contrastado contra el swagger real de la API, no contra el código fuente ni
> contra la guía anterior. `docs/technical/api/openapi.yaml` fue regenerado y ya incluye estos
> endpoints.

---

## 1. Estado actual: la pantalla existe pero es inalcanzable

El componente ya está construido y desplegado — la ruta `company-legal-profile` carga
`CompanyLegalProfileComponent`. Aun así **no hay forma de llegar a él**, por dos causas
independientes que hay que corregir juntas:

**1. El guard de la ruta pide el permiso equivocado.**

```ts
{ path: "company-legal-profile", canActivate: [t, o(n.PersonnelFiles, "Read")], ... }
```

El backend **no** gobierna este endpoint con el permiso de expedientes. Lo gobierna con el de
preferencias de empresa (`CompanyLegalProfilesController` no lleva `[AuthorizationPolicySet]`; la
autorización vive en `ICompanyPreferenceAuthorizationService`):

| Operación | Permisos aceptados (cualquiera de ellos) |
|---|---|
| `GET` | `CompanyPreferences.Read`, `CompanyPreferences.Admin`, `iam.administration.manage` |
| `POST` / `PUT` | `CompanyPreferences.Admin`, `iam.administration.manage` |

Síntoma verificado en producción: navegar a `/company-legal-profile` con sesión activa y empresa
seleccionada **redirige silenciosamente a `/`**, incluso siendo la persona dueña de la empresa. No
hay mensaje de error; parece que la ruta no existiera.

**2. Nadie la enlazó desde Configuración.** La pantalla `/settings` lista cuatro entradas
(*Configuración de la empresa*, *Marco de competencias*, *Roles y permisos*, *Suscripción*) y el
perfil legal no está entre ellas. La página *Configuración de la empresa* (`/company-profile`)
tampoco contiene sus campos: solo nombre, tipo de persona, zona horaria, moneda y representantes
legales.

Corregir solo el guard deja la pantalla accesible únicamente escribiendo la URL a mano. Hay que
hacer las dos cosas.

---

## 2. Contrato

```
GET   /api/v1/companies/{companyPublicId}/legal-profile
POST  /api/v1/companies/{companyPublicId}/legal-profile
PUT   /api/v1/companies/{companyPublicId}/legal-profile
```

> **El parámetro de ruta es `companyPublicId`, no `companyId`.** El controller lo declara como
> `companyId`, pero una convención de la API lo reescribe. Lo mismo pasa con la respuesta: el record
> de C# declara `Id` y se serializa como **`publicId`**. Guiarse por el swagger, no por el código.

### Cuerpo de `POST` y `PUT`

Ambos reciben la misma forma:

```jsonc
{
  "legalName": "Avianca El Salvador, S.A. de C.V.",   // requerido, máx. 200
  "employerNitNumber": "0614-010180-101-2",           // requerido, máx. 20, formato validado
  "isssEmployerRegistrationNumber": "123456-7",       // requerido, máx. 20, formato validado
  "fiscalAddress": "Km 42 Carretera al Aeropuerto…",  // requerido, máx. 500
  "economicActivityDescription": "Transporte aéreo",  // opcional, máx. 200, nullable
  "legalRepresentativePublicId": "8f3b…"              // opcional, nullable
}
```

`PUT` es **reemplazo total**: omitir o mandar `null` en un campo opcional lo borra.

### Respuesta

```jsonc
{
  "publicId": "…",                       // uuid — NO se llama "id"
  "legalName": "…",
  "employerNitNumber": "…",
  "isssEmployerRegistrationNumber": "…",
  "fiscalAddress": "…",
  "economicActivityDescription": null,   // nullable
  "legalRepresentativePublicId": null,   // nullable
  "concurrencyToken": "…",               // uuid — para el If-Match del siguiente PUT
  "createdAtUtc": "…",
  "modifiedAtUtc": null                  // nullable
}
```

### Códigos por verbo

| Verbo | Códigos |
|---|---|
| `GET` | `200`, `401`, `403`, `404` |
| `POST` | `201`, `400`, `401`, `403`, `404`, `409`, `422` |
| `PUT` | `200`, `400`, `401`, `403`, `404`, `409`, `422` |

---

## 3. Semántica de estados — el `404` no es un error

Esta es la parte que más fácil se implementa mal:

| Resultado del `GET` | Significa | Qué hace la UI |
|---|---|---|
| `200` | Ya está configurado | Formulario poblado; guardar con `PUT` |
| `404` `COMPANY_LEGAL_PROFILE_NOT_FOUND` | **Todavía no se ha configurado** | Formulario vacío; guardar con `POST` |
| `403` | Falta permiso | Mensaje de permiso, no pantalla en blanco |

**Un `404` acá no debe pintarse como fallo ni mandar a una página de error.** Es el estado inicial
esperado de toda empresa recién creada: el perfil legal **no** se crea al provisionar, así que
arranca inexistente hasta que alguien lo llene.

Hacer `POST` cuando ya existe devuelve `409 COMPANY_LEGAL_PROFILE_ALREADY_EXISTS`.

> **Bug corregido el 2026-08-03 — relevante si probaron antes de esa fecha.** El `POST` devolvía
> `500` (`No route matches the supplied values`) **aunque el registro sí se creaba**: la transacción
> confirmaba y luego fallaba al armar la cabecera `Location`. Para el frontend se veía como un fallo,
> y el reintento daba `409 ALREADY_EXISTS` sobre un perfil que acababa de crearse — muy confuso de
> diagnosticar. Ya arreglado: el `POST` responde `201` con `Location` y `ETag` correctos.

---

## 4. Concurrencia

`PUT` exige la cabecera `If-Match` con el `concurrencyToken` vigente:

- **Ausente** → `400`
- **Desactualizado** → `409 COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT`

El token refrescado vuelve en el cuerpo y en la cabecera `ETag`. Hay que reemplazar el que se tenía
en memoria después de cada guardado, o el segundo `PUT` seguido falla.

`POST` no lleva `If-Match`.

---

## 5. Validaciones

### Formato de los identificadores fiscales

| Campo | Regla | Ejemplo válido |
|---|---|---|
| `employerNitNumber` | `^\d{4}-\d{6}-\d{3}-\d$` | `0614-010180-101-2` |
| `isssEmployerRegistrationNumber` | `^[0-9-]{6,20}$` | `123456`, `123456-7` |

Ambos se validan sobre el valor **recortado**, así que un espacio pegado al copiar no rompe. Cuidado:
`maxLength` sí mide el valor crudo, de modo que un valor con mucho relleno se rechaza por longitud
antes que por formato.

Conviene replicar estos patrones del lado del cliente para dar retroalimentación inmediata, pero el
servidor manda: antes existían campos de texto libre y un NIT mal escrito llegaba impreso al F-14.

Un `400` trae los mensajes así (verificado contra la API real, con `Accept-Language: es`):

```jsonc
{
  "status": 400,
  "code": "common.validation",
  "detail": "Se encontraron uno o mas errores de validacion.",
  "errors": {
    "": [                                    // ← ojo con la clave VACÍA
      "El NIT patronal debe seguir el formato ####-######-###-#.",
      "El número de registro patronal del ISSS solo acepta dígitos y guiones."
    ]
  }
}
```

> **Los mensajes por campo llegan todos bajo la clave `""`, no bajo el nombre del campo.** Es una
> limitación preexistente de este endpoint: los validadores usan accesores lambda, así que
> FluentValidation no puede derivar el nombre de la propiedad, y le pasa a **todos** los campos del
> perfil legal, no solo a los dos nuevos. Consecuencia práctica: no se puede mapear el error al input
> automáticamente. Las opciones son mostrar los mensajes como lista a nivel de formulario, o
> reconocerlos por su texto. Si les estorba, avisen y se refactoriza el validador para exponer los
> nombres de campo.

### Representante legal enlazado

`legalRepresentativePublicId` es opcional, pero si se manda tiene que resolver a un representante
**de esa misma empresa** y **activo**:

| Código | HTTP | Cuándo |
|---|---|---|
| `COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_NOT_FOUND` | `422` | El id no existe en ninguna empresa |
| `COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_INACTIVE` | `422` | Existe en esta empresa pero está inactivo |
| *tenant mismatch* | `403` | Existe, pero pertenece a **otra** empresa |

Por qué importa la distinción: los representantes legales están **agrupados por empresa**. La misma
persona que representa a varias compañías tiene **una fila por cada una**, con su propio `publicId`
(la unicidad de documento es por empresa, no global). Enlazar el id de otra empresa imprimiría un
firmante ajeno en el F-14, así que se rechaza.

Consecuencia para la UI: **el selector de representante debe alimentarse de los representantes
activos de la empresa actual**, no de una lista global. Las fechas de vigencia
(`effectiveFrom`/`effectiveTo`) **no** se validan a propósito — un nombramiento con fecha futura y
una carga retroactiva son ambos legítimos.

---

## 6. Cambios pedidos al frontend

1. **Corregir el guard** de `company-legal-profile` para que use el permiso de preferencias de
   empresa (§1) en vez de `PersonnelFiles.Read`.
2. **Enlazar la pantalla** en `/settings`, sección **Organización**, junto a *Configuración de la
   empresa*.
3. **Agregarla como paso de la configuración guiada** (§7).
4. **Selector de representante legal** poblado con los activos de la empresa actual.
5. **Validación de formato en cliente** para NIT y registro ISSS, replicando §5.
6. **Tratar el `404` como estado vacío**, no como error (§3).

---

## 7. Incorporación al asistente `/setup`

La configuración guiada tiene hoy siete pasos —tipos de unidad → unidades organizativas → tipos de
centro de trabajo → centros de trabajo → perfiles de puesto → plazas → expedientes— y el perfil legal
no está entre ellos.

**Ubicación sugerida: primero, antes de "Tipos de unidad".** Es identidad de la empresa, no
estructura, y no depende de ningún otro paso.

| Propiedad del paso | Valor |
|---|---|
| Estado *Completado* | El `GET` devuelve `200` |
| Estado *Disponible* | Siempre — sin prerequisitos |
| Estado *Bloqueado* | Nunca |

> Nota aparte sobre el asistente: el paso 1 aparece **Completado** en cuanto se crea la empresa,
> porque el aprovisionamiento siembra los tipos de unidad genéricos `GERENCIA`/`DEPARTAMENTO`/
> `UNIDAD`. Se está dando por bueno un catálogo de plantilla que casi siempre hay que reemplazar. Vale
> la pena revisar si ese paso debería exigir alguna señal de edición real en vez de la mera
> existencia de filas.

---

## 8. Alcance del gate de cumplimiento

Hoy **la ausencia del perfil legal no bloquea nada**. El interruptor que lo vuelve obligatorio,
`CompanyPreference.PayrollComplianceGatesEnabled`, está apagado por defecto y **no tiene endpoint
público** para encenderlo — es deliberado, para no dejar a una empresa sin poder generar nómina antes
de que complete sus datos.

Cuando se active por tenant, `POST payroll-runs` empezará a responder
`422 PAYROLL_RUN_MISSING_LEGAL_PROFILE`. Conviene que la pantalla de generación de planilla ya sepa
reconocer ese código y ofrecer un enlace directo a esta pantalla.

---

## 9. Ruta y proxy

El frontend consume la API por un proxy del mismo origen: se observó
`dashboard.clarihr.com/account/companies` para lo que en el backend es
`/api/v1/account/companies`. Esta guía usa las rutas del backend; el prefijo lo resuelve el proxy.
Confirmar contra su configuración antes de tipar el cliente.
