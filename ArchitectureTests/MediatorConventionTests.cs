using System.Reflection;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace ArchitectureTests;

/// <summary>
/// Every use case is a command or query sent through the mediator: handlers implement the mediator's handler
/// interfaces, messages are named after what they are, and controllers depend on ISender, never on a handler.
/// </summary>
[TestFixture]
public class MediatorConventionTests
{
    // Per-request infrastructure used by the tenant middleware, deliberately not a use case
    private static readonly string[] PlainHandlers = ["ResolveCallerHandler"];

    private static IEnumerable<ModuleBoundaryTests.Module> LayeredModules => ModuleBoundaryTests.LayeredModules;

    private static IEnumerable<ModuleBoundaryTests.Module> Modules => ModuleBoundaryTests.Modules;

    [TestCaseSource(nameof(LayeredModules))]
    public void Handlers_AreCommandOrQueryHandlers(ModuleBoundaryTests.Module module)
    {
        var offending = module.Application!.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Name.EndsWith("Handler"))
            .Where(t => !PlainHandlers.Contains(t.Name))
            .Where(t => !Implements(t, typeof(ICommandHandler<,>)) && !Implements(t, typeof(IQueryHandler<,>)))
            .Select(t => t.FullName)
            .ToList();

        Assert.That(offending, Is.Empty, $"{module} handlers must implement ICommandHandler<,> or IQueryHandler<,>");
    }

    [TestCaseSource(nameof(LayeredModules))]
    public void Messages_AreNamedAfterTheirKind(ModuleBoundaryTests.Module module)
    {
        var types = module.Application!.GetTypes();

        var commands = types.Where(t => Implements(t, typeof(ICommand<>)) && !t.Name.EndsWith("Command"));
        var queries = types.Where(t => Implements(t, typeof(IQuery<>)) && !t.Name.EndsWith("Query"));

        Assert.That(commands.Concat(queries).Select(t => t.FullName), Is.Empty,
            $"{module} commands must end in 'Command' and queries in 'Query'");
    }

    [TestCaseSource(nameof(Modules))]
    public void Controllers_SendMessagesInsteadOfCallingHandlers(ModuleBoundaryTests.Module module)
    {
        var offending = module.Api.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .SelectMany(m => m.GetParameters()))
                .Where(p => p.ParameterType.Name.EndsWith("Handler"))
                .Select(p => $"{t.Name}: {p.ParameterType.Name} {p.Name}"))
            .ToList();

        Assert.That(offending, Is.Empty, $"{module} controllers must use ISender, not handlers");
    }

    private static bool Implements(Type type, Type openGeneric) =>
        type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openGeneric);
}
