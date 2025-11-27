using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

using GameServerApi.Models;

namespace GameServerApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {

        private readonly UserContext _context;
        public UserController(UserContext ctx)
        {
            _context = ctx;
        }

        // GET: api/<UserController>/All
        
        [HttpGet("All")]
        public async Task<ActionResult<List<UserPublic>>> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new UserPublic(u.Id, u.Username, u.Email))
                .ToListAsync();

            return Ok(users);
        }
        

        // GET api/<UserController>/{id}
        
        [HttpGet("{id}")]
        public async Task<ActionResult<UserPublic>> GetUserById(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserPublic(u.Id, u.Username, u.Email))
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            return Ok(user);
        }
        

        // GET api/<UserController>/Search/{name}
        
        [HttpGet("Search/{name}")]
        public async Task<ActionResult<IEnumerable<UserPublic>>> SearchUsers(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Ok(Array.Empty<UserPublic>());

            var lowerName = name.ToLower();

            var users = await _context.Users
                .Where(u => u.Username.ToLower().Contains(lowerName) || u.Username.ToLower() == lowerName)
                .ToListAsync();

            var result = users.Select(u => new UserPublic(u.Id, u.Username, u.Email));
            return Ok(result);
        }

        // POST api/<UserController>
        
        [HttpPost("Register")]
        public async Task<ActionResult<UserPublic>> RegisterUser([FromBody] UserRegister newUser)
        {
            // Check if username already exists
            bool exists = await _context.Users.AnyAsync(u => u.Email == newUser.Email);
            if (exists)
            {
                return BadRequest(new ErrorResponse(
                    "Username already exists",
                    "USERNAME_EXISTS"
                ));
            }

            try
            {

                // Create user with password (constructor handles hashing)
                User user = new User(newUser.Username, newUser.Password, newUser.Email);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Return 201 Created
                return CreatedAtAction(nameof(GetUserById),
                    new { id = user.Id },
                    new UserPublic(user.Id, user.Username, user.Email));
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
        


        // POST api/<UserController>
        
        [HttpPost("Login")]
        public async Task<ActionResult<UserPublic>> Login([FromBody] UserPass userPass)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == userPass.Email);

            // non trouvé ou mot de passe incorrect
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            if (!user.VerifyPassword(userPass.Password))
            {
                return Unauthorized(new ErrorResponse("invalid password", "INVALID_PASSWORD"));
            }


            // si tout est bon, on retourne les infos publiques
            var userPublic = new UserPublic(user.Id, user.Username, user.Email);
            return Ok(userPublic);
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
