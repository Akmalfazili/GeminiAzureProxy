using System.ComponentModel.DataAnnotations;


namespace GeminiAzureProxy.Models
{
    public class GenerateRequest
    {
        [Required]
        public string? Prompt { get; set; }
    }
}
