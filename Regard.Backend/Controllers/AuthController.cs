using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API;
using Regard.Common.API.Response;
using RegardBackend.Common.API.Request;
using RegardBackend.Model;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace RegardBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IConfiguration configuration;
        private readonly ApiResponseFactory responseFactory;

        public AuthController(UserManager<UserAccount> userManager, RoleManager<IdentityRole> roleManager,
            IConfiguration configuration, ApiResponseFactory responseFactory)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.configuration = configuration;
            this.responseFactory = responseFactory;
        }

        private async Task<JwtSecurityToken> GenerateAuthToken(UserAccount user, bool rememberMe = false)
        {
            var userRoles = await userManager.GetRolesAsync(user);

            var authClaims = new List<Claim> {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

            foreach (var role in userRoles)
                authClaims.Add(new Claim(ClaimTypes.Role, role));

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]));

            var token = new JwtSecurityToken(
                issuer: null,
                audience: null,
                expires: rememberMe ? DateTime.Now.AddDays(60) : DateTime.Now.AddDays(1),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256));
            return token;
        }

        [HttpPost]
        [Route("login")]
        public async Task<IActionResult> Login([FromBody] UserLogin login)
        {
            var user = await userManager.FindByNameAsync(login.Username);

            if (user != null && await userManager.CheckPasswordAsync(user, login.Password))
            {
                JwtSecurityToken token = await GenerateAuthToken(user, login.RememberMe);

                return Ok(responseFactory.Success(new AuthResult()
                {
                    Token = new JwtSecurityTokenHandler().WriteToken(token),
                    ValidTo = token.ValidTo
                }));
            }

            return Unauthorized(responseFactory.Error("Invalid username or password."));
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] UserRegister register)
        {
            var userExists = await userManager.FindByNameAsync(register.Username);
            if (userExists != null)
                return BadRequest(responseFactory.Error("This username is taken!"));

            // Validate password
            if (string.IsNullOrWhiteSpace(register.Password1))
                return BadRequest(responseFactory.Error("Password is required!"));

            if (string.IsNullOrWhiteSpace(register.Password2))
                return BadRequest(responseFactory.Error("Password verification is required!"));

            if (!string.Equals(register.Password1, register.Password2))
                return BadRequest(responseFactory.Error("Passwords do not match!"));

            var user = new UserAccount()
            {
                UserName = register.Username,
                FirstName = register.FirstName,
                LastName = register.LastName,
                Email = register.Email,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await userManager.CreateAsync(user, register.Password1);
            if (!result.Succeeded)
                return BadRequest(responseFactory.Error("User creation failed", result.ToString()));

            // Assign user role
            if (!await roleManager.RoleExistsAsync(UserRoles.User))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.User));

            result = await userManager.AddToRoleAsync(user, UserRoles.User);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, responseFactory.Error("Failed to assign user role!", result.ToString()));

            // Login
            JwtSecurityToken token = await GenerateAuthToken(user);

            return Ok(responseFactory.Success(new AuthResult()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ValidTo = token.ValidTo
            }));
        }

        [Authorize]
        [HttpPost]
        [Route("promote")]
        public async Task<IActionResult> Promote([FromBody] UserPromote promote)
        {
            // This method can be executed by any registered user if there is NO admin account. 
            // This should only be used during setup.
            // Otherwise, only admins can promote other users to admin
            if (!User.IsInRole(UserRoles.Admin))
            {
                var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
                if (admins.Count != 0)
                    return Unauthorized(responseFactory.Error("Only admins can promote users"));
            }

            var user = await userManager.FindByNameAsync(promote.Username);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist!"));

            if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));

            var result = await userManager.AddToRoleAsync(user, UserRoles.Admin);
            if (!result.Succeeded)
                return StatusCode(StatusCodes.Status500InternalServerError, responseFactory.Error("Failed to assign admin role!", result.ToString()));

            // Generate new token with updated credentials
            JwtSecurityToken token = await GenerateAuthToken(user);

            return Ok(responseFactory.Success(new AuthResult() 
            { 
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ValidTo = token.ValidTo
            }));
        }

        [Authorize]
        [HttpGet]
        [Route("me")]
        public async Task<IActionResult> Me()
        {
            var user = await userManager.GetUserAsync(User);

            if (user == null)
                return StatusCode(StatusCodes.Status500InternalServerError, responseFactory.Error("Failed to retrieve user details!"));

            return Ok(responseFactory.Success(new UserDetails()
            {
                Username = user.UserName,
                Email = user.Email,
                IsAdmin = User.IsInRole(UserRoles.Admin),
                FirstName = user.FirstName,
                LastName = user.LastName
            }));
        }
    }
}
