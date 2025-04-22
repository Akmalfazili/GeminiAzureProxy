using Microsoft.AspNetCore.Routing.Constraints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeminiAzureProxy.Model
{
    public class ProcessFileRequest
    {
        public string? FilePath {  get; set; }
        public string? Prompt { get;set;}
        
    }
}
