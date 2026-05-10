using System.Globalization;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CLARIHR.Infrastructure.Reports.Documents;

internal sealed class JobProfilePdfRenderer : IDocumentPdfRenderer<JobProfilePrintResponse>
{
    private const string AccentColorHex = "#1F3A8A";
    private const string MutedColorHex = "#6B7280";
    private const string EmptyValuePlaceholder = "—";

    public Task RenderAsync(JobProfilePrintResponse payload, Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(destination);

        var profile = payload.Profile;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(36);
                page.Size(PageSizes.Letter);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(text => text.FontSize(10).FontFamily(Fonts.Calibri));

                page.Header().Element(header => ComposeHeader(header, profile, payload.GeneratedAtUtc));
                page.Content().Element(content => ComposeContent(content, profile));
                page.Footer().Element(ComposeFooter);
            });
        });

        document.GeneratePdf(destination);
        return Task.CompletedTask;
    }

    private static void ComposeHeader(IContainer container, JobProfileResponse profile, DateTime generatedAtUtc)
    {
        container.Column(column =>
        {
            column.Spacing(2);
            column.Item().Text(profile.Title)
                .FontSize(20).Bold().FontColor(AccentColorHex);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span("Código: ").SemiBold();
                    text.Span(profile.Code);
                });
                row.RelativeItem().Text(text =>
                {
                    text.Span("Versión: ").SemiBold();
                    text.Span(profile.Version.ToString(CultureInfo.InvariantCulture));
                });
                row.RelativeItem().Text(text =>
                {
                    text.Span("Estado: ").SemiBold();
                    text.Span(FormatStatus(profile.Status));
                });
            });

            column.Item().Text(text =>
            {
                text.Span("Generado: ").SemiBold().FontColor(MutedColorHex);
                text.Span(generatedAtUtc.ToString("u", CultureInfo.InvariantCulture)).FontColor(MutedColorHex);
            });

            column.Item().PaddingTop(6).LineHorizontal(0.6f).LineColor(AccentColorHex);
        });
    }

    private static void ComposeContent(IContainer container, JobProfileResponse profile)
    {
        container.PaddingVertical(8).Column(column =>
        {
            column.Spacing(10);

            ComposeGeneralInformation(column, profile);
            ComposeOptionalParagraph(column, "Objetivo", profile.Objective);
            ComposeOptionalParagraph(column, "Responsabilidades", profile.Responsibilities);
            ComposeOptionalParagraph(column, "Recursos asignados", profile.AssignedResources);
            ComposeOptionalParagraph(column, "Alcance de decisión", profile.DecisionScope);

            ComposeFunctionsSection(column, profile.Functions);
            ComposeRequirementsSection(column, profile.Requirements);
            ComposeCompetenciesSection(column, profile.Competencies);
            ComposeTrainingsSection(column, profile.Trainings);
            ComposeBenefitsSection(column, profile.Benefits, profile.BenefitsSummary);
            ComposeWorkingConditionsSection(column, profile.WorkingConditions, profile.WorkingConditionSummary);
            ComposeCompensationSection(column, profile.Compensation, profile.MarketSalaryReference);
            ComposeRelationsSection(column, profile.Relations);
            ComposeDependentPositionsSection(column, profile.DependentPositions);
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(style => style.FontSize(8).FontColor(MutedColorHex));
            text.Span("Generado por CLARIHR · Página ");
            text.CurrentPageNumber();
            text.Span(" de ");
            text.TotalPages();
        });
    }

    private static void ComposeGeneralInformation(ColumnDescriptor column, JobProfileResponse profile)
    {
        ComposeSectionTitle(column, "Información general");
        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.RelativeColumn(2);
                definition.RelativeColumn(5);
            });

            AddKeyValueRow(table, "Unidad organizativa", profile.OrgUnitName);
            AddKeyValueRow(table, "Reporta a", FormatReportsTo(profile));
            AddKeyValueRow(table, "Vigencia desde", FormatNullableDate(profile.EffectiveFromUtc));
            AddKeyValueRow(table, "Vigencia hasta", FormatNullableDate(profile.EffectiveToUtc));
            AddKeyValueRow(table, "Activo", profile.IsActive ? "Sí" : "No");
        });
    }

    private static void ComposeOptionalParagraph(ColumnDescriptor column, string title, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ComposeSectionTitle(column, title);
        column.Item().Text(value);
    }

    private static void ComposeFunctionsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileFunctionResponse> functions)
    {
        ComposeSectionTitle(column, "Funciones");
        if (functions.Count == 0)
        {
            ComposeEmpty(column);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.ConstantColumn(80);
                definition.RelativeColumn();
            });

            ComposeTableHeader(table, "Tipo", "Descripción");

            foreach (var function in functions.OrderBy(f => f.SortOrder))
            {
                table.Cell().Element(BodyCell).Text(FormatFunctionType(function.FunctionType));
                table.Cell().Element(BodyCell).Text(function.Description);
            }
        });
    }

    private static void ComposeRequirementsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileRequirementResponse> requirements)
    {
        ComposeSectionTitle(column, "Requisitos");
        if (requirements.Count == 0)
        {
            ComposeEmpty(column);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.ConstantColumn(110);
                definition.RelativeColumn();
            });

            ComposeTableHeader(table, "Tipo", "Descripción");

            foreach (var requirement in requirements.OrderBy(r => r.SortOrder))
            {
                table.Cell().Element(BodyCell).Text(FormatRequirementType(requirement.RequirementType));
                table.Cell().Element(BodyCell).Text(requirement.Description);
            }
        });
    }

    private static void ComposeCompetenciesSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileCompetencyResponse> competencies)
    {
        ComposeSectionTitle(column, "Competencias");
        if (competencies.Count == 0)
        {
            ComposeEmpty(column);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.RelativeColumn(2);
                definition.RelativeColumn(3);
                definition.RelativeColumn(2);
                definition.RelativeColumn(3);
            });

            ComposeTableHeader(table, "Pirámide", "Competencia", "Nivel esperado", "Conductas / Evidencia");

            foreach (var competency in competencies.OrderBy(c => c.SortOrder))
            {
                table.Cell().Element(BodyCell).Text(competency.OccupationalPyramidLevelName);
                table.Cell().Element(BodyCell).Text($"{competency.CompetencyName} ({competency.CompetencyTypeName})");
                table.Cell().Element(BodyCell).Text(competency.BehaviorLevelName);
                table.Cell().Element(BodyCell).Text(BuildCompetencyEvidence(competency));
            }
        });
    }

    private static void ComposeTrainingsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileTrainingResponse> trainings)
    {
        ComposeSectionTitle(column, "Entrenamientos sugeridos");
        if (trainings.Count == 0)
        {
            ComposeEmpty(column);
            return;
        }

        column.Item().Column(items =>
        {
            items.Spacing(2);
            foreach (var training in trainings.OrderBy(t => t.SortOrder))
            {
                items.Item().Text(text =>
                {
                    text.Span("• ");
                    text.Span(training.Name).SemiBold();
                    if (!string.IsNullOrWhiteSpace(training.Notes))
                    {
                        text.Span(" — ");
                        text.Span(training.Notes);
                    }
                });
            }
        });
    }

    private static void ComposeBenefitsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileBenefitResponse> benefits, string? summary)
    {
        ComposeSectionTitle(column, "Beneficios");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            column.Item().Text(summary).Italic().FontColor(MutedColorHex);
        }

        if (benefits.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                ComposeEmpty(column);
            }
            return;
        }

        column.Item().Column(items =>
        {
            items.Spacing(2);
            foreach (var benefit in benefits.OrderBy(b => b.SortOrder))
            {
                items.Item().Text(text =>
                {
                    text.Span("• ");
                    text.Span(benefit.Name).SemiBold();
                    if (!string.IsNullOrWhiteSpace(benefit.Notes))
                    {
                        text.Span(" — ");
                        text.Span(benefit.Notes);
                    }
                });
            }
        });
    }

    private static void ComposeWorkingConditionsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileWorkingConditionResponse> conditions, string? summary)
    {
        ComposeSectionTitle(column, "Condiciones laborales");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            column.Item().Text(summary).Italic().FontColor(MutedColorHex);
        }

        if (conditions.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                ComposeEmpty(column);
            }
            return;
        }

        column.Item().Column(items =>
        {
            items.Spacing(2);
            foreach (var condition in conditions.OrderBy(c => c.SortOrder))
            {
                items.Item().Text(text =>
                {
                    text.Span("• ");
                    text.Span(condition.Name).SemiBold();
                    if (!string.IsNullOrWhiteSpace(condition.Notes))
                    {
                        text.Span(" — ");
                        text.Span(condition.Notes);
                    }
                });
            }
        });
    }

    private static void ComposeCompensationSection(ColumnDescriptor column, JobProfileCompensationResponse? compensation, string? marketReference)
    {
        if (compensation is null && string.IsNullOrWhiteSpace(marketReference))
        {
            return;
        }

        ComposeSectionTitle(column, "Compensación");

        if (compensation is not null)
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(definition =>
                {
                    definition.RelativeColumn(2);
                    definition.RelativeColumn(5);
                });

                AddKeyValueRow(table, "Clase salarial", compensation.SalaryClassName);
                AddKeyValueRow(table, "Escala", compensation.SalaryScaleCode);
                AddKeyValueRow(table, "Moneda", compensation.CurrencyCode);
                AddKeyValueRow(table, "Salario base", FormatNullableDecimal(compensation.BaseAmount));
                AddKeyValueRow(table, "Mínimo", FormatNullableDecimal(compensation.MinAmount));
                AddKeyValueRow(table, "Máximo", FormatNullableDecimal(compensation.MaxAmount));
                AddKeyValueRow(table, "Vigente desde", FormatNullableDate(compensation.ResolvedEffectiveFromUtc));
                AddKeyValueRow(table, "Vigente hasta", FormatNullableDate(compensation.ResolvedEffectiveToUtc));
            });
        }

        if (!string.IsNullOrWhiteSpace(marketReference))
        {
            column.Item().PaddingTop(4).Text(text =>
            {
                text.Span("Referencia de mercado: ").SemiBold();
                text.Span(marketReference);
            });
        }
    }

    private static void ComposeRelationsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileRelationResponse> relations)
    {
        ComposeSectionTitle(column, "Relaciones");
        if (relations.Count == 0)
        {
            ComposeEmpty(column);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.ConstantColumn(70);
                definition.RelativeColumn(3);
                definition.RelativeColumn(4);
            });

            ComposeTableHeader(table, "Tipo", "Contraparte", "Notas");

            foreach (var relation in relations.OrderBy(r => r.SortOrder))
            {
                table.Cell().Element(BodyCell).Text(FormatRelationType(relation.RelationType));
                table.Cell().Element(BodyCell).Text(relation.Counterpart);
                table.Cell().Element(BodyCell).Text(relation.Notes ?? EmptyValuePlaceholder);
            }
        });
    }

    private static void ComposeDependentPositionsSection(ColumnDescriptor column, IReadOnlyCollection<JobProfileDependentPositionResponse> positions)
    {
        ComposeSectionTitle(column, "Posiciones dependientes");
        if (positions.Count == 0)
        {
            ComposeEmpty(column);
            return;
        }

        column.Item().Table(table =>
        {
            table.ColumnsDefinition(definition =>
            {
                definition.ConstantColumn(90);
                definition.RelativeColumn(4);
                definition.ConstantColumn(60);
            });

            ComposeTableHeader(table, "Código", "Título", "Cantidad");

            foreach (var position in positions)
            {
                table.Cell().Element(BodyCell).Text(position.DependentJobProfileCode);
                table.Cell().Element(BodyCell).Text(position.DependentJobProfileTitle);
                table.Cell().Element(BodyCell).Text(position.Quantity.ToString(CultureInfo.InvariantCulture));
            }
        });
    }

    private static void ComposeSectionTitle(ColumnDescriptor column, string title)
    {
        column.Item().PaddingTop(4).Text(title)
            .FontSize(12).Bold().FontColor(AccentColorHex);
    }

    private static void ComposeEmpty(ColumnDescriptor column)
    {
        column.Item().Text("Sin información registrada.")
            .Italic().FontColor(MutedColorHex);
    }

    private static void ComposeTableHeader(TableDescriptor table, params string[] headers)
    {
        table.Header(header =>
        {
            foreach (var label in headers)
            {
                header.Cell().Element(HeaderCell).Text(label).SemiBold();
            }
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(6);

    private static IContainer BodyCell(IContainer container) =>
        container
            .PaddingVertical(3)
            .PaddingHorizontal(6)
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2);

    private static void AddKeyValueRow(TableDescriptor table, string label, string? value)
    {
        table.Cell().Element(BodyCell).Text(label).SemiBold().FontColor(MutedColorHex);
        table.Cell().Element(BodyCell).Text(string.IsNullOrWhiteSpace(value) ? EmptyValuePlaceholder : value);
    }

    private static string FormatStatus(JobProfileStatus status) => status.ToString();

    private static string FormatFunctionType(JobFunctionType type) => type switch
    {
        JobFunctionType.General => "General",
        JobFunctionType.Specific => "Específica",
        _ => type.ToString()
    };

    private static string FormatRequirementType(JobRequirementType type) => type switch
    {
        JobRequirementType.Education => "Educación",
        JobRequirementType.Experience => "Experiencia",
        JobRequirementType.Knowledge => "Conocimiento",
        JobRequirementType.Certification => "Certificación",
        JobRequirementType.Other => "Otro",
        _ => type.ToString()
    };

    private static string FormatRelationType(JobRelationType type) => type switch
    {
        JobRelationType.Internal => "Interna",
        JobRelationType.External => "Externa",
        _ => type.ToString()
    };

    private static string FormatReportsTo(JobProfileResponse profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ReportsToJobProfileTitle) &&
            string.IsNullOrWhiteSpace(profile.ReportsToJobProfileCode))
        {
            return EmptyValuePlaceholder;
        }

        var title = profile.ReportsToJobProfileTitle ?? string.Empty;
        var code = profile.ReportsToJobProfileCode;
        return string.IsNullOrWhiteSpace(code) ? title : $"{title} ({code})";
    }

    private static string FormatNullableDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : EmptyValuePlaceholder;

    private static string FormatNullableDecimal(decimal? value) =>
        value.HasValue ? value.Value.ToString("N2", CultureInfo.InvariantCulture) : EmptyValuePlaceholder;

    private static string BuildCompetencyEvidence(JobProfileCompetencyResponse competency)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(competency.ExpectedEvidence))
        {
            parts.Add(competency.ExpectedEvidence!);
        }

        if (competency.Conducts.Count > 0)
        {
            var conducts = competency.Conducts
                .OrderBy(c => c.SortOrder)
                .Select(c => $"• {c.Description}");
            parts.Add(string.Join("\n", conducts));
        }

        return parts.Count == 0 ? EmptyValuePlaceholder : string.Join("\n", parts);
    }
}
