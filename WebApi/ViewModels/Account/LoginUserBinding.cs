using System.ComponentModel.DataAnnotations;
using WebApi.Data;

namespace WebApi.ViewModels.Account
{
    public class LoginUserBinding
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}