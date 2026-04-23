using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddScoped<JwtTokenService>();
    builder.Services.AddScoped<PasswordHasher>();
    builder.Services.AddScoped<CurrentUserAccessor>();

    var jwtSecret = builder.Configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not configured");
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SplitBook",
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SplitBook",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    app.MapGet("/health", () => Results.Json(new { status = "ok", version = "0.1.0" }));

    app.UseAuthentication();
    app.UseAuthorization();

    var auth = app.MapGroup("/auth");
    auth.MapRegister();
    auth.MapLogin();

    // Temporary stub for slice 1 auth verification — replaced by real endpoints in slice 2
    var groups = app.MapGroup("/groups").RequireAuthorization();
    groups.MapGet("/", () => Results.Ok(new object[0]));

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
