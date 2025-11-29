using AppServerApi.Models;
using AppServerApi.Models.auth;
using Microsoft.EntityFrameworkCore;

namespace AppServerApi.Services;

public interface ITokenService
{
    Task<RefreshToken> CreateRefreshTokenAsync(int userId, int expirationDays);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string token, int userId);
    Task RevokeRefreshTokenAsync(string token);
}

public class TokenService : ITokenService
{
    private readonly AppDbContext _context;
    private readonly IJwtService _jwtService;

    public TokenService(AppDbContext context, IJwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(int userId, int expirationDays)
    {
        var token = _jwtService.GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = token,
            ExpiryDate = DateTime.UtcNow.AddDays(expirationDays),
            IsRevoked = false
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token, int userId)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token && rt.UserId == userId);

        if (refreshToken == null)
            return null;

        if (refreshToken.IsRevoked)
            return null;

        if (refreshToken.ExpiryDate < DateTime.UtcNow)
        {
            refreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();
            return null;
        }

        return refreshToken;
    }

    public async Task RevokeRefreshTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (refreshToken != null)
        {
            refreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync();
        }
    }
}
