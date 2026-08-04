# Guía de configuración de empresa — Avianca El Salvador

Guía paso a paso para dejar una compañía recién creada lista para operar, desde el frontend.
Caso de ejemplo: **Avianca El Salvador, S.A. de C.V.** — marco legal salvadoreño.

- **Marco legal**: El Salvador (ISSS, AFP, Renta SV, F-14, Planilla Única)
- **Nómina**: quincenal para todo el personal
- **Jornadas**: administrativa + turnos rotativos de aeropuerto + tripulaciones
- **Dotación de prueba**: ~45 empleados

> Esta guía **no cambia código ni base de datos**. Todo se hace desde la interfaz.
> Los valores de las tablas son propuestas — validalos y ajustá antes de cargarlos.

---

## 0. Lo que NO se toca

Antes de limpiar nada, la distinción que importa. El sistema tiene dos clases de catálogo y solo una es tuya:

### Catálogos de sistema — se quedan como están

Sembrados por migración (`GlobalCatalogSeedData`, IDs `-9000…-9969`), compartidos por todas las empresas del país:

| Catálogo | Ejemplos |
|---|---|
| Bancos SV | Banco Agrícola, Davivienda, Cuscatlán, BAC |
| AFP | Confía, Crecer, Otra |
| Estados de empleo | Activo, Suspendido, Licencia, Incapacidad, Retirado |
| Tipos de planilla | `MENSUAL`, `QUINCENAL`, `SEMANAL`, `POR_DIA`, `POR_OBRA`, `OTRO` |
| Métodos de pago | Transferencia, Cheque, Efectivo, Boleta |
| Conceptos de planilla, tipos de acción de personal | (referenciados por código desde el motor) |
| Jerarquía de ubicaciones SV | País → Departamento → Municipio |

**Si borrás estas filas se rompe el motor de planilla.** Varias se referencian por código desde el cálculo — por ejemplo la línea de horas extra `-9915` deja de existir y la corrida muere. Recuperarlas exige volver a correr migraciones.

### Catálogos de empresa — estos sí se ajustan

Creados automáticamente al provisionar tu compañía. Son plantillas genéricas pensadas para editarse:

| Catálogo | Lo que trae de fábrica |
|---|---|
| Tipos de unidad organizativa | `GERENCIA`, `DEPARTAMENTO`, `UNIDAD` |
| Áreas funcionales | `ADMIN`, `OPS`, `SALES` |
| Jornadas | una sola: `JORNADA_ORDINARIA` (44 h) |
| Justificaciones de horas extra | `PICO_PRODUCCION`, `CIERRE_CONTABLE`, `PROYECTO_ESPECIAL`, `EMERGENCIA`, `MANTENIMIENTO`, `OTRO` |
| Reconocimientos | `FELICITACION_ESCRITA`, `DESEMPENO_SOBRESALIENTE`, `PRODUCTIVIDAD`, `ANTIGUEDAD`, `OTRO` |
| Amonestaciones | `VERBAL`, `ESCRITA`, `SUSPENSION_SIN_GOCE`, `OTRO` |
| Causas de amonestación | `INASISTENCIA_INJUSTIFICADA`, `LLEGADAS_TARDIAS`, `INCUMPLIMIENTO_FUNCIONES`, `CONDUCTA_INDEBIDA`, `DANO_BIENES`, `OTRO` |
| Tiempos no trabajados | `AUSENCIA_SIN_GOCE`, `AUSENCIA_CON_GOCE`, `SUSPENSION_CON_DESCUENTO`, `LLEGADA_TARDIA` |
| Asuetos | únicamente los del **año en curso** |

### Dos que parecen de empresa pero son ley

Estos aparecen en la lista editable, pero el valor lo fija el Código de Trabajo. Renombrar la etiqueta está bien; **cambiar los números no**:

| Elemento | Valor | Base legal |
|---|---|---|
| Hora extra diurna `HED` | ×2.00 | Art. 169 CT |
| Hora extra nocturna `HEN` | ×2.50 | Art. 168/169 CT |
| Extra diurna en descanso/asueto `HEDF` | ×4.00 | Art. 171/175 CT |
| Extra nocturna en descanso/asueto `HENF` | ×5.00 | Art. 171/175 CT |
| Tipos de incapacidad | el marcador *"aplica a riesgo profesional"* de `ACCIDENTE_TRABAJO` y `ENFERMEDAD_PROFESIONAL` cambia el cálculo del subsidio | Ley ISSS |

---

## 1. Limpieza previa

Solamente si arrastrás datos de prueba anteriores.

1. **Tenant demo.** El usuario `dev@clarihr.local` y su empresa completa (unidades, plazas, tabulador, un expediente y tramos de Renta de muestra). Se elimina con el script que ya tenés en `docs/technical/operations/scripts/borrar-usuario-y-sus-empresas.sql`.
2. **Catálogos de empresa de tu compañía nueva.** No los borres desde la base. Cada pantalla de catálogo tiene inactivación lógica: en las secciones siguientes se indica cuáles renombrar, cuáles inactivar y cuáles agregar.

> **Nunca** toques las filas con ID negativo `-9000…-9969`.

---

## 2. Identidad legal y preferencias

### 2.1 Perfil legal de la empresa

Pantalla: **Configuración → Empresa → Perfil legal**

Es la cabecera de F-14, Planilla Única y Planilla Patronal. Los cuatro primeros campos son obligatorios.

| Campo | Valor propuesto |
|---|---|
| Razón social | Avianca El Salvador, S.A. de C.V. |
| NIT patronal | `0614-010180-101-2` ← *placeholder, reemplazar por el real* |
| Registro patronal ISSS (NRC) | `123456-7` ← *placeholder* |
| Dirección fiscal | Aeropuerto Internacional Monseñor Óscar Arnulfo Romero, km 42 Carretera al Aeropuerto, San Luis Talpa, La Paz |
| Actividad económica | Transporte aéreo de pasajeros y carga |

> Los identificadores fiscales son texto libre y **no se validan contra Hacienda ni ISSS**. Un NIT mal escrito pasa sin protestar y sale impreso en el F-14. Verificalos contra el documento original.

### 2.2 Representante legal

Pantalla: **Configuración → Empresa → Representantes legales**

Se crea uno automáticamente al provisionar la compañía, con los datos del usuario que la creó. Editalo:

