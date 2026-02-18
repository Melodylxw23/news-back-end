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
        private readonly GmailEmailService _emailService;
        private readonly IWebHostEnvironment _env;
        private readonly SymmetricSecurityKey _signingKey;

        public UserControllers(MyDBContext context, IConfiguration config,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, 
            RoleManager<IdentityRole> roleManager,
            GmailEmailService emailService,
            IWebHostEnvironment env,
            SymmetricSecurityKey signingKey)
        {
            _context = context;
            _config = config;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailService = emailService;
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

            // Validate secret code → assign role (Consultant registration via this endpoint removed)
            string adminCode = _config["RoleSecretCodes:Admin"];

            string assignedRole;
            if (dto.SecretCode == adminCode)
                assignedRole = "Admin";
            else
                return BadRequest("Invalid secret code.");

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.Name,
                WeChatWorkId = dto.WeChatWorkId,
                IsActive = true,
                Lastlogin = DateTime.Now
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

        //-----------------------------------------------------

        // GET: api/UserControllers/members
        [HttpGet("members")]
        [Authorize(Roles = "Admin,Consultant")]
    public async Task<IActionResult> GetAllMembers()
  {
          var members = await _context.Members
    .Include(m => m.IndustryTags)
  .Include(m => m.Interests)
     .Select(m => new
  {
     m.MemberId,
       Name = m.ContactPerson,
       m.Email,
           m.CompanyName,
         IndustryTags = m.IndustryTags.Select(t => new { t.IndustryTagId, t.NameEN, t.NameZH }).ToList(),
                    InterestTags = m.Interests.Select(t => new { t.InterestTagId, t.NameEN, t.NameZH }).ToList(),
  IsActive = _context.Users.Where(u => u.Id == m.ApplicationUserId).Select(u => u.IsActive).FirstOrDefault()
    })
          .ToListAsync();

          return Ok(new { message = "Members retrieved successfully.", data = members });
        }

        // GET: api/UserControllers/consultants
        [HttpGet("consultants")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllConsultants()
        {
            var consultants = await _userManager.Users
                .Where(u => _context.UserRoles.Any(ur => ur.UserId == u.Id &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Consultant")))
                .Select(u => new
                {
                    Id = u.Id,
                    Name = u.Name,
                    Email = u.Email,
                    u.WeChatWorkId,
                    u.IsActive,
                    u.Lastlogin
                })
                .ToListAsync();

            return Ok(new { message = "Consultants retrieved successfully.", data = consultants });
        }

        // PUT: api/UserControllers/activate/{email}
        [HttpPut("activate/{email}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ActivateUser(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            user.IsActive = true;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "User activated successfully." });
        }

        // PUT: api/UserControllers/deactivate/{email}
        [HttpPut("deactivate/{email}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeactivateUser(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "User deactivated successfully." });
        }

        // --------------------- CREATE CONSULTANT (Admin only) ------------------------------
        [HttpPost("create-consultant")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateConsultant([FromBody] CreateConsultantDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _userManager.FindByEmailAsync(dto.Email) != null)
                return BadRequest(new { message = "Email is already registered." });

            // Generate temporary password for the consultant (admin does not provide password)
            var tempPassword = GenerateTemporaryPassword();

            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.Name,
                WeChatWorkId = null,
                IsActive = true,
                Lastlogin = DateTime.Now,
                MustChangePassword = true  // Force password change on first login
            };

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Ensure Consultant role exists and assign
            if (!await _roleManager.RoleExistsAsync("Consultant"))
                await _roleManager.CreateAsync(new IdentityRole("Consultant"));

            await _userManager.AddToRoleAsync(user, "Consultant");

            // Send email to consultant with secret code and temporary password
            await SendConsultantRegistrationEmail(user.Email, tempPassword);

            return Ok(new { message = "Consultant created successfully. They must change password on first login." });
        }

        // Helper method to send consultant registration email (includes secret code and temporary password)
        private async Task SendConsultantRegistrationEmail(string email, string tempPassword)
        {
            var subject = "Your Consultant Account Has Been Created";

            // Get secret code for consultant role from configuration
            var consultantSecret = _config["RoleSecretCodes:Consultant"] ?? string.Empty;

            // Generate password reset token for the email link
            var user = await _userManager.FindByEmailAsync(email);
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = WebUtility.UrlEncode(token);

            var resetLink = $"{_config["Frontend:ResetPasswordUrl"]}?email={WebUtility.UrlEncode(email)}&token={encodedToken}";

            var html = $@"
<p>Hello,</p>
<p>An administrator has created a Consultant account for you. Please use the information below to access your account:</p>
<p><strong>Email:</strong> {email}</p>
<p><strong>Temporary Password:</strong> {tempPassword}</p>
<p><strong>Consultant Secret Code:</strong> {consultantSecret}</p>
<p>For security reasons, you must change your password on your first login.</p>
<p><a href='{resetLink}'>Click here to set your new password</a></p>
<p>Best regards,<br/>The News Team</p>";

            await _emailService.SendEmailAsync(email, subject, html);
        }

        // --------------------- SET INITIAL PASSWORD (for first-time login) ------------------------------
        [HttpPost("set-initial-password")]
        [Authorize]
        public async Task<IActionResult> SetInitialPassword([FromBody] SetInitialPasswordDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = User?.Identity?.Name;
            if (string.IsNullOrEmpty(email))
                return Unauthorized();

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return NotFound(new { message = "User not found." });

            if (!user.MustChangePassword)
                return BadRequest(new { message = "You are not required to change your password." });

            if (dto.NewPassword != dto.ConfirmPassword)
                return BadRequest(new { message = "Passwords do not match." });

            // Generate reset token and change password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Clear the flag
            user.MustChangePassword = false;
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Password changed successfully. You can now login with your new password." });
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

            user.Lastlogin = DateTime.Now;
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
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Check if member needs to select topics (only for Member role)
            bool needsTopicSelection = false;
            if (roles.Contains("Member") && !user.HasSelectedTopics)
            {
                needsTopicSelection = true;
            }

            return Ok(new
            {
                message = $"Login successful. Welcome, {user.Name}",
                token = tokenString,
                mustChangePassword = user.MustChangePassword,
                needsTopicSelection = needsTopicSelection  // ADD THIS LINE
            });
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

            user.Lastlogin = DateTime.Now;
            var upd = await _userManager.UpdateAsync(user);
            if (!upd.Succeeded)
                return BadRequest(upd.Errors);

            return Ok("User updated successfully.");
        }

        // --------------------- SELECT TOPICS (for first-time member login) ------------------------------
        [HttpPost("select-topics")]
        [Authorize] // User must be authenticated
        public async Task<IActionResult> SelectTopics([FromBody] SelectTopicsDTO dto)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Get the member profile
                var member = await _context.Members
                    .Include(m => m.Interests)
                    .FirstOrDefaultAsync(m => m.ApplicationUserId == userId);

                if (member != null)
                {
                    // Clear existing interests
                    member.Interests.Clear();

                    // Add new selections for existing tags only
                    foreach (var tagId in dto.InterestTagIds)
                    {
                        var tag = await _context.InterestTags.FindAsync(tagId);
                        if (tag != null)
                        {
                            member.Interests.Add(tag);
                        }
                    }

                    // Update member preferences
                    member.PreferredLanguage = dto.PreferredLanguage;

                    // Update notification channels
                    member.NotificationChannels = dto.NotificationChannels;
                }

                // Mark that user has selected topics
                user.HasSelectedTopics = true;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Topics saved successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error saving topics: " + ex.Message });
            }
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

            await _emailService.SendEmailAsync(user.Email, subject, html);

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

     // Do NOT URL decode - the token comes already URL-encoded from the frontend
            // and ASP.NET Identity expects the raw token
var resetResult = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
     if (!resetResult.Succeeded)
                return BadRequest(resetResult.Errors);

  user.Lastlogin = DateTime.Now;
await _userManager.UpdateAsync(user);

 return Ok("Password has been reset.");
        }

     // --------------------- RESET MEMBER PASSWORD (Alias for reset-password) ------------------------------
  [HttpPost("reset-member-password")]
      public async Task<IActionResult> ResetMemberPassword(ResetPasswordDTO dto)
        {
   if (!ModelState.IsValid)
     return BadRequest(ModelState);

    var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null)
          return NotFound(new { message = "User not found." });

       if (dto.NewPassword != dto.ConfirmPassword)
   return BadRequest(new { message = "New password and confirmation do not match." });

     // Do NOT URL decode - the token comes already URL-encoded from the frontend
       // and we need to pass it as-is to ResetPasswordAsync
  var resetResult = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
       if (!resetResult.Succeeded)
         return BadRequest(new { message = "Failed to reset password.", errors = resetResult.Errors });

    user.Lastlogin = DateTime.Now;
     await _userManager.UpdateAsync(user);

     return Ok(new { message = "Password has been reset successfully. You can now login with your new password." });
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

        // --------------------- REGISTER MEMBER (Consultant only) ------------------------------
        [HttpPost("register-member")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> RegisterMember(RegisterMemberByConsultantDTO dto)
        {
            if (!ModelState.IsValid)
  return BadRequest(ModelState);

            if (await _userManager.FindByEmailAsync(dto.Email) != null)
     return BadRequest("Email is already registered.");

            // Generate temporary password for the member
    var tempPassword = GenerateTemporaryPassword();

  var appUser = new ApplicationUser
  {
     UserName = dto.Email,
          Email = dto.Email,
         Name = dto.ContactPerson,
    WeChatWorkId = dto.WeChatWorkId,
  IsActive = true,
        Lastlogin = DateTime.Now,
    MustChangePassword = true
    };

  var res = await _userManager.CreateAsync(appUser, tempPassword);
 if (!res.Succeeded)
  return BadRequest(res.Errors);

            // Assign Member role
            if (!await _roleManager.RoleExistsAsync("Member"))
     await _roleManager.CreateAsync(new IdentityRole("Member"));

          await _userManager.AddToRoleAsync(appUser, "Member");

            // Create member profile
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
      CreatedAt = DateTime.Now
            };

    _context.Members.Add(member);
    await _context.SaveChangesAsync();

          // Add industry tag relationship
  if (dto.IndustryTagId > 0)
          {
      var industryTag = await _context.IndustryTags.FindAsync(dto.IndustryTagId);
                if (industryTag != null)
    {
                    member.IndustryTags.Add(industryTag);
     await _context.SaveChangesAsync();
        }
   }

         // Send email with temporary password
      await SendMemberRegistrationEmail(appUser.Email, tempPassword);

  return Ok(new
         {
      message = "Member registered successfully. Invitation email has been sent to the member.",
     memberId = member.MemberId,
              userId = appUser.Id
            });
}

        // Helper method to generate temporary password
        private string GenerateTemporaryPassword()
        {
    const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%";
            var random = new Random();
            var password = new StringBuilder();

     // Ensure at least one uppercase, one lowercase, one digit, one special char
  password.Append(validChars[random.Next(26, 52)]);  // Uppercase
      password.Append(validChars[random.Next(0, 26)]);   // Lowercase
            password.Append(validChars[random.Next(52, 62)]);  // Digit
            password.Append(validChars[random.Next(62, validChars.Length)]);  // Special

            // Add random characters to reach 12 characters minimum
   for (int i = 0; i < 8; i++)
    {
     password.Append(validChars[random.Next(validChars.Length)]);
       }

  return password.ToString();
    }

      // Helper method to send member registration email
    private async Task SendMemberRegistrationEmail(string email, string tempPassword)
  {
            var subject = "Your Account Has Been Created";
 
 // Generate password reset token for the email link
            var user = await _userManager.FindByEmailAsync(email);
 var token = await _userManager.GeneratePasswordResetTokenAsync(user);
   var encodedToken = WebUtility.UrlEncode(token);
      
            var resetLink = $"{_config["Frontend:ResetPasswordUrl"]}?email={WebUtility.UrlEncode(email)}&token={encodedToken}";

            var html = $@"
<p>Hello,</p>
<p>Your account has been created by your Consultant. Please use the following information to access your account:</p>
<p><strong>Email:</strong> {email}</p>
<p><strong>Temporary Password:</strong> {tempPassword}</p>
<p>For security reasons, you must change your password on your first login.</p>
<p><a href='{resetLink}'>Click here to set your new password</a></p>
<p>Best regards,<br/>The News Team</p>";

     await _emailService.SendEmailAsync(email, subject, html);
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

   // --------------------- UPDATE NOTIFICATION PREFERENCES ------------------------------
    [HttpPost("update-notification-preferences")]
        [Authorize]
        public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferencesDTO dto)
{
 try
       {
         var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Get the member profile
  var member = await _context.Members
        .FirstOrDefaultAsync(m => m.ApplicationUserId == userId);

         if (member == null)
           return NotFound(new { message = "Member profile not found" });

                // Update notification channels
    member.NotificationChannels = dto.NotificationChannels;

  await _context.SaveChangesAsync();

     return Ok(new { message = "Notification preferences updated successfully!" });
     }
            catch (Exception ex)
       {
         return StatusCode(500, new { message = "Error updating preferences: " + ex.Message });
            }
        }

    // --------------------- UPDATE NOTIFICATION FREQUENCY ------------------------------
        [HttpPost("update-notification-frequency")]
        [Authorize]
        public async Task<IActionResult> UpdateNotificationFrequency([FromBody] UpdateNotificationFrequencyDTO dto)
    {
            try
   {
       var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

     // Get the member profile
                var member = await _context.Members
         .FirstOrDefaultAsync(m => m.ApplicationUserId == userId);

     if (member == null)
       return NotFound(new { message = "Member profile not found" });

   // Update notification frequency settings
       member.NotificationFrequency = dto.NotificationFrequency;
      member.NotificationLanguage = dto.NotificationLanguage;
   member.ApplyToAllTopics = dto.ApplyToAllTopics;

                await _context.SaveChangesAsync();

         return Ok(new { message = "Notification frequency updated successfully!" });
          }
     catch (Exception ex)
            {
       return StatusCode(500, new { message = "Error updating frequency: " + ex.Message });
          }
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

            // First, get the member WITH interests and industry tags loaded
        var memberEntity = await _context.Members
  .Include(m => m.Interests)
   .Include(m => m.IndustryTags)
        .FirstOrDefaultAsync(m => m.ApplicationUserId == user.Id);

          // Then, manually projection to anonymous object
        object member = null;
            if (memberEntity != null)
{
          member = new
      {
        memberEntity.MemberId,
        memberEntity.CompanyName,
        memberEntity.ContactPerson,
      memberEntity.Email,
       memberEntity.WeChatWorkId,
  memberEntity.Country,
        memberEntity.PreferredLanguage,
             memberEntity.PreferredChannel,
        memberEntity.MembershipType,
        memberEntity.CreatedAt,
       memberEntity.NotificationChannels,
    memberEntity.NotificationFrequency,
     memberEntity.NotificationLanguage,
     memberEntity.ApplyToAllTopics,
     Interests = memberEntity.Interests?.Select(i => new { i.InterestTagId, i.NameEN, i.NameZH }).ToList(),
     IndustryTags = memberEntity.IndustryTags?.Select(i => new { i.IndustryTagId, i.NameEN, i.NameZH }).ToList()
   };
            }

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

        // --------------------- DELETE MEMBER (Consultant only) ------------------------------
   [HttpDelete("delete-member/{memberId}")]
        [Authorize(Roles = "Consultant")]
        public async Task<IActionResult> DeleteMember(int memberId)
   {
            try
            {
    var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberId == memberId);
 if (member == null)
                 return NotFound(new { message = "Member not found." });

           // Get the associated ApplicationUser
        var appUser = await _userManager.FindByIdAsync(member.ApplicationUserId);
         
    // Remove member from database
         _context.Members.Remove(member);
             await _context.SaveChangesAsync();

           // Delete the associated ApplicationUser if it exists
    if (appUser != null)
           {
        var deleteResult = await _userManager.DeleteAsync(appUser);
        if (!deleteResult.Succeeded)
   return BadRequest(new { message = "Member deleted but user account deletion failed.", errors = deleteResult.Errors });
       }

       return Ok(new { message = "Member and associated user account deleted successfully." });
            }
         catch (Exception ex)
       {
  return StatusCode(500, new { message = "Error deleting member: " + ex.Message });
     }
        }

        // --------------------- DELETE CONSULTANT (Admin only) ------------------------------
 [HttpDelete("delete-consultant/{consultantId}")]
 [Authorize(Roles = "Admin")]
 public async Task<IActionResult> DeleteConsultant(string consultantId)
 {
 try
 {
 if (string.IsNullOrEmpty(consultantId))
 return BadRequest(new { message = "Consultant id is required." });

 var user = await _userManager.FindByIdAsync(consultantId);
 if (user == null)
 return NotFound(new { message = "Consultant not found." });

 // Ensure target is a Consultant
 if (!await _userManager.IsInRoleAsync(user, "Consultant"))
 return BadRequest(new { message = "The specified user is not a Consultant." });

 var deleteResult = await _userManager.DeleteAsync(user);
 if (!deleteResult.Succeeded)
 return BadRequest(new { message = "Failed to delete consultant.", errors = deleteResult.Errors });

 return Ok(new { message = "Consultant account deleted successfully." });
 }
 catch (Exception ex)
 {
 return StatusCode(500, new { message = "Error deleting consultant: " + ex.Message });
 }
 }
    }
}
