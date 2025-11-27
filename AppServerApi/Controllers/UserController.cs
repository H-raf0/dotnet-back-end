using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

using GameServerApi.Models;
using GameServerApi.Models.auth;
using GameServerApi.Services;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {

        private readonly UserContext _context;
        private readonly IJwtService _jwtService;
        private readonly ITokenService _tokenService;
        
        public UserController(UserContext ctx, IJwtService jwtService, ITokenService tokenService)
        {
            _context = ctx;
            _jwtService = jwtService;
            _tokenService = tokenService;
        }

        // GET: api/<UserController>/All
        
        [HttpGet("All")]
        public async Task<ActionResult<List<UserPublic>>> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new UserPublic(u.Id.ToString(), u.Username, u.Email, u.CreatedAt.ToString("o"), u.UpdatedAt.ToString("o"), u.Language))
                .ToListAsync();

            return Ok(users);
        }
        

        // GET api/<UserController>/{id}
        
        [HttpGet("{id}")]
        public async Task<ActionResult<UserPublic>> GetUserById(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserPublic(u.Id.ToString(), u.Username, u.Email, u.CreatedAt.ToString("o"), u.UpdatedAt.ToString("o"), u.Language))
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            return Ok(user);
        }
        

        // GET api/<UserController>/Search/{username}
        
        [HttpGet("Search/{username}")]
        public async Task<ActionResult<IEnumerable<UserPublic>>> SearchUsers(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return Ok(Array.Empty<UserPublic>());

            var lowerUsername = username.ToLower();

            var users = await _context.Users
                .Where(u => u.Username.ToLower().Contains(lowerUsername) || u.Username.ToLower() == lowerUsername)
                .ToListAsync();

            var result = users.Select(u => new UserPublic(u.Id.ToString(), u.Username, u.Email, u.CreatedAt.ToString("o"), u.UpdatedAt.ToString("o"), u.Language));
            return Ok(result);
        }

        // POST api/<UserController>/Register
        
        [HttpPost("Register")]
        public async Task<ActionResult<RegisterResponse>> RegisterUser([FromBody] UserRegister newUser)
        {
            if(newUser.Terms == false)
            {
                return BadRequest(new ErrorResponse(
                    "Terms must be accepted",
                    "TERMS_NOT_ACCEPTED"
                ));
            }
            
            // Normalize email to lowercase for consistency
            string normalizedEmail = newUser.Email.ToLower();
            
            // Check if email already exists
            bool exists = await _context.Users.AnyAsync(u => u.Email == normalizedEmail);
            if (exists)
            {
                return BadRequest(new ErrorResponse(
                    "Email already exists",
                    "EMAIL_EXISTS"
                ));
            }

            try
            {

                // Create user with password (constructor handles hashing)
                User user = new User(newUser.Username, newUser.Password, normalizedEmail);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Generate tokens
                var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.Username);
                var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, 7);

                // Return 201 Created with tokens and user data
                var userPublic = new UserPublic(user.Id.ToString(), user.Username, user.Email, user.CreatedAt.ToString("o"), user.UpdatedAt.ToString("o"), user.Language);
                var response = new RegisterResponse(accessToken, refreshToken.Token, userPublic);
                
                return CreatedAtAction(nameof(GetUserById),
                    new { id = user.Id },
                    response);
            }
            catch
            {
                // Any unexpected failure
                return BadRequest(new ErrorResponse(
                    "Registration failed",
                    "REGISTRATION_FAILED"
                ));
            }
        }
        


        // POST api/<UserController>/Login
        
        [HttpPost("Login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] UserLogin userLogin)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == userLogin.Email);

            // non trouvé ou mot de passe incorrect
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            if (!user.VerifyPassword(userLogin.Password))
            {
                return Unauthorized(new ErrorResponse("invalid password", "INVALID_PASSWORD"));
            }

            // Generate tokens
            var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.Username);
            var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, 7);

            // si tout est bon, on retourne les infos publiques avec les tokens
            var userPublic = new UserPublic(user.Id.ToString(), user.Username, user.Email, user.CreatedAt.ToString("o"), user.UpdatedAt.ToString("o"), user.Language);
            var response = new LoginResponse(accessToken, refreshToken.Token, userPublic);
            return Ok(response);
        }
        
        // POST api/<UserController>/RefreshToken
        
        [HttpPost("RefreshToken")]
        public async Task<ActionResult<RefreshTokenResponse>> RefreshTokenEndpoint([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var principal = _jwtService.GetPrincipalFromExpiredToken(request.RefreshToken);
                if (principal == null)
                {
                    return Unauthorized(new ErrorResponse("Invalid token", "INVALID_TOKEN"));
                }

                var userIdClaim = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                {
                    return Unauthorized(new ErrorResponse("Invalid token claims", "INVALID_TOKEN_CLAIMS"));
                }

                var validToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken, userId);
                if (validToken == null)
                {
                    return Unauthorized(new ErrorResponse("Refresh token expired or revoked", "INVALID_REFRESH_TOKEN"));
                }

                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
                }

                var newAccessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.Username);
                var newRefreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, 7);

                // Revoke the old refresh token
                await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);

                var response = new RefreshTokenResponse(newAccessToken, newRefreshToken.Token);
                return Ok(response);
            }
            catch
            {
                return Unauthorized(new ErrorResponse("Token refresh failed", "TOKEN_REFRESH_FAILED"));
            }
        }
        




        // PUT api/<UserController>/5
        /*
        [HttpPut("{id}")]
        public async Task<ActionResult<User>> UpdateUser(int id, [FromBody] UserUpdate userUpdate)
        {
            // Check if the user exists
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            // Update username
            if (!string.IsNullOrEmpty(userUpdate.Username))
            {
                user.Username = userUpdate.Username;
            }

            // Update role
            if (userUpdate.Role != null)
            {
                // Mise à jour du rôle
                user.Role = userUpdate.Role.Value;
            }

            // Update password
            if (!string.IsNullOrEmpty(userUpdate.Password))
            {
                user.UpdatePassword(userUpdate.Password);
            }

            // Save changes
            await _context.SaveChangesAsync();

            return Ok(user);

        }
        */



        // DELETE api/<UserController>/{id}
        /*
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            // Rechercher l'utilisateur par son ID
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }


            // Supprimer l'utilisateur du contexte
            _context.Users.Remove(user);

            // Sauvegarder les modifications dans la base de données
            await _context.SaveChangesAsync();


            return Ok(true);

        }
        */
        
    }
}