| Campo | Valor |
|---|---|
| Nombre | *(el representante legal real)* |
| Tipo de representación | Apoderado general administrativo |
| Cargo | Director General |
| Documento | DUI |

Después volvé al perfil legal y enlazá este representante — es quien firma los reportes legales.

### 2.3 Preferencias de compañía

Pantalla: **Configuración → Empresa → Preferencias**

| Preferencia | Valor | Por qué |
|---|---|---|
| Moneda | `USD` | El Salvador está dolarizado |
| Zona horaria | `America/El_Salvador` | Determina el corte de los periodos |
| Día de descanso semanal | Domingo | Valor por defecto de la empresa; el personal de turnos lo sobreescribe en su asignación (§9.3) |
| Días de vacación anuales | 15 | Art. 177 CT |
| Días adicionales de beneficio | 0 | Subilo si Avianca da más que lo legal |
| Permitir inicio de vacación en asueto | No | Art. 178 CT |
| Permitir fin de vacación en asueto | Sí | |
| Permitir inicio de vacación en día de descanso | No | Art. 178 CT |
| Días de incapacidad cubiertos por el patrono | 9 | Tope patronal anual |
| Área funcional de RRHH | `GENTE` | Alimenta el indicador de ratio de RRHH del tablero |
| Umbral de expediente actualizado | 12 meses | |
| Antigüedad mínima para ayuda económica | 12 meses | Ajustable según política |

---

## 3. Catálogos de empresa

Propuesta completa para una aerolínea. Cada tabla indica qué hacer con lo que ya existe.

### 3.1 Tipos de unidad organizativa

Pantalla: **Configuración → Estructura → Tipos de unidad**

| Código | Nombre | Orden | Acción |
|---|---|---|---|
| `DIRECCION_GENERAL` | Dirección General | 10 | agregar |
| `VICEPRESIDENCIA` | Vicepresidencia | 20 | agregar |
| `DIRECCION` | Dirección | 30 | agregar |
| `GERENCIA` | Gerencia | 40 | **ya existe** — solo cambiar orden |
| `JEFATURA` | Jefatura | 50 | agregar |
| `DEPARTAMENTO` | Departamento | 60 | **ya existe** — solo cambiar orden |
| `AREA` | Área | 70 | agregar |
| `BASE` | Base / Estación | 80 | agregar |
| `UNIDAD` | Unidad | 90 | **ya existe** — inactivar si no la usás |

### 3.2 Áreas funcionales

Pantalla: **Configuración → Estructura → Áreas funcionales**

| Código | Nombre | Orden | Acción |
|---|---|---|---|
| `OPS_VUELO` | Operaciones de Vuelo | 10 | agregar |
| `SERV_ABORDO` | Servicio a Bordo | 20 | agregar |
| `MANTENIMIENTO` | Mantenimiento e Ingeniería | 30 | agregar |
| `AEROPUERTOS` | Aeropuertos y Servicio en Tierra | 40 | agregar |
| `CARGA` | Carga | 50 | agregar |
| `SEG_OPERACIONAL` | Seguridad Operacional y Calidad | 60 | agregar |
| `COMERCIAL` | Comercial y Ventas | 70 | reemplaza a `SALES` |
| `FINANZAS` | Finanzas y Administración | 80 | reemplaza a `ADMIN` |
| `GENTE` | Gente y Cultura | 90 | agregar |
| `TECNOLOGIA` | Tecnología | 100 | agregar |
| `LEGAL` | Legal y Cumplimiento | 110 | agregar |
| ~~`ADMIN`~~ / ~~`OPS`~~ / ~~`SALES`~~ | | | inactivar los tres |

> Inactivá los genéricos **después** de crear los nuevos y antes de armar el organigrama. Si ya hay unidades apuntando a `ADMIN`, reasignalas primero.

### 3.3 Justificaciones de horas extra

Pantalla: **Configuración → Horas extra → Justificaciones**

| Código | Nombre | Orden | Acción |
|---|---|---|---|
| `IRREGULARIDAD_OPERACIONAL` | Irregularidad operacional (demora, cancelación, desvío) | 10 | agregar |
| `MANTENIMIENTO_AOG` | Mantenimiento no programado / AOG | 20 | agregar |
| `COBERTURA_TURNO` | Cobertura de turno por ausencia | 30 | agregar |
| `TEMPORADA_ALTA` | Temporada alta / pico de demanda | 40 | reemplaza a `PICO_PRODUCCION` |
| `AUDITORIA_CERTIFICACION` | Auditoría o certificación aeronáutica | 50 | agregar |
| `CIERRE_CONTABLE` | Cierre contable o de periodo | 60 | **ya existe** |
| `EMERGENCIA` | Emergencia o contingencia operativa | 70 | **ya existe** |
| `OTRO` | Otra | 80 | **ya existe** |
| ~~`PICO_PRODUCCION`~~ / ~~`PROYECTO_ESPECIAL`~~ / ~~`MANTENIMIENTO`~~ | | | inactivar |

> `MANTENIMIENTO` genérico se inactiva a propósito: en una aerolínea "mantenimiento" es un área, no una justificación. La justificación real es `MANTENIMIENTO_AOG`.

### 3.4 Reconocimientos

Pantalla: **Configuración → Relaciones laborales → Reconocimientos**

| Código | Nombre | Orden | Acción |
|---|---|---|---|
| `SEGURIDAD_OPERACIONAL` | Reporte voluntario de seguridad operacional | 10 | agregar |
| `SERVICIO_CLIENTE` | Excelencia en servicio al pasajero | 20 | agregar |
| `PUNTUALIDAD` | Contribución a puntualidad de la operación | 30 | agregar |
| `DESEMPENO_SOBRESALIENTE` | Desempeño sobresaliente | 40 | **ya existe** |
| `ANTIGUEDAD` | Reconocimiento por años de servicio | 50 | **ya existe** |
| `OTRO` | Otro | 60 | **ya existe** |
| ~~`FELICITACION_ESCRITA`~~ / ~~`PRODUCTIVIDAD`~~ | | | inactivar |

### 3.5 Causas de amonestación

Pantalla: **Configuración → Relaciones laborales → Causas de amonestación**

Conservá las seis de fábrica y agregá tres específicas del sector:

