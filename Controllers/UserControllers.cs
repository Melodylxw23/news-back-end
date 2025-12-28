using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;
using System;
using System.Security.Claims;
using System.Text;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;

namespace News_Back_end.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserControllers : ControllerBase
    {
        private readonly MyDBContext _context;
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly Services.IEmailSender _emailSender;
        private readonly IWebHostEnvironment _env;
        private readonly SymmetricSecurityKey _signingKey;

        public UserControllers(MyDBContext context, IConfiguration config,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, 
            RoleManager<IdentityRole> roleManager,
            Services.IEmailSender emailSender,
            IWebHostEnvironment env,
            SymmetricSecurityKey signingKey)
        {
            _context = context;
            _config = config;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _env = env;
            _signingKey = signingKey;
        }

        // --------------------- REGISTER ------------------------------
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterUserDTO dto)
        {
            if (dto.Password != dto.ConfirmPassword)
                return BadRequest("Password and Confirm Password do not match.");

            // Check if email already exists
            if (await _userManager.FindByEmailAsync(dto.Email) != null)
                return BadRequest("Email is already registered.");

            // Validate secret code → assign role
            string adminCode = _config["RoleSecretCodes:Admin"];
            string consultantCode = _config["RoleSecretCodes:Consultant"];

            string assignedRole;
            if (dto.SecretCode == adminCode)
                assignedRole = "Admin";
            else if (dto.SecretCode == consultantCode)
                assignedRole = "Consultant";
            else
                return BadRequest("Invalid secret code.");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.Name,
                WeChatWorkId = dto.WeChatWorkId,
                IsActive = true,
                Lastlogin = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Ensure role exists and assign
            if (!await _roleManager.RoleExistsAsync(assignedRole))
                await _roleManager.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(assignedRole));

            await _userManager.AddToRoleAsync(user, assignedRole);

            return Ok("Registration successful!");
        }

        // --------------------- LOGIN ------------------------------
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginUserDTOs dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Unauthorized("Invalid email or password.");

            if (!user.IsActive)
                return Unauthorized("Account is deactivated.");

            if (!await _userManager.CheckPasswordAsync(user, dto.Password))
                return Unauthorized("Invalid email or password.");

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains("Admin") && dto.SecretCode != _config["RoleSecretCodes:Admin"])
                return Unauthorized("Invalid secret code for Admin.");
            if (roles.Contains("Consultant") && dto.SecretCode != _config["RoleSecretCodes:Consultant"])
                return Unauthorized("Invalid secret code for Consultant.");

            user.Lastlogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Create JWT token for authenticated user
            var jwtIssuer = _config["Jwt:Issuer"] ?? "NewsBackEnd";
            var jwtAudience = _config["Jwt:Audience"] ?? "NewsBackEndAudience";

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim("name", user.Name ?? string.Empty),
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            };

            foreach (var r in roles)
                claims.Add(new Claim(ClaimTypes.Role, r));

            var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { message = $"Login successful. Welcome, {user.Name}", token = tokenString });
        }

        // --------------------- UPDATE USER ------------------------------
        [HttpPut("update")]
        [Authorize]
        public async Task<IActionResult> UpdateUser(UpdateUserDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Use authenticated user as target of update
            var callerEmail = User?.Identity?.Name;
            if (string.IsNullOrEmpty(callerEmail))
                return Unauthorized("Unable to determine caller identity.");

            var user = await _userManager.FindByEmailAsync(callerEmail);

            if (user == null)
                return NotFound("User not found.");

            if (!user.IsActive)
                return BadRequest("Account is deactivated.");

            // Only allow changing name and WeChatWorkId and password
            if (!string.IsNullOrWhiteSpace(dto.Name))
                user.Name = dto.Name;

            if (dto.WeChatWorkId != null)
                user.WeChatWorkId = dto.WeChatWorkId;

            if (!string.IsNullOrEmpty(dto.NewPassword))
            {
                if (string.IsNullOrEmpty(dto.CurrentPassword))
                    return BadRequest("Current password is required to change password.");

                var changeResult = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
                if (!changeResult.Succeeded)
                    return BadRequest(changeResult.Errors);
            }

            user.Lastlogin = DateTime.UtcNow;
            var upd = await _userManager.UpdateAsync(user);
            if (!upd.Succeeded)
                return BadRequest(upd.Errors);

            return Ok("User updated successfully.");
        }

        
        // --------------------- FORGOT PASSWORD (email flow) ------------------------------
        [HttpPost("forgot-password-request")]
        public async Task<IActionResult> ForgotPasswordRequest(ForgotPasswordRequestDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
            {
                // Do not reveal whether the email exists
                return Ok();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            var frontendResetUrl = _config["Frontend:ResetPasswordUrl"] ?? "https://example.com/reset-password";
            var link = $"{frontendResetUrl}?email={WebUtility.UrlEncode(user.Email)}&token={encodedToken}";

            var subject = "Password reset request";
            var html = $"<p>Click the link to reset your password:</p><p><a href=\"{link}\">Reset password</a></p>";

            await _emailSender.SendEmailAsync(user.Email, subject, html);

            return Ok("Password reset email sent.");
        }

        // Development helper: preview reset link without sending email
        [HttpPost("preview-reset-link")]
        public async Task<IActionResult> PreviewResetLink(ForgotPasswordRequestDTO dto)
        {
            if (!_env.IsDevelopment())
                return Forbid();

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return Ok(new { link = (string?)null });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);
            var frontendResetUrl = _config["Frontend:ResetPasswordUrl"] ?? "https://example.com/reset-password";
            var link = $"{frontendResetUrl}?email={WebUtility.UrlEncode(user.Email)}&token={encodedToken}";
            return Ok(new { link });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound("User not found.");

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest("New password and confirmation do not match.");

            var decodedToken = WebUtility.UrlDecode(dto.Token);
            var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, dto.NewPassword);
            if (!resetResult.Succeeded)
                return BadRequest(resetResult.Errors);

            user.Lastlogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return Ok("Password has been reset.");
        }

        // --------------------- ACTIVATE / DEACTIVATE USER ------------------------------
        [HttpPost("set-active")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SetActive(SetActiveDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound("User not found.");

            // Only applies to Admin or Consultant (roles)
            var isTargetAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            var isTargetConsultant = await _userManager.IsInRoleAsync(user, "Consultant");
            if (!isTargetAdmin && !isTargetConsultant)
                return BadRequest("Only Admin or Consultant users can be activated/deactivated.");

            user.IsActive = dto.IsActive;
            var updRes = await _userManager.UpdateAsync(user);
            if (!updRes.Succeeded)
                return BadRequest(updRes.Errors);

            return Ok($"User {(dto.IsActive ? "activated" : "deactivated")} successfully.");
        }

        // Identity handles password hashing and verification


        // --------------------- REGISTER MEMBER (creates Identity user + Member profile) ------------------------------
        [HttpPost("register-member")]
        public async Task<IActionResult> RegisterMember(MemberDTOs dto)
        {
            if (dto.Password != dto.ConfirmPassword)
                return BadRequest("Password and Confirm Password do not match.");

            if (await _userManager.FindByEmailAsync(dto.Email) != null)
                return BadRequest("Email is already registered.");

            var appUser = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.ContactPerson,
                WeChatWorkId = dto.WeChatWorkId,
                IsActive = true,
                Lastlogin = DateTime.UtcNow
            };

            var res = await _userManager.CreateAsync(appUser, dto.Password);
            if (!res.Succeeded)
                return BadRequest(res.Errors);

            // assign Member role
            if (!await _roleManager.RoleExistsAsync("Member"))
                await _roleManager.CreateAsync(new IdentityRole("Member"));

            await _userManager.AddToRoleAsync(appUser, "Member");

            // create member profile
            var member = new Member
            {
                CompanyName = dto.CompanyName,
                ContactPerson = dto.ContactPerson,
                Email = dto.Email,
                WeChatWorkId = dto.WeChatWorkId,
                Country = dto.Country,
                PreferredLanguage = dto.PreferredLanguage,
                PreferredChannel = dto.PreferredChannel,
                MembershipType = dto.MembershipType,
                ApplicationUserId = appUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            _context.Members.Add(member);
            await _context.SaveChangesAsync();

            return Ok("Member registered successfully.");
        }

        // --------------------- LINK EXISTING MEMBER TO APPLICATIONUSER ------------------------------
        [HttpPost("link-member")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> LinkMemberToUser([FromBody] LinkMemberDTO dto)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberId == dto.MemberId);
            if (member == null)
                return NotFound("Member not found.");

            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
                return NotFound("User not found.");

            member.ApplicationUserId = user.Id;
            await _context.SaveChangesAsync();

            // ensure Member role
            if (!await _roleManager.RoleExistsAsync("Member"))
                await _roleManager.CreateAsync(new IdentityRole("Member"));
            await _userManager.AddToRoleAsync(user, "Member");

            return Ok("Linked member to user.");
        }

        // --------------------- GET CURRENT USER PROFILE & ROLES (for testing) ------------------------------
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUserProfile()
        {
            var email = User?.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);

            var member = await _context.Members
                .Where(m => m.ApplicationUserId == user.Id)
                .Select(m => new
                {
                    m.MemberId,
                    m.CompanyName,
                    m.ContactPerson,
                    m.Email,
                    m.WeChatWorkId,
                    m.Country,
                    m.PreferredLanguage,
                    m.PreferredChannel,
                    m.MembershipType,
                    m.CreatedAt
                })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                user.Id,
                user.Email,
                user.UserName,
                user.Name,
                user.WeChatWorkId,
                user.IsActive,
                user.Lastlogin,
                Roles = roles,
                Member = member
            });
        }
    }
}
