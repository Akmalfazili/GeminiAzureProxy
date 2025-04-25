using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GeminiAzureProxy.Model
{
    public class ConversationTurn
    {
        [JsonProperty("role")]
        public string? Role { get; set; }

        [JsonProperty("parts")]
        public List<ConversationPart> Parts { get; set; } = new List<ConversationPart>();
    }
}
