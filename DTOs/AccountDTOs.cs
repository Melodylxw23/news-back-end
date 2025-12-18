using System.ComponentModel.DataAnnotations;

namespace News_Back_end.DTOs
{
    public class RegisterUserDTO
    {
        [Required, MaxLength(50)]
        public string Name { get; set; }

        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Password and ConfirmPassword does not match.")]
        public string ConfirmPassword { get; set; }

        public string? WeChatWorkId { get; set; }

        [Required]
        public string SecretCode { get; set; }
    }

    public class LoginUserDTOs
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
        /// <summary>
        /// Optional secret code used only when logging in as Admin or Consultant.
        /// Members may omit this value.
        /// </summary>
        public string? SecretCode { get; set; }
    }

    public class UpdateUserDTO
    {
        // Current password required for verification when changing password
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [MaxLength(50)]
        public string? Name { get; set; }

        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "NewPassword and ConfirmPassword does not match.")]
        public string? ConfirmPassword { get; set; }

        public string? WeChatWorkId { get; set; }
    }

    public class ForgotPasswordRequestDTO
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }
    }

    public class ResetPasswordDTO
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "NewPassword and ConfirmPassword does not match.")]
        public string ConfirmPassword { get; set; }
    }

    public class SetActiveDTO
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [Required]
        public bool IsActive { get; set; }
    }
}
