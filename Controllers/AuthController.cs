using System.Security.Claims;
using System.Text;
using BookShelf.Api.Dtos;
using BookShelf.Api.Models;
using BookShelf.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace BookShelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TokenService tokenService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("providers")]
    [AllowAnonymous]
    public ActionResult<IEnumerable<AuthProviderDto>> GetProviders()
    {
        return Ok(new[]
        {
            new AuthProviderDto(
                "google",
                "Google",
                !string.IsNullOrWhiteSpace(configuration["Authentication:Google:ClientId"])),
            new AuthProviderDto(
                "facebook",
                "Facebook",
                !string.IsNullOrWhiteSpace(configuration["Authentication:Facebook:AppId"]))
        });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        await userManager.AddToRoleAsync(user, "User");
        return Ok(await tokenService.CreateTokenAsync(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized("Invalid email or password.");
        }

        return Ok(await tokenService.CreateTokenAsync(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok((await tokenService.CreateTokenAsync(user)).User);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Ok(new ForgotPasswordResponse(
                "If an account exists for that email, a reset token has been generated."));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return Ok(new ForgotPasswordResponse(
            "Use the reset token on the reset password screen. Replace this with email delivery in production.",
            encodedToken));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest("Invalid reset request.");
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return BadRequest("Reset token is malformed.");
        }

        var result = await userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors.Select(error => error.Description));
        }

        return NoContent();
    }

    [HttpGet("external/{provider}")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl = null)
    {
        var normalizedProvider = provider.ToLowerInvariant();
        if (normalizedProvider is not ("google" or "facebook"))
        {
            return BadRequest("Unsupported external provider.");
        }

        var callbackUrl = Url.ActionLink(
            nameof(ExternalLoginCallback),
            values: new { returnUrl = ResolveExternalReturnUrl(returnUrl) });

        if (callbackUrl is null)
        {
            return Problem("Unable to create the external login callback URL.");
        }

        var frameworkProvider = normalizedProvider == "google" ? "Google" : "Facebook";
        var properties = signInManager.ConfigureExternalAuthenticationProperties(frameworkProvider, callbackUrl);
        return Challenge(properties, frameworkProvider);
    }

    [HttpGet("external/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] string? returnUrl = null, [FromQuery] string? remoteError = null)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            return Redirect(BuildExternalRedirectUrl(returnUrl, error: remoteError));
        }

        var info = await signInManager.GetExternalLoginInfoAsync();
        if (info is null)
        {
            return Redirect(BuildExternalRedirectUrl(returnUrl, error: "Unable to read external login information."));
        }

        var email = info.Principal.FindFirstValue(ClaimTypes.Email)
                    ?? info.Principal.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
        {
            return Redirect(BuildExternalRedirectUrl(returnUrl, error: "The external provider did not return an email address."));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            var displayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email;
            user = new AppUser
            {
                UserName = email,
                Email = email,
                DisplayName = displayName,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                var message = string.Join(", ", createResult.Errors.Select(error => error.Description));
                return Redirect(BuildExternalRedirectUrl(returnUrl, error: message));
            }

            await userManager.AddToRoleAsync(user, "User");
        }

        var addLoginResult = await userManager.AddLoginAsync(user, info);
        if (!addLoginResult.Succeeded && addLoginResult.Errors.All(error => error.Code != "LoginAlreadyAssociated"))
        {
            var message = string.Join(", ", addLoginResult.Errors.Select(error => error.Description));
            return Redirect(BuildExternalRedirectUrl(returnUrl, error: message));
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var authResponse = await tokenService.CreateTokenAsync(user);
        return Redirect(BuildExternalRedirectUrl(returnUrl, authResponse.Token));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await userManager.FindByIdAsync(userId);
    }

    private string ResolveExternalReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            return returnUrl;
        }

        var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var callbackPath = configuration["Frontend:ExternalCallbackPath"] ?? "/auth/external-callback";
        return $"{baseUrl.TrimEnd('/')}{callbackPath}";
    }

    private string BuildExternalRedirectUrl(string? returnUrl, string? token = null, string? error = null)
    {
        var resolvedReturnUrl = ResolveExternalReturnUrl(returnUrl);
        var query = new Dictionary<string, string?>
        {
            ["token"] = token,
            ["error"] = error
        };

        return QueryHelpers.AddQueryString(resolvedReturnUrl, query!);
    }
}
