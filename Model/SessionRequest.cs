using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GeminiAzureProxy.Model
{
    public class SessionRequest
    {
        [JsonProperty("sessionId")]
        public string? SessionId { get; set; }

        [JsonProperty("currentPrompt")]
        public string? CurrentPrompt { get; set; }

        [JsonProperty("filePath")]
        public string? FilePath { get; set; }
    }
}
