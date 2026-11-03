using System.Text;
using System.Text.Json.Serialization;
using DeviceManagerMVC.Api.ErrorHandling;
using DeviceManagerMVC.Application;
using DeviceManagerMVC.Infrastructure;
using DeviceManagerMVC.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        // "InUse" instead of 2 in JSON; numbers are still accepted on input.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

// Every error response is application/problem+json; known exceptions get 400/401/404/409, the rest a bare 500.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep JWT claim names as issued ("sub", not ClaimTypes.NameIdentifier).
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents { OnChallenge = JwtBearerProblemDetails.WriteChallengeAsync };
    });

// Validates with the same JwtOptions the issuer signs with (registered by AddInfrastructure).
builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtOptions>((options, jwt) => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        // Default is 5 minutes, a third of the token's 15-minute life.
        ClockSkew = TimeSpan.FromSeconds(30)
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the entry point to WebApplicationFactory<Program> in the integration tests.
public partial class Program;