| Código | Nombre | Orden |
|---|---|---|
| `INCUMPLIMIENTO_PROC_SEGURIDAD` | Incumplimiento de procedimiento de seguridad operacional | 70 |
| `USO_INDEBIDO_CREDENCIAL` | Uso indebido de credencial aeroportuaria | 80 |
| `INCUMPLIMIENTO_NORMATIVA_AERONAUTICA` | Incumplimiento de normativa aeronáutica | 90 |

**Los tipos de amonestación** (`VERBAL`, `ESCRITA`, `SUSPENSION_SIN_GOCE`, `OTRO`) quedan como están — la suspensión sin goce tiene efecto en planilla y su marcador ya viene bien configurado.

### 3.6 Tiempos no trabajados

Los cuatro de fábrica sirven tal cual. Agregá uno si Avianca maneja licencias sindicales o permisos de estudio:

| Código | Nombre |
|---|---|
| `PERMISO_ESTUDIO` | Permiso por estudio |

---

## 4. Geografía y sedes

### 4.1 Jerarquía de ubicaciones

Pantalla: **Configuración → Ubicaciones**

Ya viene armada: País → Departamento → Municipio, con los 14 departamentos y municipios de El Salvador. **No la toques.**

### 4.2 Tipos de centro de trabajo

Pantalla: **Configuración → Centros de trabajo → Tipos**

| Código | Nombre |
|---|---|
| `ESTACION_AEROPUERTO` | Estación aeroportuaria |
| `HANGAR` | Hangar de mantenimiento |
| `OFICINA` | Oficina corporativa |
| `TERMINAL_CARGA` | Terminal de carga |
| `CENTRO_ENTRENAMIENTO` | Centro de entrenamiento |

### 4.3 Centros de trabajo (sedes)

Pantalla: **Configuración → Centros de trabajo**

| Código | Nombre | Tipo | Departamento | Municipio |
|---|---|---|---|---|
| `SAL-EST` | Estación SAL — Aeropuerto Int. Mons. Óscar A. Romero | `ESTACION_AEROPUERTO` | La Paz | San Luis Talpa |
| `SAL-HGR` | Hangar de Mantenimiento SAL | `HANGAR` | La Paz | San Luis Talpa |
| `SAL-CRG` | Terminal de Carga SAL | `TERMINAL_CARGA` | La Paz | San Luis Talpa |
| `SS-CORP` | Oficina Corporativa San Salvador | `OFICINA` | San Salvador | San Salvador |
| `SS-CAP` | Centro de Entrenamiento | `CENTRO_ENTRENAMIENTO` | San Salvador | Antiguo Cuscatlán |

> El centro de trabajo se usa en el reporte de Planilla Patronal y en la distribución geográfica del tablero. Que el municipio sea el correcto importa.

---

## 5. Estructura organizativa

Pantalla: **Configuración → Estructura → Unidades organizativas**

Creá de arriba hacia abajo — cada unidad necesita que su padre exista.

| Código | Nombre | Tipo | Padre | Área funcional |
|---|---|---|---|---|
| `DG` | Dirección General | `DIRECCION_GENERAL` | — | `FINANZAS` |
| `VP-OPS` | Vicepresidencia de Operaciones | `VICEPRESIDENCIA` | `DG` | `OPS_VUELO` |
| `GER-VUELO` | Gerencia de Operaciones de Vuelo | `GERENCIA` | `VP-OPS` | `OPS_VUELO` |
| `JEF-PILOTOS` | Jefatura de Pilotos | `JEFATURA` | `GER-VUELO` | `OPS_VUELO` |
| `DEP-DESPACHO` | Despacho y Control de Vuelo | `DEPARTAMENTO` | `GER-VUELO` | `OPS_VUELO` |
| `GER-ABORDO` | Gerencia de Servicio a Bordo | `GERENCIA` | `VP-OPS` | `SERV_ABORDO` |
| `GER-AEROP` | Gerencia de Aeropuertos | `GERENCIA` | `VP-OPS` | `AEROPUERTOS` |
| `DEP-RAMPA` | Rampa y Equipaje | `DEPARTAMENTO` | `GER-AEROP` | `AEROPUERTOS` |
| `DEP-COUNTER` | Counter y Sala de Abordaje | `DEPARTAMENTO` | `GER-AEROP` | `AEROPUERTOS` |
| `VP-TEC` | Vicepresidencia Técnica | `VICEPRESIDENCIA` | `DG` | `MANTENIMIENTO` |
| `GER-MTTO` | Gerencia de Mantenimiento en Línea | `GERENCIA` | `VP-TEC` | `MANTENIMIENTO` |
| `GER-ING` | Gerencia de Ingeniería y Planeación | `GERENCIA` | `VP-TEC` | `MANTENIMIENTO` |
| `DEP-ALMACEN` | Almacén Técnico | `DEPARTAMENTO` | `VP-TEC` | `MANTENIMIENTO` |
| `DIR-SEG` | Dirección de Seguridad Operacional | `DIRECCION` | `DG` | `SEG_OPERACIONAL` |
| `VP-COM` | Vicepresidencia Comercial | `VICEPRESIDENCIA` | `DG` | `COMERCIAL` |
| `GER-VENTAS` | Gerencia de Ventas | `GERENCIA` | `VP-COM` | `COMERCIAL` |
| `GER-CARGA` | Gerencia de Carga | `GERENCIA` | `VP-COM` | `CARGA` |
| `VP-FIN` | Vicepresidencia de Finanzas y Administración | `VICEPRESIDENCIA` | `DG` | `FINANZAS` |
| `DEP-CONTA` | Contabilidad | `DEPARTAMENTO` | `VP-FIN` | `FINANZAS` |
| `DEP-TESO` | Tesorería | `DEPARTAMENTO` | `VP-FIN` | `FINANZAS` |
| `DEP-COMPRAS` | Compras | `DEPARTAMENTO` | `VP-FIN` | `FINANZAS` |
| `DIR-GENTE` | Dirección de Gente y Cultura | `DIRECCION` | `DG` | `GENTE` |
| `DEP-ADMPER` | Administración de Personal | `DEPARTAMENTO` | `DIR-GENTE` | `GENTE` |
| `DEP-TALENTO` | Atracción de Talento | `DEPARTAMENTO` | `DIR-GENTE` | `GENTE` |
| `GER-TI` | Gerencia de Tecnología | `GERENCIA` | `DG` | `TECNOLOGIA` |
| `DIR-LEGAL` | Dirección Legal | `DIRECCION` | `DG` | `LEGAL` |

