using BookShelf.Api.Dtos;
using BookShelf.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BookShelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController(UserManager<AppUser> userManager) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<AdminUserDto>>> GetUsers()
    {
        var users = userManager.Users.OrderBy(user => user.DisplayName).ToList();
        var response = new List<AdminUserDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            response.Add(new AdminUserDto(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                roles.ToArray(),
                user.CreatedAtUtc));
        }

        return Ok(response);
    }

    [HttpPut("users/{id}/role")]
    public async Task<ActionResult> UpdateRole(string id, UpdateUserRoleRequest request)
    {
        if (request.Role is not ("Admin" or "User"))
        {
            return BadRequest("Role must be Admin or User.");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        if (currentRoles.Contains("Admin") && request.Role == "User")
        {
            await userManager.RemoveFromRoleAsync(user, "Admin");
        }
        else if (!currentRoles.Contains("Admin") && request.Role == "Admin")
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }

        if (!currentRoles.Contains("User"))
        {
            await userManager.AddToRoleAsync(user, "User");
        }

        return NoContent();
    }
}
