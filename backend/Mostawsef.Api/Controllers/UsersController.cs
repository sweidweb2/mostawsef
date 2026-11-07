using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Mostawsef.Api.Extensions;
using Mostawsef.Application.Modules.Users;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Users;

namespace Mostawsef.Api.Controllers;

/// <summary>User management. Failures come back as problem responses with an ErrorCodes "code".</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthPolicies.SuperOnly)]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserListItemDto>> List([FromQuery] PageQuery query, CancellationToken cancellationToken) =>
        users.GetPageAsync(query, cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<UserDetailsDto> Get(Guid id, CancellationToken cancellationToken) => users.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<UserDetailsDto> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        users.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await users.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await users.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
