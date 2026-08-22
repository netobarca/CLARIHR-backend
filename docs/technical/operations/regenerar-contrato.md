# Regenerar el contrato público

Qué hacer cuando **añades, quitas o cambias la forma de un endpoint**.

Hasta el 2026-08-16 esto no estaba escrito en ninguna parte: no había manifiesto de herramientas, ni nota en las definiciones técnicas, ni script. La consecuencia está medida — `openapi.yaml` se desfasó del código durante siete días y con él los tres endpoints que bloquean los Pasos 7 y 8 del asistente. Ver [`00950 / B-01`](../../../ComentariosPruebasBackend/00950-Remediacion.md#2-b-01--el-contrato-publicado-puede-desfasarse-del-código-sin-que-nada-lo-detecte).

---

## Los dos artefactos, y cuál manda

| Artefacto | Qué es | ¿Autoritativo? | Tamaño |
|---|---|---|---|
| `docs/technical/api/contract-fingerprint.txt` | Huella: una línea por operación | ✅ **sí** — el guardrail la verifica | 93 KB |
| `docs/technical/api/openapi.yaml` | Documento completo OpenAPI 3.0 | ❌ referencia legible | 2.7 MB |

**La huella es la que manda**, y es la que rompe la corrida si no está sincronizada. El documento completo se mantiene porque es legible para una persona, pero **nada lo verifica**: a 10 000 operaciones serían ~30 MB y ~987 000 líneas, ilegible en cualquier revisión.

> Si solo tienes tiempo para una cosa, **sincroniza la huella**. Es la que protege.

---

## 1. Volcar el contrato completo — solo si vas a sincronizar `openapi.yaml`

> Para la huella **no hace falta**: el paso 2 la genera directamente desde el host de pruebas.

```bash
CLARIHR_DUMP_SWAGGER=1 dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj \
  --no-build --filter "FullyQualifiedName~Swagger_DumpContract"
```

Deja `tests/CLARIHR.Api.IntegrationTests/bin/Debug/net10.0/swagger-dump.json`.

`SwaggerDumpTests` es **inerte sin la variable de entorno**: no añade tiempo a ninguna corrida y no afirma nada — es una herramienta, no un guardrail.

> Requiere el Postgres de Docker en `:5433`. Si hay una corrida de tests viva, espérala: compilar sobre DLL en uso produce fallos irreproducibles.

## 2. Regenerar la huella

```bash
CLARIHR_WRITE_FINGERPRINT=1 dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj \
  --no-build --filter "FullyQualifiedName~ContractFingerprint"
```

Reescribe `docs/technical/api/contract-fingerprint.txt` desde el contrato que genera el código.

> **Es el mismo test que verifica.** Con la variable escribe; sin ella compara. Eso es deliberado: el
> primer intento tenía la generación en un script de Python y la verificación en C#, **dos
> implementaciones del mismo hash que no coincidían** — las 901 operaciones salían como ausentes. Dos
> fuentes de verdad para una sola cosa divergen siempre, que es justo lo que esta huella existe para
> evitar. Ahora el archivo no se puede generar por una vía distinta de la que lo comprueba.

**Revisa el diff antes de commitear.** Debería contener exactamente lo que cambiaste. Si aparecen
operaciones que no tocaste, algo más se movió y conviene entenderlo.

## 3. Verificar

```bash
dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj \
  --no-build --filter "FullyQualifiedName~ContractFingerprint"
```

Si falla, el mensaje dice qué falta y qué sobra, línea por línea.

## 4. Sincronizar `openapi.yaml` — opcional, y por qué se hace a mano

**No lo regeneres entero.** El generador original no está identificado y su estilo no se reproduce: la conversión directa desde el swagger produce un diff de **87 908 líneas** de puro reformateo. Ajustando indentación y comillas se baja a ~1 200, pero sigue sin coincidir — el archivo usa una mezcla de estilos de comillas que ninguna configuración de PyYAML reproduce.

La vía que funciona es **inserción quirúrgica**, y es la que se usó el 2026-08-16 para 9 rutas, 14 esquemas y 2 verbos:

| Caso | Qué hacer |
|---|---|
| **Ruta nueva** | Insertar el bloque tras la ruta existente con el prefijo común más largo — junto a sus hermanas. Ojo con el orden alfabético: el prefijo común puede emparejar mal (`aguinaldo-exemptions` se empareja con `auth`, pero va antes) |
| **Verbo nuevo en ruta existente** | Añadir al final del bloque de la ruta. El orden del archivo es `get · put · patch · delete` |
| **Esquema nuevo** | Insertar en `components.schemas` en orden alfabético |
| **Forma cambiada** | Sustituir el bloque del verbo |

Las claves de ruta que contienen `{` van **entrecomilladas** (`'/api/v1/…/{id}'`); las listas se **indentan** bajo su clave.

Al terminar, comprobar que sigue parseando y que no falta nada:

```bash
python3 -c "
import yaml, json
a=yaml.safe_load(open('docs/technical/api/openapi.yaml',encoding='utf-8'))
g=json.load(open('tests/CLARIHR.Api.IntegrationTests/bin/Debug/net10.0/swagger-dump.json'))
print('rutas:', len(a['paths']), 'vs', len(g['paths']))
falta=[k for k in g['paths'] if set(g['paths'][k])-set(a['paths'].get(k,{}))]
print('rutas con verbos ausentes:', len(falta))
print('esquemas ausentes:', len(set(g['components']['schemas'])-set(a['components']['schemas'])))"
```

---

## Lo que conviene cambiar cuando haya tiempo

El paso 4 es trabajo manual que existe solo porque **se versiona una salida de compilación**. Dos mejoras, en orden de valor:

1. **Dejar de versionar `openapi.yaml`.** Generarlo en CI y publicarlo como artefacto o URL. La huella cubre la verificación; el documento solo hace falta como referencia, y una referencia se sirve, no se commitea.

2. **Publicar subconjuntos por consumidor.** El frontend usa del orden de 300 de las 901 operaciones. Un documento con lo que cada consumidor consume es más útil para él que uno de 30 MB donde el 97 % no le concierne — y **escala con el uso, no con el tamaño del API**.

La opción que suele proponerse primero —partir el documento por dominio con `$ref` externos— hace el diff revisable pero **no verificable**: seguiría sin detectarse que falta un endpoint. Es la mitad que ya cubre la huella, sin la mitad que importa.
