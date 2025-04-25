using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GeminiAzureProxy.Model
{
    public class ConversationPart
    {
        [JsonProperty("text")]
        public string? Text { get; set; }
    }
}