Verificá el resultado en **Estructura → Organigrama**. La vista de árbol debe mostrar una sola raíz (`DG`); si aparecen varias, alguna unidad quedó sin padre.

---

## 6. Centros de costo

Pantalla: **Configuración → Centros de costo → Tipos**, luego **Centros de costo**

Tipos:

| Código | Nombre |
|---|---|
| `OPERATIVO` | Operativo |
| `TECNICO` | Técnico |
| `COMERCIAL` | Comercial |
| `ADMINISTRATIVO` | Administrativo |

Centros:

| Código | Nombre | Tipo |
|---|---|---|
| `CC-1000` | Operaciones de Vuelo | `OPERATIVO` |
| `CC-1100` | Servicio a Bordo | `OPERATIVO` |
| `CC-1200` | Aeropuertos SAL | `OPERATIVO` |
| `CC-1300` | Seguridad Operacional | `OPERATIVO` |
| `CC-2000` | Mantenimiento en Línea | `TECNICO` |
| `CC-2100` | Ingeniería y Planeación | `TECNICO` |
| `CC-2200` | Almacén Técnico | `TECNICO` |
| `CC-3000` | Ventas | `COMERCIAL` |
| `CC-3100` | Carga | `COMERCIAL` |
| `CC-4000` | Finanzas | `ADMINISTRATIVO` |
| `CC-4100` | Gente y Cultura | `ADMINISTRATIVO` |
| `CC-4200` | Tecnología | `ADMINISTRATIVO` |
| `CC-4300` | Legal | `ADMINISTRATIVO` |
| `CC-9000` | Dirección General | `ADMINISTRATIVO` |

> El centro de costo se asigna en la **asignación del empleado**, no en la plaza. Es lo que agrupa la planilla para contabilidad.

---

## 7. Puestos y tabulador salarial

### 7.1 Niveles de pirámide ocupacional

Pantalla: **Configuración → Puestos → Niveles ocupacionales**

| Código | Nombre | Orden |
|---|---|---|
| `DIRECTIVO` | Directivo | 10 |
| `GERENCIAL` | Gerencial | 20 |
| `JEFATURA` | Jefatura / Supervisión | 30 |
| `PROFESIONAL` | Profesional | 40 |
| `TECNICO` | Técnico | 50 |
| `OPERATIVO` | Operativo | 60 |
| `APOYO` | Apoyo | 70 |

### 7.2 Categorías de puesto

Pantalla: **Configuración → Puestos → Categorías**

| Código | Nombre |
|---|---|
| `OPERATIVO_AEREO` | Operativo aéreo (tripulaciones) |
| `TECNICO_AERONAUTICO` | Técnico aeronáutico |
| `OPERATIVO_TIERRA` | Operativo en tierra |
| `COMERCIAL` | Comercial |
| `ADMINISTRATIVO` | Administrativo |

### 7.3 Perfiles de puesto

Pantalla: **Puestos → Perfiles de puesto**

| Código | Puesto | Nivel | Categoría | Unidad |
|---|---|---|---|---|
| `P-DG` | Director General | `DIRECTIVO` | `ADMINISTRATIVO` | `DG` |
| `P-VPOPS` | Vicepresidente de Operaciones | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-OPS` |
| `P-VPTEC` | Vicepresidente Técnico | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-TEC` |
| `P-VPCOM` | Vicepresidente Comercial | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-COM` |
| `P-VPFIN` | Vicepresidente de Finanzas | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-FIN` |
| `P-DIRSEG` | Director de Seguridad Operacional | `DIRECTIVO` | `ADMINISTRATIVO` | `DIR-SEG` |
| `P-DIRGENTE` | Director de Gente y Cultura | `DIRECTIVO` | `ADMINISTRATIVO` | `DIR-GENTE` |
| `P-JEFPIL` | Jefe de Pilotos | `JEFATURA` | `OPERATIVO_AEREO` | `JEF-PILOTOS` |
| `P-CMDTE` | Piloto Comandante | `PROFESIONAL` | `OPERATIVO_AEREO` | `JEF-PILOTOS` |
| `P-PRIOF` | Primer Oficial | `PROFESIONAL` | `OPERATIVO_AEREO` | `JEF-PILOTOS` |
| `P-DESPACH` | Despachador de Vuelo | `TECNICO` | `OPERATIVO_AEREO` | `DEP-DESPACHO` |
| `P-SOBJEFE` | Sobrecargo Jefe | `TECNICO` | `OPERATIVO_AEREO` | `GER-ABORDO` |
| `P-TCP` | Tripulante de Cabina | `TECNICO` | `OPERATIVO_AEREO` | `GER-ABORDO` |
| `P-GERAER` | Gerente de Aeropuertos | `GERENCIAL` | `OPERATIVO_TIERRA` | `GER-AEROP` |
| `P-SUPRAMPA` | Supervisor de Rampa | `JEFATURA` | `OPERATIVO_TIERRA` | `DEP-RAMPA` |
| `P-AGRAMPA` | Agente de Rampa | `OPERATIVO` | `OPERATIVO_TIERRA` | `DEP-RAMPA` |
| `P-AGPAX` | Agente de Servicio al Pasajero | `OPERATIVO` | `OPERATIVO_TIERRA` | `DEP-COUNTER` |
| `P-GERMTTO` | Gerente de Mantenimiento en Línea | `GERENCIAL` | `TECNICO_AERONAUTICO` | `GER-MTTO` |
| `P-TECAP` | Técnico Aeronáutico A&P | `TECNICO` | `TECNICO_AERONAUTICO` | `GER-MTTO` |
| `P-INGMTTO` | Ingeniero de Mantenimiento | `PROFESIONAL` | `TECNICO_AERONAUTICO` | `GER-ING` |
| `P-INSPCAL` | Inspector de Calidad | `PROFESIONAL` | `TECNICO_AERONAUTICO` | `DIR-SEG` |
| `P-ALMTEC` | Almacenista Técnico | `OPERATIVO` | `TECNICO_AERONAUTICO` | `DEP-ALMACEN` |
| `P-GERVTA` | Gerente de Ventas | `GERENCIAL` | `COMERCIAL` | `GER-VENTAS` |
| `P-EJEVTA` | Ejecutivo de Ventas | `PROFESIONAL` | `COMERCIAL` | `GER-VENTAS` |
| `P-AGCARGA` | Agente de Carga | `OPERATIVO` | `COMERCIAL` | `GER-CARGA` |
| `P-CONTGRAL` | Contador General | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-CONTA` |
| `P-ANACONT` | Analista Contable | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-CONTA` |
| `P-ANANOM` | Analista de Nómina | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-ADMPER` |
| `P-GENRRHH` | Generalista de Gente y Cultura | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-TALENTO` |
| `P-ANASIS` | Analista de Sistemas | `PROFESIONAL` | `ADMINISTRATIVO` | `GER-TI` |
| `P-ABOG` | Abogado Corporativo | `PROFESIONAL` | `ADMINISTRATIVO` | `DIR-LEGAL` |
| `P-RECEP` | Recepcionista | `APOYO` | `ADMINISTRATIVO` | `DG` |

