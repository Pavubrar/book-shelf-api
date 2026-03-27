using System.Text;
using BookShelf.Api.Data;
using BookShelf.Api.Models;
using BookShelf.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRootPath);
builder.WebHost.UseWebRoot(webRootPath);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager<SignInManager<AppUser>>()
    .AddDefaultTokenProviders();

var jwtKey = builder.Configuration["Jwt:Key"]
             ?? throw new InvalidOperationException("JWT signing key is missing.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    })
    .AddCookie(IdentityConstants.ExternalScheme);

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication().AddGoogle("Google", options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    builder.Services.AddAuthentication().AddFacebook("Facebook", options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
    });
}

static IEnumerable<string> SplitOrigins(params string?[] values) =>
    values
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .SelectMany(value => value!.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var configuredOriginsCsv = builder.Configuration["Cors:AllowedOriginsCsv"];
var configuredOriginsValue = builder.Configuration["Cors:AllowedOrigins"];
var frontendBaseUrl = builder.Configuration["Frontend:BaseUrl"];
var allowedOrigins = configuredOrigins
    .Concat(SplitOrigins(configuredOriginsCsv, configuredOriginsValue))
    .Concat(SplitOrigins(frontendBaseUrl))
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin!.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<FileStorageService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var healthEndpoint = async Task<IResult> (ApplicationDbContext dbContext) =>
{
    var databaseOnline = await dbContext.Database.CanConnectAsync();
    var payload = new HealthResponse(
        databaseOnline ? "healthy" : "degraded",
        "BookShelf.Api",
        DateTime.UtcNow,
        new HealthChecks(
            new HealthCheckResult("healthy"),
            new HealthCheckResult(databaseOnline ? "healthy" : "unreachable")
        )
    );

    return Results.Json(
        payload,
        statusCode: databaseOnline ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable
    );
};

app.MapGet("/health", healthEndpoint);
app.MapGet("/api/health", healthEndpoint);

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await SeedData.InitializeAsync(services, builder.Configuration);
}

app.Run();

internal sealed record HealthResponse(
    string Status,
    string Service,
    DateTime TimestampUtc,
    HealthChecks Checks
);

internal sealed record HealthChecks(
    HealthCheckResult Api,
    HealthCheckResult Database
);

internal sealed record HealthCheckResult(string Status);
