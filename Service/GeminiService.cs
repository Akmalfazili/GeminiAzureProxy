using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeminiAzureProxy.Service
{
    public class GeminiService
    {
        private readonly ILogger<GeminiService> _logger;
        private readonly IConfiguration _configuration;

        public GeminiService (ILogger<GeminiService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public string GetApiKey()
        {
            return _configuration["GeminiApiKey"];
        }
        public string GetSessionCache()
        {
            return _configuration["SessionCache"];
        }
        public string GetWelcomeMessage()
        {
            _logger.LogInformation("C# HTTP trigger function processed a request");
            return _configuration["WelcomeMessage"] ?? "Welcome to Azure Functions";
        }
    }
}
