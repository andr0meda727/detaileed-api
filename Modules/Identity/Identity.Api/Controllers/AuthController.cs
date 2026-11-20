using Identity.Application;
using Identity.Application.Studios.RegisterOwner;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Shared.Api.Errors;

namespace Identity.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterOwnerRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RegisterOwnerCommand(request), cancellationToken);

        return result.Success
            ? Ok(new { message = "Konto zostało utworzone. Możesz się zalogować." })
            : this.ToProblem(result.Error!);
    }
}
