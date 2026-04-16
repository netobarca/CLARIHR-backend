using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application.UnitTests;

public sealed class RequestDispatcherTests
{
    [Fact]
    public async Task SendAsync_ReturnsValidationFailure_WhenValidatorFails()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandDispatcher, RequestDispatcher>();
        services.AddScoped<ICommandHandler<SampleCommand, string>, SampleCommandHandler>();
        services.AddScoped<IValidator<SampleCommand>, SampleCommandValidator>();

        await using var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new SampleCommand(string.Empty));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("common.validation", result.Error.Code);
        Assert.Contains("name", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task SendAsync_InvokesHandler_WhenValidationPasses()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandDispatcher, RequestDispatcher>();
        services.AddScoped<ICommandHandler<SampleCommand, string>, SampleCommandHandler>();
        services.AddScoped<IValidator<SampleCommand>, SampleCommandValidator>();

        await using var serviceProvider = services.BuildServiceProvider();
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new SampleCommand("CLARIHR"));

        Assert.True(result.IsSuccess);
        Assert.Equal("processed:CLARIHR", result.Value);
    }

    private sealed record SampleCommand(string Name) : ICommand<string>;

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty();
        }
    }

    private sealed class SampleCommandHandler : ICommandHandler<SampleCommand, string>
    {
        public Task<Result<string>> Handle(SampleCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(Result<string>.Success($"processed:{command.Name}"));
    }
}