### 7.4 Tabulador salarial

Pantalla: **Compensación → Tabulador salarial**

Bandas mensuales en USD. El tabulador se edita mediante **solicitudes de cambio**, no directamente — creás la solicitud, se revisa el impacto y se aplica.

| Puesto | Mínimo | Medio | Máximo |
|---|---|---|---|
| Director General | 8,000.00 | 10,000.00 | 12,000.00 |
| Vicepresidente (cualquiera) | 6,000.00 | 7,500.00 | 9,000.00 |
| Director de Seguridad Operacional | 5,000.00 | 6,250.00 | 7,500.00 |
| Director de Gente y Cultura | 4,500.00 | 5,750.00 | 7,000.00 |
| Jefe de Pilotos | 8,000.00 | 9,500.00 | 11,000.00 |
| Piloto Comandante | 7,000.00 | 9,000.00 | 11,000.00 |
| Primer Oficial | 3,500.00 | 4,500.00 | 5,500.00 |
| Gerente de Mantenimiento en Línea | 3,500.00 | 4,250.00 | 5,000.00 |
| Gerente de Aeropuertos | 3,000.00 | 3,750.00 | 4,500.00 |
| Gerente de Ventas | 2,800.00 | 3,500.00 | 4,200.00 |
| Ingeniero de Mantenimiento | 2,000.00 | 2,600.00 | 3,200.00 |
| Abogado Corporativo | 2,000.00 | 2,500.00 | 3,000.00 |
| Inspector de Calidad | 1,800.00 | 2,300.00 | 2,800.00 |
| Contador General | 1,800.00 | 2,300.00 | 2,800.00 |
| Sobrecargo Jefe | 1,400.00 | 1,800.00 | 2,200.00 |
| Técnico Aeronáutico A&P | 1,200.00 | 1,600.00 | 2,000.00 |
| Analista de Sistemas | 1,200.00 | 1,600.00 | 2,000.00 |
| Despachador de Vuelo | 900.00 | 1,200.00 | 1,500.00 |
| Supervisor de Rampa | 900.00 | 1,150.00 | 1,400.00 |
| Generalista de Gente y Cultura | 900.00 | 1,150.00 | 1,400.00 |
| Analista de Nómina | 800.00 | 1,050.00 | 1,300.00 |
| Tripulante de Cabina | 800.00 | 1,100.00 | 1,400.00 |
| Ejecutivo de Ventas | 800.00 | 1,100.00 | 1,400.00 |
| Analista Contable | 700.00 | 900.00 | 1,100.00 |
| Almacenista Técnico | 500.00 | 625.00 | 750.00 |
| Agente de Carga | 480.00 | 590.00 | 700.00 |
| Agente de Servicio al Pasajero | 450.00 | 575.00 | 700.00 |
| Agente de Rampa | 420.00 | 535.00 | 650.00 |
| Recepcionista | 408.80 | 480.00 | 550.00 |

> **Salario mínimo vigente: $408.80 mensuales.** Ninguna banda puede empezar por debajo. El motor de planilla tiene garantía de ingreso mínimo activada (§9.1) y va a levantar la línea si un cálculo cae abajo.

---

## 8. Plazas

Pantalla: **Puestos → Plazas**

La plaza es la posición presupuestada: puesto + centro de trabajo + dependencia + **salario base configurado**.

> **El salario vive en la plaza, no en el perfil de puesto.** El perfil define qué es el trabajo; la plaza define cuánto se paga por ella. Es el error de configuración más común.

Campos por plaza:

| Campo | Nota |
|---|---|
| Código | Convención sugerida: `PL-<puesto>-<correlativo>` — ej. `PL-AGRAMPA-001` |
| Perfil de puesto | de §7.3 |
| Centro de trabajo | de §4.3 |
| Dependencia directa | la plaza del jefe (armá el organigrama de plazas de arriba hacia abajo) |
| Máximo de empleados | 1 para puestos únicos; para plazas tipo pool (agentes de rampa) podés poner el total |
| Salario base configurado | dentro de la banda del tabulador |
| Moneda | `USD` |
| Vigente desde | fecha de inicio de operación |

Plazas mínimas para el ejemplo (≈45 empleados):

