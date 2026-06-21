using System.Reflection;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.Provisioning;
using CLARIHR.Application.Features.Reports.Common;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddScoped<ICommandDispatcher, RequestDispatcher>();
        services.AddScoped<IQueryDispatcher, RequestDispatcher>();
        services.AddScoped<ICompanyProvisioningService, CompanyProvisioningService>();
        services.AddScoped<ICompanyUserProvisioningService, CompanyUserProvisioningService>();
        services.AddScoped<IPersonnelFileFinalizationService, PersonnelFileFinalizationService>();
        services.AddScoped<IReportExportResourceAuthorizer, ReportExportResourceAuthorizer>();

        RegisterHandlers(services, assembly, typeof(ICommandHandler<,>));
        RegisterHandlers(services, assembly, typeof(IQueryHandler<,>));

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly, Type openGenericType)
    {
        var implementations = assembly
            .DefinedTypes
            .Where(static type => type is { IsAbstract: false, IsInterface: false });

        foreach (var implementation in implementations)
        {
            foreach (var serviceType in implementation.ImplementedInterfaces
                         .Where(interfaceType =>
                             interfaceType.IsGenericType &&
                             interfaceType.GetGenericTypeDefinition() == openGenericType))
            {
                services.AddScoped(serviceType, implementation.AsType());
            }
        }
    }
}
