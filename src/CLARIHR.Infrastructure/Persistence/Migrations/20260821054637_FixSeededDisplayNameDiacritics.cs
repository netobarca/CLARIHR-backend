using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CLARIHR.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixSeededDisplayNameDiacritics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "bank_catalog_items",
                keyColumn: "id",
                keyValue: -9002L,
                columns: new[] { "alias", "name", "normalized_alias" },
                values: new object[] { "Cuscatlán", "Cuscatlán", "CUSCATLÁN" });

            migrationBuilder.UpdateData(
                table: "bank_catalog_items",
                keyColumn: "id",
                keyValue: -9000L,
                columns: new[] { "alias", "name", "normalized_alias" },
                values: new object[] { "Agrícola", "Banco Agrícola", "AGRÍCOLA" });

            migrationBuilder.UpdateData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9753L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Rubro específico", "RUBRO ESPECÍFICO" });

            migrationBuilder.UpdateData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9752L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Ingreso base de cotización", "INGRESO BASE DE COTIZACIÓN" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8204L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Asociación Civil", "ASOCIACIÓN CIVIL" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8200L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Sociedad Anónima de Capital Variable", "SOCIEDAD ANÓNIMA DE CAPITAL VARIABLE" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8104L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Asociación", "ASOCIACIÓN" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8103L,
                column: "description",
                value: "Entidad asociativa organizada bajo el régimen cooperativo.");

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8102L,
                column: "description",
                value: "Operación empresarial inscrita a nombre de una sola persona.");

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8100L,
                columns: new[] { "description", "name", "normalized_name" },
                values: new object[] { "Sociedad mercantil con capital representado en acciones y posibilidad de variación de capital.", "Sociedad Anónima de Capital Variable", "SOCIEDAD ANÓNIMA DE CAPITAL VARIABLE" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9738L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Procuraduría", "PROCURADURÍA" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9730L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Daño de equipo", "DAÑO DE EQUIPO" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9724L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Viáticos", "VIÁTICOS" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9722L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Comisión", "COMISIÓN" });

            migrationBuilder.UpdateData(
                table: "currency_catalog_items",
                keyColumn: "id",
                keyValue: -9370L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Dólar estadounidense", "DÓLAR ESTADOUNIDENSE" });

            migrationBuilder.UpdateData(
                table: "duration_unit_catalog_items",
                keyColumn: "id",
                keyValue: -9441L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Día", "DÍA" });

            migrationBuilder.UpdateData(
                table: "language_catalog_items",
                keyColumn: "id",
                keyValue: -9411L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Español", "ESPAÑOL" });

            migrationBuilder.UpdateData(
                table: "language_catalog_items",
                keyColumn: "id",
                keyValue: -9410L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Inglés", "INGLÉS" });

            migrationBuilder.UpdateData(
                table: "language_level_catalog_items",
                keyColumn: "id",
                keyValue: -9422L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Básico", "BÁSICO" });

            migrationBuilder.UpdateData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9743L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Única", "ÚNICA" });

            migrationBuilder.UpdateData(
                table: "training_type_catalog_items",
                keyColumn: "id",
                keyValue: -9432L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Certificación", "CERTIFICACIÓN" });
        
        // 00005 / B-01 — las seis tablas de abajo NO se siembran con `HasData`, así que EF no las incluye
        // en el andamiaje: los catálogos territoriales y de referencia se crearon con SQL de una migración
        // anterior, y `location_groups` e `iam_permissions` los crea el aprovisionamiento de cada empresa
        // a partir de las plantillas del código. Arreglar la plantilla solo corrige a las empresas NUEVAS;
        // esto corrige las que ya existen. Se empareja por `code`, que es la clave estable.

        migrationBuilder.Sql(@"
            UPDATE department_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('AHUACHAPAN', 'Ahuachapán'),
                    ('CABANAS', 'Cabañas'),
                    ('CUSCATLAN', 'Cuscatlán'),
                    ('LA_UNION', 'La Unión'),
                    ('MORAZAN', 'Morazán'),
                    ('USULUTAN', 'Usulután')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE municipality_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('AHUACHAPAN_CENTRO', 'Ahuachapán Centro'),
                    ('AHUACHAPAN_NORTE', 'Ahuachapán Norte'),
                    ('AHUACHAPAN_SUR', 'Ahuachapán Sur'),
                    ('CABANAS_ESTE', 'Cabañas Este'),
                    ('CABANAS_OESTE', 'Cabañas Oeste'),
                    ('CUSCATLAN_NORTE', 'Cuscatlán Norte'),
                    ('CUSCATLAN_SUR', 'Cuscatlán Sur'),
                    ('LA_UNION_NORTE', 'La Unión Norte'),
                    ('LA_UNION_SUR', 'La Unión Sur'),
                    ('MORAZAN_NORTE', 'Morazán Norte'),
                    ('MORAZAN_SUR', 'Morazán Sur'),
                    ('USULUTAN_ESTE', 'Usulután Este'),
                    ('USULUTAN_NORTE', 'Usulután Norte'),
                    ('USULUTAN_OESTE', 'Usulután Oeste')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE location_groups AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('AHUACHAPAN', 'Ahuachapán'),
                    ('AHUACHAPAN_CENTRO', 'Ahuachapán Centro'),
                    ('AHUACHAPAN_NORTE', 'Ahuachapán Norte'),
                    ('AHUACHAPAN_SUR', 'Ahuachapán Sur'),
                    ('CABANAS', 'Cabañas'),
                    ('CABANAS_ESTE', 'Cabañas Este'),
                    ('CABANAS_OESTE', 'Cabañas Oeste'),
                    ('CUSCATLAN', 'Cuscatlán'),
                    ('CUSCATLAN_NORTE', 'Cuscatlán Norte'),
                    ('CUSCATLAN_SUR', 'Cuscatlán Sur'),
                    ('LA_UNION', 'La Unión'),
                    ('LA_UNION_NORTE', 'La Unión Norte'),
                    ('LA_UNION_SUR', 'La Unión Sur'),
                    ('MORAZAN', 'Morazán'),
                    ('MORAZAN_NORTE', 'Morazán Norte'),
                    ('MORAZAN_SUR', 'Morazán Sur'),
                    ('USULUTAN', 'Usulután'),
                    ('USULUTAN_ESTE', 'Usulután Este'),
                    ('USULUTAN_NORTE', 'Usulután Norte'),
                    ('USULUTAN_OESTE', 'Usulután Oeste')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE profession_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('DISENADOR_A_GRAFICO_A', 'Diseñador/a gráfico/a'),
                    ('MEDICO_A', 'Médico/a'),
                    ('ODONTOLOGO_A', 'Odontólogo/a'),
                    ('OPERARIO_A_DE_PRODUCCION', 'Operario/a de producción'),
                    ('PSICOLOGO_A', 'Psicólogo/a'),
                    ('TECNICO_A_DE_MANTENIMIENTO', 'Técnico/a de mantenimiento'),
                    ('TECNICO_A_DE_SOPORTE', 'Técnico/a de soporte')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE marital_status_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('UNION_NO_MATRIMONIAL', 'Unión no matrimonial')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE iam_permissions AS t
            SET name = v.name, description = v.description
            FROM (VALUES
                    ('CompanyUsers.Admin', 'Administrar usuarios de empresa', 'Administracion completa de usuarios operativos del tenant.'),
                    ('CompetencyFramework.Admin', 'Administrar marco de competencias', 'Administracion completa de competencias, conductas y piramide ocupacional.'),
                    ('CostCenters.Admin', 'Administrar centros de costo', 'Administracion completa de centros de costo contable.'),
                    ('EmployeeRelationsConfiguration.Admin', 'Administrar configuracion de relaciones laborales', 'Administracion completa de los maestros de configuracion de otras transacciones de personal y carga de la plantilla.'),
                    ('EmployeeRelationsConfiguration.Read', 'Leer configuracion de relaciones laborales', 'Consulta de los maestros de configuracion de otras transacciones de personal (tipos de reconocimiento, tipos y causas de amonestacion).'),
                    ('JobCatalogs.Admin', 'Administrar catalogos de puestos', 'Administracion de catalogos del manual de puestos.'),
                    ('JobProfiles.Admin', 'Administrar perfiles de puesto', 'Administracion completa de perfiles de puesto.'),
                    ('JobProfiles.Publish', 'Publicar perfiles de puesto', 'Publicar, reabrir y archivar perfiles de puesto. No implicado por la administracion de perfiles.'),
                    ('LeaveConfiguration.Admin', 'Administrar configuracion de vacaciones e incapacidades', 'Administracion completa de los maestros de configuracion de vacaciones e incapacidades y carga de la plantilla legal.'),
                    ('LeaveConfiguration.Read', 'Leer configuracion de vacaciones e incapacidades', 'Consulta de los maestros de configuracion de vacaciones e incapacidades (clinicas medicas, riesgos y tipos de incapacidad, asuetos y periodos de planilla).'),
                    ('LegalRepresentatives.Admin', 'Administrar representantes legales', 'Administracion completa de representantes legales.'),
                    ('LegalRepresentatives.Read', 'Leer representantes legales', 'Consulta de representantes legales activos e historicos.'),
                    ('Locations.Admin', 'Administrar ubicaciones y centros de trabajo', 'Administracion completa de ubicaciones y centros de trabajo.'),
                    ('OrgStructureCatalogs.Admin', 'Administrar catalogos de estructura organizativa', 'Administracion completa de catalogos de estructura organizativa.'),
                    ('OrgStructureCatalogs.Read', 'Leer catalogos de estructura organizativa', 'Consulta de catalogos de tipos de empresa, unidades y areas funcionales.'),
                    ('OrgUnits.Admin', 'Administrar unidades organizativas', 'Administracion completa de unidades organizativas.'),
                    ('OrgUnits.Read', 'Leer unidades organizativas', 'Consulta de unidades organizativas y su jerarquia.'),
                    ('PayrollConfiguration.Manage', 'Administrar configuracion de planillas', 'Administracion completa de los maestros de configuracion de planillas (nominas, jornadas laborales y carga de la plantilla).'),
                    ('PayrollConfiguration.Read', 'Leer configuracion de planillas', 'Consulta de los maestros de configuracion de planillas (nominas y jornadas laborales).'),
                    ('PersonnelFiles.Admin', 'Administrar expedientes de personal', 'Administracion completa de expedientes de personal.'),
                    ('PersonnelFiles.AuthorizeDisciplinaryActions', 'Autorizar amonestaciones', 'Decidir (aplicar/rechazar) y revocar las amonestaciones de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeOneTimeDeductions', 'Autorizar descuentos eventuales', 'Decidir (autorizar/rechazar) y revocar los descuentos eventuales de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeOneTimeIncomes', 'Autorizar ingresos eventuales', 'Decidir (autorizar/rechazar) y revocar los ingresos eventuales de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeOvertimeRecords', 'Autorizar horas extras', 'Decidir (autorizar/rechazar) y revocar las horas extras de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizePayrollRuns', 'Autorizar corridas de planilla', 'Autorizar una corrida de planilla o devolverla con motivo. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeRecognitions', 'Autorizar reconocimientos', 'Decidir (aplicar/rechazar) y revocar los reconocimientos de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeRecurringDeductions', 'Autorizar descuentos cíclicos', 'Decidir (autorizar/rechazar) y revocar los descuentos cíclicos de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeRecurringIncomes', 'Autorizar ingresos cíclicos', 'Decidir (autorizar/rechazar) y revocar los ingresos cíclicos de los empleados. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.AuthorizeRehire', 'Autorizar recontratacion', 'Autorizar la recontratacion de empleados marcados como no recontratables.'),
                    ('PersonnelFiles.AuthorizeRetirement', 'Autorizar retiros definitivos', 'Autorizar o rechazar las solicitudes de retiro definitivo (y anular una autorizada). No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.ManageDisciplinaryActions', 'Gestionar amonestaciones', 'Registrar, editar y anular amonestaciones (en revision) de los expedientes.'),
                    ('PersonnelFiles.ManageEconomicAidRequests', 'Gestionar ayuda economica', 'Validar (aprobar/rechazar), desembolsar, editar y dar de baja las solicitudes de ayuda economica de los expedientes.'),
                    ('PersonnelFiles.ManageExitInterviewForms', 'Gestionar formularios de entrevista de retiro', 'Disenar, publicar y asociar los formularios de entrevista de retiro (salida) de la institucion.'),
                    ('PersonnelFiles.ManageMedicalClaims', 'Gestionar reclamos de seguro medico', 'Crear, editar y eliminar los reclamos de seguro medico de los expedientes.'),
                    ('PersonnelFiles.ManageNotWorkedTimes', 'Gestionar tiempos no trabajados', 'Registrar y anular tiempos no trabajados (ausencias, suspensiones con descuento, llegadas tardias) con su descuento calculado.'),
                    ('PersonnelFiles.ManageOffPayrollTransactions', 'Gestionar transacciones fuera de nomina', 'Registrar, editar y eliminar las transacciones fuera de nomina de los expedientes.'),
                    ('PersonnelFiles.ManageOneTimeDeductions', 'Gestionar descuentos eventuales', 'Registrar, editar y anular descuentos eventuales, y aplicarlos (o revertir su aplicacion) en la planilla.'),
                    ('PersonnelFiles.ManageOvertimeRecords', 'Gestionar horas extras', 'Registrar, editar y anular horas extras, aplicarlas por periodo (unitario o en lote) y administrar los maestros de configuracion de horas extras (tipos, justificaciones y carga de la plantilla).'),
                    ('PersonnelFiles.ManageRecognitions', 'Gestionar reconocimientos', 'Registrar, editar y anular reconocimientos (en revision) de los expedientes.'),
                    ('PersonnelFiles.ManageSettlements', 'Gestionar liquidaciones', 'Crear, editar, emitir y anular las liquidaciones de personal, y administrar los escenarios de simulacion.'),
                    ('PersonnelFiles.RevertRetirement', 'Revertir retiros definitivos', 'Revertir un retiro definitivo ejecutado restaurando los estados del empleado. No implicado por la administracion de expedientes.'),
                    ('PersonnelFiles.ViewCompensation', 'Ver compensacion', 'Consulta de la compensacion (salario, ingresos y egresos) de los expedientes.'),
                    ('PersonnelFiles.ViewComplianceReports', 'Ver reportes legales de planilla', 'Consulta y descarga de los reportes legales de planilla: F-14, Planilla Unica y Planilla Patronal (REQ-016).'),
                    ('PersonnelFiles.ViewEconomicAidRequests', 'Ver ayuda economica', 'Consulta de las solicitudes de ayuda economica (asistencia por emergencia) de los expedientes.'),
                    ('PersonnelFiles.ViewMedicalClaims', 'Ver reclamos de seguro medico', 'Consulta de los reclamos de seguro medico (incluye el diagnostico, dato de salud) de los expedientes.'),
                    ('PersonnelFiles.ViewOffPayrollTransactions', 'Ver transacciones fuera de nomina', 'Consulta de las transacciones fuera de nomina (gastos de la empresa por el empleado) de los expedientes.'),
                    ('PersonnelFiles.ViewOvertimeRecords', 'Ver horas extras', 'Consulta de las horas extras de los expedientes, la bandeja de la empresa, sus exportaciones (insumo de planilla) y los maestros de configuracion de horas extras.'),
                    ('PositionDescriptionCatalogs.Admin', 'Administrar catalogos de descripcion de puesto', 'Administracion completa de catalogos de descripcion de puesto.'),
                    ('PositionDescriptionCatalogs.Read', 'Leer catalogos de descripcion de puesto', 'Consulta de catalogos de descripcion de puesto.'),
                    ('PositionSlots.Admin', 'Administrar plazas', 'Administracion completa de plazas y ocupacion.'),
                    ('RBAC.PERMISSIONS.MANAGE', 'Gestionar permisos', 'Administracion de permisos del tenant.'),
                    ('RBAC.ROLES.MANAGE', 'Gestionar roles', 'Administracion de roles del tenant.'),
                    ('RBAC.USERS.MANAGE', 'Gestionar usuarios', 'Administracion de usuarios del tenant.'),
                    ('SalaryTabulator.Admin', 'Administrar tabulador salarial', 'Administracion completa del tabulador salarial.'),
                    ('SalaryTabulator.Approve', 'Aprobar cambios de tabulador salarial', 'Aprobacion o rechazo de solicitudes del tabulador salarial.'),
                    ('SalaryTabulator.Read', 'Leer tabulador salarial', 'Consulta de lineas y solicitudes del tabulador salarial.'),
                    ('SalaryTabulator.Request', 'Solicitar cambios de tabulador salarial', 'Creacion y gestion de solicitudes de cambio al tabulador salarial.'),
                    ('WorkCenters.Admin', 'Administrar centros de trabajo', 'Administracion completa de centros de trabajo y tipos de centro del tenant.'),
                    ('iam.administration.manage', 'Administrar IAM', 'Administracion completa de identidad.')
                 ) AS v(code, name, description)
            -- `iam_permissions.code` se guarda en MAYUSCULAS y la constante del codigo es PascalCase:
            -- emparejar tal cual no casa ni una fila. Se compara plegado a mayusculas.
            WHERE upper(t.code) = upper(v.code);
");
    }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "bank_catalog_items",
                keyColumn: "id",
                keyValue: -9002L,
                columns: new[] { "alias", "name", "normalized_alias" },
                values: new object[] { "Cuscatlan", "Cuscatlan", "CUSCATLAN" });

            migrationBuilder.UpdateData(
                table: "bank_catalog_items",
                keyColumn: "id",
                keyValue: -9000L,
                columns: new[] { "alias", "name", "normalized_alias" },
                values: new object[] { "Agricola", "Banco Agricola", "AGRICOLA" });

            migrationBuilder.UpdateData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9753L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Rubro especifico", "RUBRO ESPECIFICO" });

            migrationBuilder.UpdateData(
                table: "calculation_base_catalog_items",
                keyColumn: "id",
                keyValue: -9752L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Ingreso base de cotizacion", "INGRESO BASE DE COTIZACION" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8204L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Asociacion Civil", "ASOCIACION CIVIL" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8200L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Sociedad Anonima de Capital Variable", "SOCIEDAD ANONIMA DE CAPITAL VARIABLE" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8104L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Asociacion", "ASOCIACION" });

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8103L,
                column: "description",
                value: "Entidad asociativa organizada bajo el regimen cooperativo.");

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8102L,
                column: "description",
                value: "Operacion empresarial inscrita a nombre de una sola persona.");

            migrationBuilder.UpdateData(
                table: "company_type_catalog_items",
                keyColumn: "id",
                keyValue: -8100L,
                columns: new[] { "description", "name", "normalized_name" },
                values: new object[] { "Sociedad mercantil con capital representado en acciones y posibilidad de variacion de capital.", "Sociedad Anonima de Capital Variable", "SOCIEDAD ANONIMA DE CAPITAL VARIABLE" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9738L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Procuraduria", "PROCURADURIA" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9730L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Dano de equipo", "DANO DE EQUIPO" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9724L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Viaticos", "VIATICOS" });

            migrationBuilder.UpdateData(
                table: "compensation_concept_type_catalog_items",
                keyColumn: "id",
                keyValue: -9722L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Comision", "COMISION" });

            migrationBuilder.UpdateData(
                table: "currency_catalog_items",
                keyColumn: "id",
                keyValue: -9370L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Dolar estadounidense", "DOLAR ESTADOUNIDENSE" });

            migrationBuilder.UpdateData(
                table: "duration_unit_catalog_items",
                keyColumn: "id",
                keyValue: -9441L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Dia", "DIA" });

            migrationBuilder.UpdateData(
                table: "language_catalog_items",
                keyColumn: "id",
                keyValue: -9411L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Espanol", "ESPANOL" });

            migrationBuilder.UpdateData(
                table: "language_catalog_items",
                keyColumn: "id",
                keyValue: -9410L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Ingles", "INGLES" });

            migrationBuilder.UpdateData(
                table: "language_level_catalog_items",
                keyColumn: "id",
                keyValue: -9422L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Basico", "BASICO" });

            migrationBuilder.UpdateData(
                table: "pay_period_catalog_items",
                keyColumn: "id",
                keyValue: -9743L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Unica", "UNICA" });

            migrationBuilder.UpdateData(
                table: "training_type_catalog_items",
                keyColumn: "id",
                keyValue: -9432L,
                columns: new[] { "name", "normalized_name" },
                values: new object[] { "Certificacion", "CERTIFICACION" });
        
        // 00005 / B-01 — las seis tablas de abajo NO se siembran con `HasData`, así que EF no las incluye
        // en el andamiaje: los catálogos territoriales y de referencia se crearon con SQL de una migración
        // anterior, y `location_groups` e `iam_permissions` los crea el aprovisionamiento de cada empresa
        // a partir de las plantillas del código. Arreglar la plantilla solo corrige a las empresas NUEVAS;
        // esto corrige las que ya existen. Se empareja por `code`, que es la clave estable.

        migrationBuilder.Sql(@"
            UPDATE department_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('AHUACHAPAN', 'Ahuachapan'),
                    ('CABANAS', 'Cabanas'),
                    ('CUSCATLAN', 'Cuscatlan'),
                    ('LA_UNION', 'La Union'),
                    ('MORAZAN', 'Morazan'),
                    ('USULUTAN', 'Usulutan')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE municipality_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('AHUACHAPAN_CENTRO', 'Ahuachapan Centro'),
                    ('AHUACHAPAN_NORTE', 'Ahuachapan Norte'),
                    ('AHUACHAPAN_SUR', 'Ahuachapan Sur'),
                    ('CABANAS_ESTE', 'Cabanas Este'),
                    ('CABANAS_OESTE', 'Cabanas Oeste'),
                    ('CUSCATLAN_NORTE', 'Cuscatlan Norte'),
                    ('CUSCATLAN_SUR', 'Cuscatlan Sur'),
                    ('LA_UNION_NORTE', 'La Union Norte'),
                    ('LA_UNION_SUR', 'La Union Sur'),
                    ('MORAZAN_NORTE', 'Morazan Norte'),
                    ('MORAZAN_SUR', 'Morazan Sur'),
                    ('USULUTAN_ESTE', 'Usulutan Este'),
                    ('USULUTAN_NORTE', 'Usulutan Norte'),
                    ('USULUTAN_OESTE', 'Usulutan Oeste')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE location_groups AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('AHUACHAPAN', 'Ahuachapan'),
                    ('AHUACHAPAN_CENTRO', 'Ahuachapan Centro'),
                    ('AHUACHAPAN_NORTE', 'Ahuachapan Norte'),
                    ('AHUACHAPAN_SUR', 'Ahuachapan Sur'),
                    ('CABANAS', 'Cabanas'),
                    ('CABANAS_ESTE', 'Cabanas Este'),
                    ('CABANAS_OESTE', 'Cabanas Oeste'),
                    ('CUSCATLAN', 'Cuscatlan'),
                    ('CUSCATLAN_NORTE', 'Cuscatlan Norte'),
                    ('CUSCATLAN_SUR', 'Cuscatlan Sur'),
                    ('LA_UNION', 'La Union'),
                    ('LA_UNION_NORTE', 'La Union Norte'),
                    ('LA_UNION_SUR', 'La Union Sur'),
                    ('MORAZAN', 'Morazan'),
                    ('MORAZAN_NORTE', 'Morazan Norte'),
                    ('MORAZAN_SUR', 'Morazan Sur'),
                    ('USULUTAN', 'Usulutan'),
                    ('USULUTAN_ESTE', 'Usulutan Este'),
                    ('USULUTAN_NORTE', 'Usulutan Norte'),
                    ('USULUTAN_OESTE', 'Usulutan Oeste')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE profession_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('DISENADOR_A_GRAFICO_A', 'Disenador/a grafico/a'),
                    ('MEDICO_A', 'Medico/a'),
                    ('ODONTOLOGO_A', 'Odontologo/a'),
                    ('OPERARIO_A_DE_PRODUCCION', 'Operario/a de produccion'),
                    ('PSICOLOGO_A', 'Psicologo/a'),
                    ('TECNICO_A_DE_MANTENIMIENTO', 'Tecnico/a de mantenimiento'),
                    ('TECNICO_A_DE_SOPORTE', 'Tecnico/a de soporte')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE marital_status_catalog_items AS t
            SET name = v.name, normalized_name = upper(v.name)
            FROM (VALUES
                    ('UNION_NO_MATRIMONIAL', 'Union no matrimonial')
                 ) AS v(code, name)
            WHERE t.code = v.code;

            UPDATE iam_permissions AS t
            SET name = v.name, description = v.description
            FROM (VALUES
                    ('CompanyUsers.Admin', 'Administrar usuarios de empresa', 'Administración completa de usuarios operativos del tenant.'),
                    ('CompetencyFramework.Admin', 'Administrar marco de competencias', 'Administración completa de competencias, conductas y pirámide ocupacional.'),
                    ('CostCenters.Admin', 'Administrar centros de costo', 'Administración completa de centros de costo contable.'),
                    ('EmployeeRelationsConfiguration.Admin', 'Administrar configuración de relaciones laborales', 'Administración completa de los maestros de configuración de otras transacciones de personal y carga de la plantilla.'),
                    ('EmployeeRelationsConfiguration.Read', 'Leer configuración de relaciones laborales', 'Consulta de los maestros de configuración de otras transacciones de personal (tipos de reconocimiento, tipos y causas de amonestación).'),
                    ('JobCatalogs.Admin', 'Administrar catálogos de puestos', 'Administración de catálogos del manual de puestos.'),
                    ('JobProfiles.Admin', 'Administrar perfiles de puesto', 'Administración completa de perfiles de puesto.'),
                    ('JobProfiles.Publish', 'Publicar perfiles de puesto', 'Publicar, reabrir y archivar perfiles de puesto. No implicado por la administración de perfiles.'),
                    ('LeaveConfiguration.Admin', 'Administrar configuración de vacaciones e incapacidades', 'Administración completa de los maestros de configuración de vacaciones e incapacidades y carga de la plantilla legal.'),
                    ('LeaveConfiguration.Read', 'Leer configuración de vacaciones e incapacidades', 'Consulta de los maestros de configuración de vacaciones e incapacidades (clínicas médicas, riesgos y tipos de incapacidad, asuetos y periodos de planilla).'),
                    ('LegalRepresentatives.Admin', 'Administrar representantes legales', 'Administración completa de representantes legales.'),
                    ('LegalRepresentatives.Read', 'Leer representantes legales', 'Consulta de representantes legales activos e históricos.'),
                    ('Locations.Admin', 'Administrar ubicaciones y centros de trabajo', 'Administración completa de ubicaciones y centros de trabajo.'),
                    ('OrgStructureCatalogs.Admin', 'Administrar catálogos de estructura organizativa', 'Administración completa de catálogos de estructura organizativa.'),
                    ('OrgStructureCatalogs.Read', 'Leer catálogos de estructura organizativa', 'Consulta de catálogos de tipos de empresa, unidades y areas funcionales.'),
                    ('OrgUnits.Admin', 'Administrar unidades organizativas', 'Administración completa de unidades organizativas.'),
                    ('OrgUnits.Read', 'Leer unidades organizativas', 'Consulta de unidades organizativas y su jerarquía.'),
                    ('PayrollConfiguration.Manage', 'Administrar configuración de planillas', 'Administración completa de los maestros de configuración de planillas (nóminas, jornadas laborales y carga de la plantilla).'),
                    ('PayrollConfiguration.Read', 'Leer configuración de planillas', 'Consulta de los maestros de configuración de planillas (nóminas y jornadas laborales).'),
                    ('PersonnelFiles.Admin', 'Administrar expedientes de personal', 'Administración completa de expedientes de personal.'),
                    ('PersonnelFiles.AuthorizeDisciplinaryActions', 'Autorizar amonestaciones', 'Decidir (aplicar/rechazar) y revocar las amonestaciones de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeOneTimeDeductions', 'Autorizar descuentos eventuales', 'Decidir (autorizar/rechazar) y revocar los descuentos eventuales de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeOneTimeIncomes', 'Autorizar ingresos eventuales', 'Decidir (autorizar/rechazar) y revocar los ingresos eventuales de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeOvertimeRecords', 'Autorizar horas extras', 'Decidir (autorizar/rechazar) y revocar las horas extras de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizePayrollRuns', 'Autorizar corridas de planilla', 'Autorizar una corrida de planilla o devolverla con motivo. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeRecognitions', 'Autorizar reconocimientos', 'Decidir (aplicar/rechazar) y revocar los reconocimientos de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeRecurringDeductions', 'Autorizar descuentos cíclicos', 'Decidir (autorizar/rechazar) y revocar los descuentos cíclicos de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeRecurringIncomes', 'Autorizar ingresos cíclicos', 'Decidir (autorizar/rechazar) y revocar los ingresos cíclicos de los empleados. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.AuthorizeRehire', 'Autorizar recontratación', 'Autorizar la recontratación de empleados marcados como no recontratables.'),
                    ('PersonnelFiles.AuthorizeRetirement', 'Autorizar retiros definitivos', 'Autorizar o rechazar las solicitudes de retiro definitivo (y anular una autorizada). No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.ManageDisciplinaryActions', 'Gestionar amonestaciones', 'Registrar, editar y anular amonestaciones (en revisión) de los expedientes.'),
                    ('PersonnelFiles.ManageEconomicAidRequests', 'Gestionar ayuda económica', 'Validar (aprobar/rechazar), desembolsar, editar y dar de baja las solicitudes de ayuda económica de los expedientes.'),
                    ('PersonnelFiles.ManageExitInterviewForms', 'Gestionar formularios de entrevista de retiro', 'Disenar, publicar y asociar los formularios de entrevista de retiro (salida) de la institución.'),
                    ('PersonnelFiles.ManageMedicalClaims', 'Gestionar reclamos de seguro médico', 'Crear, editar y eliminar los reclamos de seguro médico de los expedientes.'),
                    ('PersonnelFiles.ManageNotWorkedTimes', 'Gestionar tiempos no trabajados', 'Registrar y anular tiempos no trabajados (ausencias, suspensiones con descuento, llegadas tardías) con su descuento calculado.'),
                    ('PersonnelFiles.ManageOffPayrollTransactions', 'Gestionar transacciones fuera de nómina', 'Registrar, editar y eliminar las transacciones fuera de nómina de los expedientes.'),
                    ('PersonnelFiles.ManageOneTimeDeductions', 'Gestionar descuentos eventuales', 'Registrar, editar y anular descuentos eventuales, y aplicarlos (o revertir su aplicación) en la planilla.'),
                    ('PersonnelFiles.ManageOvertimeRecords', 'Gestionar horas extras', 'Registrar, editar y anular horas extras, aplicarlas por periodo (unitario o en lote) y administrar los maestros de configuración de horas extras (tipos, justificaciones y carga de la plantilla).'),
                    ('PersonnelFiles.ManageRecognitions', 'Gestionar reconocimientos', 'Registrar, editar y anular reconocimientos (en revisión) de los expedientes.'),
                    ('PersonnelFiles.ManageSettlements', 'Gestionar liquidaciones', 'Crear, editar, emitir y anular las liquidaciones de personal, y administrar los escenarios de simulación.'),
                    ('PersonnelFiles.RevertRetirement', 'Revertir retiros definitivos', 'Revertir un retiro definitivo ejecutado restaurando los estados del empleado. No implicado por la administración de expedientes.'),
                    ('PersonnelFiles.ViewCompensation', 'Ver compensación', 'Consulta de la compensación (salario, ingresos y egresos) de los expedientes.'),
                    ('PersonnelFiles.ViewComplianceReports', 'Ver reportes legales de planilla', 'Consulta y descarga de los reportes legales de planilla: F-14, Planilla Única y Planilla Patronal (REQ-016).'),
                    ('PersonnelFiles.ViewEconomicAidRequests', 'Ver ayuda económica', 'Consulta de las solicitudes de ayuda económica (asistencia por emergencia) de los expedientes.'),
                    ('PersonnelFiles.ViewMedicalClaims', 'Ver reclamos de seguro médico', 'Consulta de los reclamos de seguro médico (incluye el diagnóstico, dato de salud) de los expedientes.'),
                    ('PersonnelFiles.ViewOffPayrollTransactions', 'Ver transacciones fuera de nómina', 'Consulta de las transacciones fuera de nómina (gastos de la empresa por el empleado) de los expedientes.'),
                    ('PersonnelFiles.ViewOvertimeRecords', 'Ver horas extras', 'Consulta de las horas extras de los expedientes, la bandeja de la empresa, sus exportaciones (insumo de planilla) y los maestros de configuración de horas extras.'),
                    ('PositionDescriptionCatalogs.Admin', 'Administrar catálogos de descripción de puesto', 'Administración completa de catálogos de descripción de puesto.'),
                    ('PositionDescriptionCatalogs.Read', 'Leer catálogos de descripción de puesto', 'Consulta de catálogos de descripción de puesto.'),
                    ('PositionSlots.Admin', 'Administrar plazas', 'Administración completa de plazas y ocupación.'),
                    ('RBAC.PERMISSIONS.MANAGE', 'Gestionar permisos', 'Administración de permisos del tenant.'),
                    ('RBAC.ROLES.MANAGE', 'Gestionar roles', 'Administración de roles del tenant.'),
                    ('RBAC.USERS.MANAGE', 'Gestionar usuarios', 'Administración de usuarios del tenant.'),
                    ('SalaryTabulator.Admin', 'Administrar tabulador salarial', 'Administración completa del tabulador salarial.'),
                    ('SalaryTabulator.Approve', 'Aprobar cambios de tabulador salarial', 'Aprobación o rechazo de solicitudes del tabulador salarial.'),
                    ('SalaryTabulator.Read', 'Leer tabulador salarial', 'Consulta de líneas y solicitudes del tabulador salarial.'),
                    ('SalaryTabulator.Request', 'Solicitar cambios de tabulador salarial', 'Creación y gestión de solicitudes de cambio al tabulador salarial.'),
                    ('WorkCenters.Admin', 'Administrar centros de trabajo', 'Administración completa de centros de trabajo y tipos de centro del tenant.'),
                    ('iam.administration.manage', 'Administrar IAM', 'Administración completa de identidad.')
                 ) AS v(code, name, description)
            -- `iam_permissions.code` se guarda en MAYUSCULAS y la constante del codigo es PascalCase:
            -- emparejar tal cual no casa ni una fila. Se compara plegado a mayusculas.
            WHERE upper(t.code) = upper(v.code);
");
    }
    }
}