| Plaza | Puesto | Sede | Máx. empleados | Salario base |
|---|---|---|---|---|
| `PL-DG-001` | Director General | `SS-CORP` | 1 | 10,000.00 |
| `PL-VPOPS-001` | Vicepresidente de Operaciones | `SS-CORP` | 1 | 7,500.00 |
| `PL-VPTEC-001` | Vicepresidente Técnico | `SAL-HGR` | 1 | 7,500.00 |
| `PL-VPCOM-001` | Vicepresidente Comercial | `SS-CORP` | 1 | 7,500.00 |
| `PL-VPFIN-001` | Vicepresidente de Finanzas | `SS-CORP` | 1 | 7,500.00 |
| `PL-DIRSEG-001` | Director de Seguridad Operacional | `SS-CORP` | 1 | 6,250.00 |
| `PL-DIRGENTE-001` | Director de Gente y Cultura | `SS-CORP` | 1 | 5,750.00 |
| `PL-JEFPIL-001` | Jefe de Pilotos | `SAL-EST` | 1 | 9,500.00 |
| `PL-CMDTE-001` | Piloto Comandante | `SAL-EST` | 5 | 9,000.00 |
| `PL-PRIOF-001` | Primer Oficial | `SAL-EST` | 5 | 4,500.00 |
| `PL-DESPACH-001` | Despachador de Vuelo | `SAL-EST` | 2 | 1,200.00 |
| `PL-SOBJEFE-001` | Sobrecargo Jefe | `SAL-EST` | 1 | 1,800.00 |
| `PL-TCP-001` | Tripulante de Cabina | `SAL-EST` | 9 | 1,100.00 |
| `PL-GERAER-001` | Gerente de Aeropuertos | `SAL-EST` | 1 | 3,750.00 |
| `PL-SUPRAMPA-001` | Supervisor de Rampa | `SAL-EST` | 2 | 1,150.00 |
| `PL-AGRAMPA-001` | Agente de Rampa | `SAL-EST` | 5 | 535.00 |
| `PL-AGPAX-001` | Agente de Servicio al Pasajero | `SAL-EST` | 3 | 575.00 |
| `PL-GERMTTO-001` | Gerente de Mantenimiento en Línea | `SAL-HGR` | 1 | 4,250.00 |
| `PL-TECAP-001` | Técnico Aeronáutico A&P | `SAL-HGR` | 3 | 1,600.00 |
| `PL-INGMTTO-001` | Ingeniero de Mantenimiento | `SAL-HGR` | 1 | 2,600.00 |
| `PL-INSPCAL-001` | Inspector de Calidad | `SAL-HGR` | 1 | 2,300.00 |
| `PL-ALMTEC-001` | Almacenista Técnico | `SAL-HGR` | 1 | 625.00 |
| `PL-GERVTA-001` | Gerente de Ventas | `SS-CORP` | 1 | 3,500.00 |
| `PL-EJEVTA-001` | Ejecutivo de Ventas | `SS-CORP` | 2 | 1,100.00 |
| `PL-AGCARGA-001` | Agente de Carga | `SAL-CRG` | 1 | 590.00 |
| `PL-CONTGRAL-001` | Contador General | `SS-CORP` | 1 | 2,300.00 |
| `PL-ANACONT-001` | Analista Contable | `SS-CORP` | 1 | 900.00 |
| `PL-ANANOM-001` | Analista de Nómina | `SS-CORP` | 1 | 1,050.00 |
| `PL-GENRRHH-001` | Generalista de Gente y Cultura | `SS-CORP` | 1 | 1,150.00 |
| `PL-ANASIS-001` | Analista de Sistemas | `SS-CORP` | 1 | 1,600.00 |
| `PL-ABOG-001` | Abogado Corporativo | `SS-CORP` | 1 | 2,500.00 |
| `PL-RECEP-001` | Recepcionista | `SS-CORP` | 1 | 480.00 |

**Total: 45 posiciones.**

---

## 9. Nómina

### 9.1 Definición de nómina

Pantalla: **Planilla → Configuración → Nóminas**

Una sola definición, quincenal para todo el personal:

| Campo | Valor |
|---|---|
| Código | `NOM-QUINCENAL` |
| Nombre | Planilla Quincenal Avianca ES |
| Tipo de planilla | `QUINCENAL` |
| Periodicidad de pago | `QUINCENAL` |
| Periodos al año | `24` |
| Garantiza ingreso mínimo | Sí |
| Moneda | `USD` |
| Ventana de captura de horas extra | Habilitada, desfase `+2` días |
| Ventana de captura de asistencia | Habilitada, desfase `+2` días |

> El desfase `+2` significa que la ventana de captura cierra dos días después del fin del periodo. Si tu equipo de nómina necesita más margen, subilo — es el único campo aquí que conviene calibrar contra el proceso real.

> Si más adelante querés segregar administrativos de operativos en dos planillas, podés crear una segunda definición quincenal. La frecuencia se mantiene; lo que cambia es la agrupación y quién autoriza cada una.

### 9.2 Calendario de periodos

Pantalla: **Planilla → Periodos → Generar calendario**

Elegí la definición `NOM-QUINCENAL` y el año. Se generan 24 periodos: del 1 al 15 y del 16 al fin de mes.

> **La quincena siempre son 15 días comerciales**, aunque el mes tenga 28, 30 o 31 días. No es un error de redondeo — es la convención de cálculo del sistema. El salario diario es `mensual / 30` y la hora es `diaria / 8`.

Después de generar, revisá que las fechas de corte de captura quedaron razonables. Son **editables periodo por periodo** — ajustá los que caigan en asueto o fin de semana.

### 9.3 Jornadas

Pantalla: **Planilla → Configuración → Jornadas**

La semana legal son **44 horas** (Art. 161 CT). Las tres jornadas suman 44 exactas.

**`JORNADA_ADMIN` — Administrativa**
Clase `ORDINARIA`, ancla `ENTRADA`, total 44 h

| Día | Entrada | Salida | Comida | Netas |
|---|---|---|---|---|
| Lunes a viernes | 08:00 | 17:00 | 12:00–13:00 | 8.00 c/u |
| Sábado | 08:00 | 12:00 | — | 4.00 |

**`JORNADA_TURNO_AEROP` — Turnos rotativos de aeropuerto**
Clase `ORDINARIA`, ancla `ENTRADA`, total 44 h

| Día | Entrada | Salida | Comida | Netas |
|---|---|---|---|---|
| Lunes a jueves | 05:00 | 17:00 | 11:00–12:00 | 11.00 c/u |

> La rotación real no se modela en la jornada — se maneja con el **día de descanso de cada asignación** (§11.2) y los registros de asistencia. La jornada define el patrón de horas; quién trabaja qué día lo define la asignación.

**`JORNADA_TRIPULACION` — Tripulaciones**
Clase `ORDINARIA`, ancla `ENTRADA`, total 44 h

| Día | Entrada | Salida | Comida | Netas |
|---|---|---|---|---|
| Lunes a viernes | 06:00 | 15:00 | 11:00–12:00 | 8.00 c/u |
| Sábado | 06:00 | 10:00 | — | 4.00 |

> **Advertencia sobre tripulaciones.** El sistema no modela límites de tiempo de vuelo y servicio (FTL) ni descansos mínimos entre vuelos — esa normativa aeronáutica es independiente del Código de Trabajo y no existe en CLARIHR. Esta jornada es una **aproximación nominal de 44 h** que sirve para que la planilla calcule; la programación real de tripulaciones tiene que vivir en otro sistema. Si Avianca necesita FTL dentro de CLARIHR, es desarrollo nuevo, no configuración.

