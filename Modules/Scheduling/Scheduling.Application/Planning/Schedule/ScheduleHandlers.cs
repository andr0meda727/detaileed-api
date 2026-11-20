using Mediator;
using Scheduling.Application.Abstractions;
using Scheduling.Domain.Planning;
using Shared.Kernel.Results;

namespace Scheduling.Application.Planning.Schedule;

// The schedule is one small aggregate per studio, so its four use cases share a file.

public sealed record GetScheduleQuery : IQuery<ScheduleDto>;

public sealed class GetScheduleHandler(ISchedulingDbContext db) : IQueryHandler<GetScheduleQuery, ScheduleDto>
{
    /// <summary>
    /// The studio's schedule, or the defaults (no opening hours, so planning is off) when it never set one.
    /// </summary>
    public async ValueTask<ScheduleDto> Handle(GetScheduleQuery query, CancellationToken cancellationToken)
    {
        var schedule = await db.FindOrCreateScheduleAsync(addIfMissing: false, cancellationToken);
        return await db.ToDtoAsync(schedule, cancellationToken);
    }
}

public sealed record UpdateScheduleCommand(ScheduleRequest Request) : ICommand<Result<ScheduleDto>>;

public sealed class UpdateScheduleHandler(ISchedulingDbContext db)
    : ICommandHandler<UpdateScheduleCommand, Result<ScheduleDto>>
{
    public async ValueTask<Result<ScheduleDto>> Handle(
        UpdateScheduleCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var schedule = await db.FindOrCreateScheduleAsync(addIfMissing: true, cancellationToken);

        var hours = schedule.SetOpeningHours(
            request.OpeningHours.Select(p => new OpeningPeriod(p.Day, p.Opens, p.Closes)).ToList());
        if (!hours.Success) return hours.Error!;

        var rules = schedule.SetRules(
            request.BufferMinutes, request.SlotStepMinutes, request.MinLeadHours,
            request.DurationMultipliers?.ToDomain() ?? schedule.Multipliers);
        if (!rules.Success) return rules.Error!;

        await db.SaveChangesAsync(cancellationToken);
        return await db.ToDtoAsync(schedule, cancellationToken);
    }
}

public sealed record CloseDayCommand(ClosureRequest Request) : ICommand<ScheduleDto>;

public sealed class CloseDayHandler(ISchedulingDbContext db) : ICommandHandler<CloseDayCommand, ScheduleDto>
{
    /// <summary>
    /// Closes the studio for a local day (a holiday). Existing bookings that day stay; staff move them themselves.
    /// </summary>
    public async ValueTask<ScheduleDto> Handle(CloseDayCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var schedule = await db.FindOrCreateScheduleAsync(addIfMissing: true, cancellationToken);

        schedule.Close(request.Date, request.Reason);
        await db.SaveChangesAsync(cancellationToken);

        return await db.ToDtoAsync(schedule, cancellationToken);
    }
}

public sealed record ReopenDayCommand(DateOnly Date) : ICommand<Result>;

public sealed class ReopenDayHandler(ISchedulingDbContext db) : ICommandHandler<ReopenDayCommand, Result>
{
    public static readonly Error NotClosed = Error.NotFound("CLOSURE_NOT_FOUND", "Ten dzień nie jest oznaczony jako zamknięty.");

    public async ValueTask<Result> Handle(ReopenDayCommand command, CancellationToken cancellationToken)
    {
        var date = command.Date;
        var schedule = await db.FindOrCreateScheduleAsync(addIfMissing: false, cancellationToken);
        if (!schedule.Reopen(date)) return NotClosed;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}
