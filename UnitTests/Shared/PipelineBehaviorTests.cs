using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Application.Messaging.Behaviors;
using Shared.Kernel.Results;

namespace Unit.Shared;

[TestFixture]
public class PipelineBehaviorTests
{
    public sealed record Body(string Name);

    public sealed record UpdateThingCommand(Guid Id, Body Body) : ICommand<Result>;

    public sealed record FindThingsQuery(int Limit) : IQuery<Result>;

    private sealed class BodyValidator : AbstractValidator<Body>
    {
        public BodyValidator() => RuleFor(b => b.Name).NotEmpty().WithMessage("Name is required.");
    }

    private sealed class FindThingsQueryValidator : AbstractValidator<FindThingsQuery>
    {
        public FindThingsQueryValidator() => RuleFor(q => q.Limit).InclusiveBetween(1, 100);
    }

    private sealed class AsyncBodyValidator : AbstractValidator<Body>
    {
        public AsyncBodyValidator() =>
            RuleFor(b => b.Name).MustAsync(async (name, ct) =>
            {
                await Task.Yield();
                return name != "taken";
            }).WithMessage("Name is taken.");
    }

    private static ValidationBehavior<TMessage, Result> Validation<TMessage>(Action<IServiceCollection> register)
        where TMessage : IMessage
    {
        var services = new ServiceCollection();
        register(services);
        return new ValidationBehavior<TMessage, Result>(services.BuildServiceProvider());
    }

    private static MessageHandlerDelegate<TMessage, Result> Handler<TMessage>(Action onCall) where TMessage : IMessage => (_, _) =>
    {
        onCall();
        return ValueTask.FromResult(Result.Ok());
    };

    [Test]
    public void Validation_ChecksTheBodyAMessageWraps_WithUnprefixedKeys()
    {
        var behavior = Validation<UpdateThingCommand>(s => s.AddScoped<IValidator<Body>, BodyValidator>());
        var called = false;

        var error = Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(new UpdateThingCommand(Guid.NewGuid(), new Body("")),
                Handler<UpdateThingCommand>(() => called = true), CancellationToken.None));

        Assert.That(called, Is.False);
        Assert.That(error!.Errors.Select(e => e.PropertyName), Is.EqualTo(new[] { "Name" }));
    }

    [Test]
    public void Validation_ChecksTheMessageItself()
    {
        var behavior = Validation<FindThingsQuery>(s => s.AddScoped<IValidator<FindThingsQuery>, FindThingsQueryValidator>());

        var error = Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(new FindThingsQuery(0), Handler<FindThingsQuery>(() => { }), CancellationToken.None));

        Assert.That(error!.Errors.Select(e => e.PropertyName), Is.EqualTo(new[] { "Limit" }));
    }

    [Test]
    public void Validation_RunsEveryValidator_IncludingAsyncOnes()
    {
        var behavior = Validation<UpdateThingCommand>(s => s
            .AddScoped<IValidator<Body>, BodyValidator>()
            .AddScoped<IValidator<Body>, AsyncBodyValidator>());

        var error = Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(new UpdateThingCommand(Guid.NewGuid(), new Body("taken")),
                Handler<UpdateThingCommand>(() => { }), CancellationToken.None));

        Assert.That(error!.Errors.Select(e => e.ErrorMessage), Is.EqualTo(new[] { "Name is taken." }));
    }

    [Test]
    public async Task Validation_CallsTheHandler_WhenValid()
    {
        var behavior = Validation<UpdateThingCommand>(s => s.AddScoped<IValidator<Body>, BodyValidator>());
        var called = false;

        var result = await behavior.Handle(new UpdateThingCommand(Guid.NewGuid(), new Body("Anna")),
            Handler<UpdateThingCommand>(() => called = true), CancellationToken.None);

        Assert.That(called, Is.True);
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task Validation_PassesThrough_WhenNothingIsRegistered()
    {
        var behavior = Validation<UpdateThingCommand>(_ => { });
        var called = false;

        await behavior.Handle(new UpdateThingCommand(Guid.NewGuid(), new Body("")),
            Handler<UpdateThingCommand>(() => called = true), CancellationToken.None);

        Assert.That(called, Is.True);
    }

    [Test]
    public async Task Logging_ReturnsTheHandlersResponse_SuccessOrFailure()
    {
        var behavior = new LoggingBehavior<FindThingsQuery, Result>(NullLogger<LoggingBehavior<FindThingsQuery, Result>>.Instance);
        var failure = Error.NotFound("THING_NOT_FOUND", "Nie ma.");

        var ok = await behavior.Handle(new FindThingsQuery(1), Handler<FindThingsQuery>(() => { }), CancellationToken.None);
        var failed = await behavior.Handle(new FindThingsQuery(1),
            (_, _) => ValueTask.FromResult<Result>(failure), CancellationToken.None);

        Assert.That(ok.Success, Is.True);
        Assert.That(failed.ErrorCode, Is.EqualTo("THING_NOT_FOUND"));
    }

    [Test]
    public void Logging_LetsExceptionsThrough()
    {
        var behavior = new LoggingBehavior<FindThingsQuery, Result>(NullLogger<LoggingBehavior<FindThingsQuery, Result>>.Instance);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await behavior.Handle(new FindThingsQuery(1),
            (_, _) => throw new InvalidOperationException(), CancellationToken.None));
    }
}
