using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GeminiAzureProxy.Models
{
    public class GeminiApiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; } = new List<Content>();
    }
    public class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; } = new List<Part>();
    }
    public class Part 
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    
    }
}
