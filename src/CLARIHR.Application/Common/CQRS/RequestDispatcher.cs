using CLARIHR.Application.Common.Errors;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace CLARIHR.Application.Common.CQRS;

public sealed class RequestDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher, IQueryDispatcher
{
    public Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default) =>
        SendCommandAsync(command, cancellationToken);

    public Task<Result<TResponse>> SendAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default) =>
        SendQueryAsync(query, cancellationToken);

    private async Task<Result<TResponse>> SendCommandAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        var requestType = command.GetType();
        var validationResult = await ValidateAsync<TResponse>(requestType, command, cancellationToken);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var handlerType = typeof(ICommandHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        return await InvokeHandlerAsync<TResponse>(handler, handlerType, command, cancellationToken);
    }

    private async Task<Result<TResponse>> SendQueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        var requestType = query.GetType();
        var validationResult = await ValidateAsync<TResponse>(requestType, query, cancellationToken);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var handlerType = typeof(IQueryHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = serviceProvider.GetRequiredService(handlerType);

        return await InvokeHandlerAsync<TResponse>(handler, handlerType, query, cancellationToken);
    }

    private async Task<Result<TResponse>?> ValidateAsync<TResponse>(
        Type requestType,
        object request,
        CancellationToken cancellationToken)
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(requestType);
        var validators = serviceProvider.GetServices(validatorType).Cast<IValidator>().ToArray();
        if (validators.Length == 0)
        {
            return null;
        }

        var context = new ValidationContext<object>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(static result => result.Errors)
            .Where(static failure => failure is not null)
            .ToArray();

        if (failures.Length == 0)
        {
            return null;
        }

        return Result<TResponse>.Failure(ErrorCatalog.Validation(ToDictionary(failures)));
    }

    private static async Task<Result<TResponse>> InvokeHandlerAsync<TResponse>(
        object handler,
        Type handlerType,
        object request,
        CancellationToken cancellationToken)
    {
        var handleMethod = handlerType.GetMethod(nameof(ICommandHandler<ICommand<TResponse>, TResponse>.Handle), BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Handle method not found on {handlerType.Name}.");

        var handlerTask = (Task<Result<TResponse>>?)handleMethod.Invoke(handler, [request, cancellationToken])
            ?? throw new InvalidOperationException($"Unable to invoke {handlerType.Name}.Handle.");

        return await handlerTask;
    }

    private static IReadOnlyDictionary<string, string[]> ToDictionary(IEnumerable<ValidationFailure> failures) =>
        failures
            .GroupBy(
                static failure => failure.PropertyName,
                static failure => failure.ErrorMessage)
            .ToDictionary(
                static group => group.Key,
                static group => group.Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
}
