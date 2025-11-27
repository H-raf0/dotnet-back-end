
namespace GameServerApi;

using Scalar.AspNetCore;
using GameServerApi.Models;
using GameServerApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore.Sqlite;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.AddDbContext<UserContext>();

        builder.Services.AddScoped<PasswordHasher<User>>();
        builder.Services.AddScoped<IJwtService, JwtService>();
        builder.Services.AddScoped<ITokenService, TokenService>();

        // Configure JWT
        var accessTokenSecret = builder.Configuration["Jwt:AccessTokenSecret"];
        var refreshTokenSecret = builder.Configuration["Jwt:RefreshTokenSecret"];
        
        if (string.IsNullOrEmpty(accessTokenSecret) || string.IsNullOrEmpty(refreshTokenSecret))
        {
            throw new InvalidOperationException(
                $"JWT secrets are not configured for environment: {builder.Environment.EnvironmentName}\n" +
                "Please set Jwt:AccessTokenSecret and Jwt:RefreshTokenSecret in:\n" +
                "- appsettings.json (development defaults)\n" + 
                "- appsettings.Production.json (production)\n" +
                "- Environment variables (highest priority)");
        }

        

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessTokenSecret)),
                    ValidateIssuer = true,
                    ValidIssuer = "angulardotnetapp",
                    ValidateAudience = true,
                    ValidAudience = "angulardotnetapp",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            });

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();

            // Log the configuration source for debugging
            app.Logger.LogInformation("Running in Development environment");
            app.Logger.LogInformation("JWT Secret Source: {Source}", 
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("Jwt__AccessTokenSecret")) 
                    ? "appsettings.Development.json" 
                    : "Environment Variable");
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCors("AllowAll");
        app.MapControllers();
        app.Run();
    }
}
