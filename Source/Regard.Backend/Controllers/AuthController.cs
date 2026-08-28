using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Regard.Backend.Configuration;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Auth;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly JwtSecretProvider jwtSecret;
        private readonly IOptionManager optionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly IEmailService emailService;
        private readonly ILogger<AuthController> logger;

        public AuthController(UserManager<UserAccount> userManager, RoleManager<IdentityRole> roleManager,
            JwtSecretProvider jwtSecret, IOptionManager optionManager, ApiResponseFactory responseFactory,
            IEmailService emailService, ILogger<AuthController> logger)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.jwtSecret = jwtSecret;
            this.optionManager = optionManager;
            this.responseFactory = responseFactory;
            this.emailService = emailService;
            this.logger = logger;
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

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret.Value));

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
        public async Task<IActionResult> Login([FromBody] UserLoginRequest login)
        {
            var user = await userManager.FindByNameAsync(login.Username);

            if (user != null && await userManager.CheckPasswordAsync(user, login.Password))
            {
                // Manual login (no SignInManager) doesn't consult lockout, so enforce it here —
                // this is what makes an admin "disable user" actually block sign-in.
                if (await userManager.IsLockedOutAsync(user))
                    return Unauthorized(responseFactory.Error("This account has been disabled."));

                JwtSecurityToken token = await GenerateAuthToken(user, login.RememberMe);

                return Ok(responseFactory.Success(new AuthResponse()
                {
                    Token = new JwtSecurityTokenHandler().WriteToken(token),
                    ValidTo = token.ValidTo
                }));
            }

            return Unauthorized(responseFactory.Error("Invalid username or password."));
        }

        [HttpPost]
        [Route("register")]
        public async Task<IActionResult> Register([FromBody] UserRegisterRequest register)
        {
            var userExists = await userManager.FindByNameAsync(register.Username);
            if (userExists != null)
                return BadRequest(responseFactory.Error("This username is taken!"));

            // Registration gate: honor the server option, but always allow bootstrapping the first
            // admin, so a locked-down instance with no admin yet can still be set up.
            bool noAdminYet = (await userManager.GetUsersInRoleAsync(UserRoles.Admin)).Count == 0;
            if (!optionManager.GetGlobal(Options.Server_AllowRegistrations) && !noAdminYet)
                return BadRequest(responseFactory.Error("Registrations are disabled."));

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

            // The first account (when no admin exists yet) becomes the administrator, replacing the
            // old self-promote bootstrap. The token is generated afterwards so its claims include Admin.
            if (noAdminYet)
            {
                if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));

                result = await userManager.AddToRoleAsync(user, UserRoles.Admin);
                if (!result.Succeeded)
                    return StatusCode(StatusCodes.Status500InternalServerError, responseFactory.Error("Failed to assign admin role!", result.ToString()));
            }

            // Login
            JwtSecurityToken token = await GenerateAuthToken(user);

            return Ok(responseFactory.Success(new AuthResponse()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ValidTo = token.ValidTo
            }));
        }

        [HttpPost]
        [Route("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            // Always return the same generic response so this endpoint can't be used to probe which
            // usernames exist. The reset link only ever reaches the user's mailbox or the server log.
            const string genericMessage =
                "If an account with that username exists, password reset instructions have been sent.";

            var user = await userManager.FindByNameAsync(request.Username);
            if (user != null)
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var link = BuildResetLink(user.UserName, token);

                if (emailService.IsConfigured && !string.IsNullOrWhiteSpace(user.Email))
                {
                    try
                    {
                        await emailService.SendAsync(user.Email, "Regard password reset",
                            "A password reset was requested for your Regard account.\n\n" +
                            "Open this link to choose a new password:\n" + link + "\n\n" +
                            "If you didn't request this, you can ignore this email — your password stays unchanged.");
                    }
                    catch (Exception ex)
                    {
                        // Delivery failed; fall back to the log so the reset isn't lost.
                        logger.LogError(ex, "Failed to email password reset to {User}; reset link: {Link}",
                            user.UserName, link);
                    }
                }
                else
                {
                    // No SMTP configured (or no email on file): the admin reads the link from the log.
                    logger.LogWarning("Password reset requested for {User}. No SMTP configured or no email on " +
                        "file — reset link: {Link}", user.UserName, link);
                }
            }

            return Ok(responseFactory.Success(message: genericMessage));
        }

        [HttpPost]
        [Route("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await userManager.FindByNameAsync(request.Username);
            // Same generic error for "no such user" and "bad token" so neither leaks account existence.
            if (user == null)
                return BadRequest(responseFactory.Error("This reset link is invalid or has expired."));

            var result = await userManager.ResetPasswordAsync(user, request.Token, request.Password1);
            if (!result.Succeeded)
            {
                var message = result.Errors.FirstOrDefault()?.Description
                    ?? "This reset link is invalid or has expired.";
                return BadRequest(responseFactory.Error(message));
            }

            return Ok(responseFactory.Success(message: "Your password has been reset. You can now log in."));
        }

        // Builds the absolute /auth/reset-password link. Server_PublicBaseUrl is authoritative; when it's
        // unset we derive scheme+host from the request (correct only same-origin/dev) and warn. The token
        // is URL-encoded exactly once here and decoded exactly once by the frontend query parser.
        private string BuildResetLink(string username, string token)
        {
            var baseUrl = optionManager.GetGlobal(Options.Server_PublicBaseUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = $"{Request.Scheme}://{Request.Host}";
                logger.LogWarning("Server_PublicBaseUrl is not set; deriving the reset-link base URL from the " +
                    "request ({BaseUrl}). Set PublicBaseUrl / REGARD_PUBLIC_BASE_URL for correct links behind a proxy.",
                    baseUrl);
            }

            baseUrl = baseUrl.TrimEnd('/');
            return $"{baseUrl}/auth/reset-password?username={WebUtility.UrlEncode(username)}" +
                   $"&token={WebUtility.UrlEncode(token)}";
        }

        [Authorize]
        [HttpPost]
        [Route("promote")]
        public async Task<IActionResult> Promote([FromBody] UserPromoteRequest promote)
        {
            // Only admins can promote other users. First-admin bootstrap now happens automatically
            // for the first registered account (see Register), so there is no self-promote escape.
            if (!User.IsInRole(UserRoles.Admin))
                return Unauthorized(responseFactory.Error("Only admins can promote users"));

            var user = await userManager.FindByNameAsync(promote.Username);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist!"));

            if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));

            // Idempotent: the first registered account is already an admin (see Register), and the
            // setup wizard still calls promote — so treat "already an admin" as success rather than
            // failing with UserAlreadyInRole.
            if (!await userManager.IsInRoleAsync(user, UserRoles.Admin))
            {
                var result = await userManager.AddToRoleAsync(user, UserRoles.Admin);
                if (!result.Succeeded)
                    return StatusCode(StatusCodes.Status500InternalServerError, responseFactory.Error("Failed to assign admin role!", result.ToString()));
            }

            // Generate new token with updated credentials
            JwtSecurityToken token = await GenerateAuthToken(user);

            return Ok(responseFactory.Success(new AuthResponse() 
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

            return Ok(responseFactory.Success(new MeResponse()
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
