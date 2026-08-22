# ComentariosPruebasBackend — definición del documento

Hallazgos de **backend**: fallas, carencias de contrato y mejoras del servidor, detectados durante las pruebas manuales.

> **Un documento por paso probado**, con **el mismo número y el mismo nombre** que su par en `ComentariosPruebasFrontend/`.

Cada paso genera como máximo dos documentos, espejo uno del otro:

```
ComentariosPruebasFrontend/00001-CompanyLegalProfile.md   ← lo que arregla el frontend
ComentariosPruebasBackend/00001-CompanyLegalProfile.md    ← lo que arregla el backend
```

Así, al revisar un paso, se abren los dos archivos y se tiene la foto completa. Y no hay que rastrear en qué ficha suelta quedó cada hallazgo.

Si un paso **no** generó hallazgos de backend, **no se crea el archivo**. El documento de frontend lo dirá con «Ninguno en esta corrida».

---

## 1. De dónde salen

La mayoría se detectan **probando el frontend**: se prueba el cliente, pero se prueba *contra* el servidor, y ahí es donde las carencias del backend se vuelven visibles. También pueden salir de pruebas de API directas, de revisiones de código o de incidentes.

> **Nada queda pendiente de medir.** Un hallazgo de backend sin medir no se documenta como sospecha: se mide. Los montajes están catalogados en [`../ComentariosPruebasFrontend/README.md`](../ComentariosPruebasFrontend/README.md) §8 — sondas garantizadas a fallar, pares diferenciales, lectura de cabeceras sin descargar, ciclos reversibles y empresa desechable. Las dos únicas excepciones admitidas son un insumo externo que no controlamos o un paso posterior de la propia secuencia, y las dos se nombran con su responsable o su paso.
>
> **Los que más aparecen son los del camino de escritura.** Una corrida de solo lectura destapa lo que falta en el contrato; el ciclo completo —crear, editar, dar de baja, provocar el duplicado y el conflicto de concurrencia— destapa además lo que el servidor **hace mal al responder**: mensajes sin localizar, cabeceras que no llegan, claves de error que no casan con el parámetro público. En el Paso 2, tres de los cuatro hallazgos salieron al escribir, no al leer.
>
> **Mirar las cabeceras de respuesta, no solo el cuerpo.** `ETag` y `Location` fueron el origen de un hallazgo transversal que el cuerpo de la respuesta nunca habría mostrado.

> **Una ausencia del contrato se juzga antes de darla por buena.** Que el servidor no exponga un verbo, un campo o un endpoint **no es prueba de que no deba exponerlo**. La regla completa está en [`../ComentariosPruebasFrontend/README.md`](../ComentariosPruebasFrontend/README.md) §6; del lado del backend, la comprobación que más rinde es **buscar el mismo patrón en un módulo hermano**: si otro controlador ya lo implementa, desaparece el argumento de que no se puede y aparece la implementación de referencia.
>
> Pasó en el Paso 2: se dio por bueno que los tipos de unidad no tuvieran `DELETE`. `JobCatalogsController` ya tenía borrado condicional con verificación de uso, y `CostCentersController` —la pestaña de al lado— ya tenía `/usage`. La conclusión se invirtió y salió **00003 / B-03**.

Se levanta un hallazgo cuando:

- El contrato **no da al frontend** lo que necesita para comportarse bien (p. ej. no expone si el usuario puede escribir).
- El backend **obliga al cliente a una solución peor** de la necesaria (adivinar permisos, no poder mapear un error a su campo).
- Hay **incoherencia** entre lo que documenta el contrato y lo que llega al cliente.
- Se detecta una **regla de negocio ausente, ambigua o contradictoria**.
- El servidor **calcula un dato que el cliente necesita y lo descarta** al responder (p. ej. rechaza por «está en uso» sabiendo exactamente quién lo usa).
- Es una mejora que reduce deuda transversal, **aunque hoy nadie la esté sufriendo**.

**Se registra aunque el frontend tenga vía alterna y no quede bloqueado.** Si tiene vía alterna, se anota como tal — pero se registra igual, porque casi siempre reaparece en otras pantallas.

