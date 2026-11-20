using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scheduling.Application.Planning.DayBoard;
using Scheduling.Application.Planning.Schedule;
using Shared.Api.Authorization;
using Shared.Api.Errors;

namespace Scheduling.Api.Controllers;

/// <summary>
/// The studio's opening hours, closed days and planning rules. Times are the studio's local time.
/// </summary>
[ApiController]
[Route("api/schedule")]
[Authorize]
public class ScheduleController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ScheduleDto>> Get(
        CancellationToken cancellationToken)
    {
        return await sender.Send(new GetScheduleQuery(), cancellationToken);
    }

    /// <summary>
    /// Replaces the weekly hours and the rules. An empty list of hours switches planning off.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<ScheduleDto>> Update(
        ScheduleRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new UpdateScheduleCommand(request), cancellationToken);

        return result.Success ? result.Value! : this.ToProblem(result.Error!);
    }

    /// <summary>
    /// One local day: each bay's bookings and free gaps, how busy it is, and the requests waiting for that day.
    /// </summary>
    [HttpGet("day")]
    public async Task<ActionResult<DayBoardDto>> Day(
        [FromQuery] DateOnly date, CancellationToken cancellationToken)
    {
        return await sender.Send(new GetDayBoardQuery(date), cancellationToken);
    }

    [HttpPost("closures")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<ActionResult<ScheduleDto>> CloseDay(
        ClosureRequest request, CancellationToken cancellationToken)
    {
        return await sender.Send(new CloseDayCommand(request), cancellationToken);
    }

    [HttpDelete("closures/{date}")]
    [Authorize(Policy = Policies.OwnerOnly)]
    public async Task<IActionResult> ReopenDay(
        DateOnly date, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ReopenDayCommand(date), cancellationToken);

        return result.Success ? NoContent() : this.ToProblem(result.Error!);
    }
}