Podés inactivar `JORNADA_ORDINARIA` (la de fábrica) una vez que las tres estén creadas y ninguna asignación la use.

### 9.4 Asuetos

Pantalla: **Configuración → Asuetos**

Ya están cargados los del **año en curso**. Verificá que estén los nueve asuetos nacionales de El Salvador y agregá los del año siguiente antes de generar su calendario:

| Fecha | Asueto |
|---|---|
| 1 de enero | Año Nuevo |
| Jueves y Viernes Santo, Sábado de Gloria | Semana Santa (móvil) |
| 1 de mayo | Día del Trabajo |
| 10 de mayo | Día de la Madre |
| 17 de junio | Día del Padre |
| 1, 2 y 6 de agosto | Fiestas Agostinas |
| 15 de septiembre | Independencia |
| 2 de noviembre | Día de los Difuntos |
| 25 de diciembre | Navidad |

> Agregá también el día de la fiesta patronal del municipio donde está cada sede, si aplica. Los asuetos alimentan el cálculo de horas extra en asueto (`HEDF` / `HENF`), que se pagan al cuádruple y quíntuple.

---

## 10. Tablas de Renta (ISR) — paso obligatorio

Pantalla: **Planilla → Configuración → Tablas de retención de Renta**

**Tu compañía está vacía de tramos.** Las tablas de Renta se guardan por empresa y solo el sembrado de desarrollo las creaba. Sin ellas, la planilla corre pero **retiene $0.00 de ISR en todos los empleados** — sin error ni advertencia.

Cargá las tres tablas oficiales (DL 95/2015), vigentes desde `2024-01-01`. La periodicidad del periodo elige la tabla; con nómina quincenal usarás la segunda, pero cargá las tres.

**MENSUAL**

| # | Desde | Hasta | Cuota fija | % | Sobre exceso de |
|---|---|---|---|---|---|
| 1 | 0.01 | 472.00 | 0.00 | 0 % | 0.00 |
| 2 | 472.01 | 895.24 | 17.67 | 10 % | 472.00 |
| 3 | 895.25 | 2,038.10 | 60.00 | 20 % | 895.24 |
| 4 | 2,038.11 | — | 288.57 | 30 % | 2,038.10 |

**QUINCENAL** ← la que usa tu nómina

| # | Desde | Hasta | Cuota fija | % | Sobre exceso de |
|---|---|---|---|---|---|
| 1 | 0.01 | 236.00 | 0.00 | 0 % | 0.00 |
| 2 | 236.01 | 447.62 | 8.83 | 10 % | 236.00 |
| 3 | 447.63 | 1,019.05 | 30.00 | 20 % | 447.62 |
| 4 | 1,019.06 | — | 144.28 | 30 % | 1,019.05 |

**SEMANAL**

| # | Desde | Hasta | Cuota fija | % | Sobre exceso de |
|---|---|---|---|---|---|
| 1 | 0.01 | 118.00 | 0.00 | 0 % | 0.00 |
| 2 | 118.01 | 223.81 | 4.42 | 10 % | 118.00 |
| 3 | 223.82 | 509.52 | 15.00 | 20 % | 223.81 |
| 4 | 509.53 | — | 72.14 | 30 % | 509.52 |

> Las cifras son las oficiales del decreto. **No las derives aritméticamente** entre periodicidades: la tabla quincenal no es exactamente la mensual dividida entre dos, y la semanal tampoco. Copialas tal cual.

---

## 11. Personas

### 11.1 Usuarios y roles

Pantalla: **Configuración → Usuarios**

Al crear la compañía se generaron dos roles: **Administrador** (todos los permisos) y **Estándar**. El usuario que creó la empresa ya es administrador.

Invitá al menos:

| Usuario | Rol | Para qué |
|---|---|---|
| Analista de Nómina | rol nuevo *Nómina* | Captura y genera planillas; **no** las autoriza |
| Generalista de Gente | rol nuevo *RRHH* | Expedientes, acciones de personal |
| Vicepresidente de Finanzas | rol nuevo *Autorizador* | Autoriza y cierra planillas |

> **Separación obligatoria entre quien genera y quien autoriza.** El sistema tiene control anti-autoservicio en varios flujos: quien registra una acción no puede autorizarla. Si un solo usuario tiene ambos permisos, esos flujos se bloquean. Creá roles separados desde el inicio.

Antes de crear expedientes, revisá **Configuración → Autorizaciones**: el módulo de expedientes exige un conjunto de políticas de autorización configurado.

### 11.2 Expedientes de empleado

Pantalla: **Personal → Expedientes → Nuevo**

Distribución propuesta para llegar a 45:

| Área | Cantidad | Puestos |
|---|---|---|
| Dirección General | 1 | Director General |
| Operaciones de Vuelo | 13 | 1 jefe de pilotos, 5 comandantes, 5 primeros oficiales, 2 despachadores |
| Servicio a Bordo | 10 | 1 sobrecargo jefe, 9 tripulantes de cabina |
| Aeropuertos | 11 | 1 gerente, 2 supervisores de rampa, 5 agentes de rampa, 3 agentes de pasajero |
| Mantenimiento e Ingeniería | 7 | 1 VP técnico, 1 gerente, 3 técnicos A&P, 1 ingeniero, 1 almacenista |
| Seguridad Operacional | 2 | 1 director, 1 inspector de calidad |
| Comercial y Carga | 5 | 1 VP, 1 gerente de ventas, 2 ejecutivos, 1 agente de carga |
| Finanzas y Administración | 5 | 1 VP, 1 contador, 1 analista contable, 1 analista de nómina, 1 recepcionista |
| Gente y Cultura | 2 | 1 director, 1 generalista |
| Tecnología | 1 | 1 analista de sistemas |
| Legal | 1 | 1 abogado |

Datos mínimos por expediente:

| Bloque | Campos |
|---|---|
| Personales | Nombres, apellidos, fecha de nacimiento, nacionalidad, estado civil |
| Identificaciones | **DUI** (`########-#`) y **NIT** (`####-######-###-#`) |
| Previsión | AFP (`CONFIA` o `CRECER`) y número de cuenta AFP |
| Contacto | Correo personal, correo institucional, teléfono |
| Laboral | Código de empleado, fecha de contratación, estado `ACTIVO` |

