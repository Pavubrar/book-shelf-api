namespace BookShelf.Api.Dtos;

public record AdminUserDto(
    string Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    DateTime CreatedAtUtc);

public record UpdateUserRoleRequest(string Role);