---

## 2. Nomenclatura

### Del archivo

```
<NNNNN>-<NombrePantalla>.md
```

Copiado tal cual del documento espejo de frontend. Mismo correlativo, mismo nombre.

### De los hallazgos dentro del archivo

`B-01`, `B-02`, … **correlativos dentro del documento**, igual que los `F-NN` del lado del frontend. Se numeran en **orden de lectura** (que es el orden de severidad); si al agregar uno cambia el orden, se renumera.

Para citar un hallazgo desde otro documento se antepone el ID del paso:

> «Ver **00001 / B-02**» → hallazgo `B-02` del documento `00001-CompanyLegalProfile.md`.

---

## 3. Estructura obligatoria

```markdown
# <NNNNN> — <NombrePantalla> · Hallazgos de backend

| | |
|---|---|
| **ID** | <NNNNN>-<NombrePantalla> |
| **Documento espejo** | enlace al de ComentariosPruebasFrontend |
| **Paso probado** | … |
| **Pantalla que los destapó** | … |
| **Fecha** | AAAA-MM-DD |
| **Ambiente** | … |

## 1. Resumen
Tabla con: ID · severidad · hallazgo · componente · origen · alcance · estado.
Y una línea que diga si alguno bloquea al frontend.

## 2. B-01 — <título>
## 3. B-02 — <título>
…
```

Cada hallazgo, con su encabezado de metadatos (severidad, estado, componente, origen, alcance) y **estas ocho secciones**:

| # | Sección | Qué lleva |
|---|---|---|
| 1 | **Evidencia** | El dato crudo: respuesta observada, fragmento de código, cabecera |
| 2 | **Causa** | Qué en el backend lo produce, citando clase y archivo |
| 3 | **Impacto** | Qué le cuesta al frontend hoy, y a quién más le cuesta |
| 4 | **Propuesta** | El cambio concreto. Si hay varias opciones, cuál se recomienda y por qué |
| 5 | **Compatibilidad** | ¿Rompe el contrato? ¿Requiere regenerar `openapi.yaml`? ¿Hay que migrar clientes? |
| 6 | **Alcance a revisar** | Si se sospecha que el patrón se repite en otros módulos. Cambia el costo/beneficio de la decisión |
| 7 | **Vía alterna vigente** | Qué hace hoy el frontend mientras esto no se resuelve. Si no hay, decirlo |
| 8 | **Bitácora** | Fecha · estado · nota. El rastro de las decisiones |

---

## 4. Estados

| | Significado |
|---|---|
| 🔲 **Propuesto** | Detectado y documentado. Falta analizarlo y decidir |
| 🔍 **En análisis** | Se está evaluando alcance, riesgo y compatibilidad |
| ✅ **Aprobado** | Decidido que se hace. Pendiente de implementar |
| 🛠️ **En curso** | Implementándose |
| 🟢 **Resuelto** | Implementado y verificado. Anotar en qué corrida se confirmó |
| ⛔ **Descartado** | Se decide no hacerlo. **Anotar el porqué**, para que no se re-litigue |

El estado se actualiza en **dos lugares del mismo archivo**: la tabla de resumen (§1) y el encabezado del hallazgo. Y se deja rastro en su bitácora. No hay tablero externo: el estado vive donde vive el hallazgo.

---

## 5. Hallazgos transversales

Un hallazgo puede afectar a más pantallas de la que lo destapó. En ese caso:

- **Se queda en el documento del paso donde se detectó** — no se mueve ni se duplica.
- Se marca `Alcance: Transversal` en su encabezado.
- Los pasos siguientes que lo vuelvan a topar **lo citan** (`Ver 00001 / B-03`) en lugar de levantarlo de nuevo.

Así el hallazgo conserva un solo dueño y una sola bitácora, y aun así se ve desde donde haga falta.

---

## 6. Relación con `ComentariosPruebasFrontend/`

- El **detalle completo vive aquí**. El documento de la pantalla **no lo repite**: solo deja una tabla de punteros (ID, severidad, resumen de una línea, origen y enlace).
- Así se evita que el mismo contenido exista en dos lugares y se desincronice.
