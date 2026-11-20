using System.Collections.Concurrent;
using System.Reflection;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Shared.Application.Messaging.Behaviors;

/// <summary>
/// Runs the registered FluentValidation validators before a command or query reaches its handler.
/// It validates the message itself and each of its top-level property values (the request bodies and query objects
/// it wraps), so a validator for <c>ClientRequest</c> also checks <c>UpdateClientCommand(Id, ClientRequest)</c>, and
/// error keys stay unprefixed (<c>Name</c>, <c>Prices[0].Price</c>). A failure throws <see cref="ValidationException"/>,
/// which the host turns into a 400 ValidationProblemDetails.
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse>(IServiceProvider services)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var target in ValidationTargets.Of(message))
        {
            foreach (var validator in ValidatorsFor(target.GetType()))
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(target), cancellationToken);
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count > 0) throw new ValidationException(failures);

        return await next(message, cancellationToken);
    }

    private IEnumerable<IValidator> ValidatorsFor(Type type)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(typeof(IValidator<>).MakeGenericType(type));
        return services.GetService(enumerableType) as IEnumerable<IValidator> ?? [];
    }
}

internal static class ValidationTargets
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    /// <summary>The message, then the values of its properties that could have a validator of their own.</summary>
    public static IEnumerable<object> Of(object message)
    {
        yield return message;

        foreach (var property in Properties.GetOrAdd(message.GetType(), ValidatableProperties))
        {
            if (property.GetValue(message) is { } value) yield return value;
        }
    }

    private static PropertyInfo[] ValidatableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0 && IsComplex(p.PropertyType))
            .ToArray();

    // Ids, flags, dates and strings are checked by the validator of whatever contains them
    private static bool IsComplex(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return !(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal)
                 || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
                 || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan));
    }
}
