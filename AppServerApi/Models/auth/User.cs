using Microsoft.AspNetCore.Identity;
using AppServerApi.Models.auth;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    // Make password property mappable by EF (keep setter private)
    public string Password { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string Language { get; set; } = "en"; // Default language
    
    // Portfolio fields per user
    public double Balance { get; set; } = 10000; // Initial balance for new users
    public string StocksJson { get; set; } = "{}"; // JSON string: { "SYMBOL": quantity }
    
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public User(string username, string password, string email)
    {
        Username = username;
        var hasher = new PasswordHasher<User>();
        Password = hasher.HashPassword(this, password);
        Email = email;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    // Parameterless constructor required by EF Core when it can't use constructor binding
    protected User() { }

    // Verifies the provided password against the stored one
    public bool VerifyPassword(string password)
    {
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(this, this.Password, password);
        if (result == PasswordVerificationResult.Success)
        {
            return true;
        }
        else
        {
            return false;
        }

    }

    public void UpdatePassword(string password)
    {

        var hasher = new PasswordHasher<User>();
        Password = hasher.HashPassword(this, password);
    }
    
}