**Asignación** (es donde se conecta todo — sin esto el empleado no entra en planilla):

| Campo | Valor |
|---|---|
| Plaza | de §8 |
| Unidad organizativa | de §5 |
| Centro de trabajo | de §4.3 |
| Centro de costo | de §6 |
| Tipo de planilla | `QUINCENAL` — **debe coincidir con el de la definición de nómina** |
| Jornada | `JORNADA_ADMIN`, `JORNADA_TURNO_AEROP` o `JORNADA_TRIPULACION` |
| Día de descanso | Domingo para administrativos; **variado** para turnos de aeropuerto |
| Tipo de contrato | Indefinido / Plazo fijo |
| Método de pago | `TRANSFERENCIA` |
| Cuenta bancaria | banco de §0 + número de cuenta |
| Es asignación principal | Sí |
| Vigente desde | fecha de contratación |

> **Tres validaciones cruzadas que fallan en silencio o con error críptico:**
> - El **tipo de planilla** de la asignación tiene que coincidir con el de la definición de nómina, o el empleado no aparece en la corrida.
> - El **código de jornada** tiene que existir como jornada activa. La comparación es exacta en mayúsculas.
> - El **día de descanso** afecta el cálculo del séptimo día. Para el personal de turnos, repartilos: si todos quedan en domingo, el reporte de descansos no refleja la operación real.

> **El método de pago y la cuenta bancaria viven en la asignación, no en el perfil personal.** La conciliación bancaria de la planilla emite una advertencia por cada empleado sin cuenta configurada.

---

## 12. Primera corrida de planilla

Pantalla: **Planilla → Corridas → Nueva**

1. Seleccioná la nómina `NOM-QUINCENAL` y un periodo abierto.
2. **Generar.** El motor arma las líneas: salario, ISSS, AFP, Renta, más las transacciones capturadas.
3. **Revisar.** Verificá en la bandeja:
   - Que aparezcan los 45 empleados.
   - Que el ISR **no** sea $0.00 en los salarios altos — si lo es, faltan las tablas de §10.
   - Que ninguna línea neta caiga bajo el mínimo de $408.80.
   - Advertencias de conciliación por empleados sin cuenta bancaria.
4. **Autorizar** — con un usuario distinto al que generó.
5. **Cerrar** el periodo.
6. Generá las **boletas de pago** (PDF individual o lote comprimido).

Para validar el cierre completo, corré los reportes legales: **Planilla → Reportes legales** (F-14, Planilla Única, Planilla Patronal). Si algún dato del perfil legal falta, los reportes lo señalan.

---

## Lista de verificación

**Identidad**
- [ ] Perfil legal con razón social, NIT, registro ISSS y dirección fiscal reales
- [ ] Representante legal creado y enlazado al perfil legal
- [ ] Preferencias: USD, `America/El_Salvador`, 15 días de vacación, 9 días de incapacidad patronal
- [ ] Área funcional de RRHH apuntando a `GENTE`

**Catálogos de empresa**
- [ ] 8 tipos de unidad organizativa; `UNIDAD` inactivada
- [ ] 11 áreas funcionales; `ADMIN`/`OPS`/`SALES` inactivadas
- [ ] 8 justificaciones de horas extra
- [ ] Reconocimientos y causas de amonestación ajustados
- [ ] **Multiplicadores de horas extra sin tocar** (2.00 / 2.50 / 4.00 / 5.00)

**Estructura**
- [ ] 5 centros de trabajo con municipio correcto
- [ ] 26 unidades organizativas — organigrama con una sola raíz
- [ ] 14 centros de costo

**Puestos**
- [ ] 7 niveles ocupacionales, 5 categorías
- [ ] 32 perfiles de puesto
- [ ] Tabulador cargado; ninguna banda bajo $408.80
- [ ] 45 plazas con salario base configurado **en la plaza**

**Nómina**
- [ ] Definición `NOM-QUINCENAL`, 24 periodos, garantía de mínimo activada
- [ ] Calendario anual generado; fechas de captura revisadas
- [ ] 3 jornadas de 44 h; `JORNADA_ORDINARIA` inactivada
- [ ] Asuetos del año en curso verificados; año siguiente cargado
- [ ] **Las 3 tablas de Renta cargadas** ← sin esto el ISR sale en cero

**Personas**
- [ ] Roles separados: quien genera ≠ quien autoriza
- [ ] Conjunto de políticas de autorización configurado
- [ ] 45 expedientes con DUI, NIT, AFP y cuenta bancaria
- [ ] Cada asignación con tipo de planilla `QUINCENAL` y jornada válida
- [ ] Días de descanso repartidos en el personal de turnos

**Validación**
- [ ] Planilla generada con 45 empleados
- [ ] ISR distinto de cero en salarios sobre el tramo exento
- [ ] Sin advertencias de conciliación bancaria
- [ ] Planilla autorizada por usuario distinto y periodo cerrado
- [ ] Boletas generadas y reportes legales sin datos faltantes

---

## Anexo — trampas conocidas

| Síntoma | Causa |
|---|---|
| El ISR sale $0.00 para todos | Faltan las tablas de Renta (§10). Se guardan por empresa y tu compañía nace sin ellas. |
| Un empleado no aparece en la corrida | El tipo de planilla de su asignación no coincide con el de la definición de nómina. |
| Error al guardar una asignación por la jornada | El código de jornada no existe o está inactivo. La comparación es exacta en mayúsculas. |
| El cálculo del séptimo día se ve raro | Día de descanso mal configurado en la asignación. En turnos no puede ser domingo para todos. |
| Advertencias de conciliación en toda la planilla | Cuentas bancarias sin configurar. Viven en la asignación, no en el perfil personal. |
| No se puede autorizar la planilla | El mismo usuario la generó. El control anti-autoservicio lo bloquea a propósito. |
| Los reportes legales salen con campos vacíos | Perfil legal incompleto o sin representante legal enlazado. |
| Aparece un error de conflicto al guardar | Alguien editó el registro entre que lo abriste y lo guardaste. Recargá la pantalla y repetí el cambio. |
| Los asuetos del año que viene no existen | Solo se siembran los del año en curso. Cargalos antes de generar el calendario del año siguiente. |
| Falta un catálogo de país (bancos, AFP, tipos de planilla) | Se borraron las filas con ID negativo. Requiere volver a correr migraciones. |
