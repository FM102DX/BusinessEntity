using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BusinessEntity.Pages.Auth
{
    public class ErrorModel : PageModel
    {
        public string ErrorMessage { get; set; } = "An authentication error occurred";

        public void OnGet([FromQuery] string? message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ErrorMessage = message;
            }
        }
    }
}