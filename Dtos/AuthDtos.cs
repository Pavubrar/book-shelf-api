namespace BookShelf.Api.Dtos;

public record RegisterRequest(string DisplayName, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserDto(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, UserDto User);

public record ForgotPasswordResponse(string Message, string? ResetToken = null);

public record AuthProviderDto(string Name, string DisplayName, bool IsConfigured);
