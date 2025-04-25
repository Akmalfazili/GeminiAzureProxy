using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;


namespace GeminiAzureProxy.Model
{
    public class SessionResponse
    {
        [JsonProperty ("sessionId")]
        public string? SessionId { get; set; }

        [JsonProperty("modelResponse")]
        public string? ModelResponse { get; set; }
    }
}
