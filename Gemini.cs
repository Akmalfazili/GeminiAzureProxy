using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GeminiAzureProxy.Model;
using Microsoft.Extensions.Configuration;
using GeminiAzureProxy.Service;
using GenerativeAI;


namespace GeminiAzureProxy
{
    public class Gemini
    {

        private readonly GeminiService _geminiService;
        private readonly ILogger<Gemini> _logger;
        

        public Gemini(GeminiService geminiService, ILogger<Gemini> logger)
        {
            _geminiService = geminiService;
            _logger = logger;
        }

        [Function("Gemini")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ExecutionContext context)
        {
            _geminiService.GetWelcomeMessage();
            string geminiApiKey = _geminiService.GetApiKey();
            if (string.IsNullOrEmpty(geminiApiKey))
            {
                _logger.LogError("Gemini API Key is not configured");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            var requestBody = await req.ReadFromJsonAsync<GeminiRequest>();
            if (requestBody == null || string.IsNullOrEmpty(requestBody.Prompt))
            {
                return new BadRequestObjectResult("Please enter a prompt");
            }

            string prompt = requestBody.Prompt;
            _logger.LogInformation($"Received prompt: `{prompt}`");

            
             var googleAi = new GoogleAi(geminiApiKey);
             var googleModel = googleAi.CreateGeminiModel("models/gemini-2.0-flash");
             var googleResponse = await googleModel.GenerateContentAsync(prompt);
             string? generatedText = googleResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;
               
            
            
            return new OkObjectResult(generatedText);
        }
    }
}
