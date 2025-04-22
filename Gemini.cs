using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GeminiAzureProxy.Model;
using Microsoft.Extensions.Configuration;
using GeminiAzureProxy.Service;
using GenerativeAI;
using System.Text.Json;


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
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _geminiService.GetWelcomeMessage();
            string geminiApiKey = _geminiService.GetApiKey();
            if (string.IsNullOrEmpty(geminiApiKey))
            {
                _logger.LogError("Gemini API Key is not configured");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            GeminiRequest? requestBody = null;
            try
            {
                requestBody = await req.ReadFromJsonAsync<GeminiRequest>();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Error deserializing request body: {ex.Message}");
                return new BadRequestObjectResult("Please provide a valid JSON request body");
            }

            if (requestBody == null || string.IsNullOrEmpty(requestBody.Prompt))
            {
                return new BadRequestObjectResult("Please enter a prompt");
            }

            string prompt = requestBody.Prompt;
            _logger.LogInformation($"Received prompt: `{prompt}`");
            try
            {
                var googleAi = new GoogleAi(geminiApiKey);
                var googleModel = googleAi.CreateGeminiModel("models/gemini-2.0-flash");
                var googleResponse = await googleModel.GenerateContentAsync(prompt);
                string? generatedText = googleResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;

                if (string.IsNullOrEmpty(generatedText))
                {
                    _logger.LogWarning("Gemini API returned an empty response");
                    return new OkObjectResult("No content generated");
                }

                _logger.LogInformation("Successfully generated text from Gemini");

                return new OkObjectResult(generatedText);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"An error occured while calling Gemini API: {ex.Message}");

                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException.Message}");
                }

                return new InternalServerErrorResult($"An error occured while calling the Gemini API: {ex.Message}");

            }
        }

        public class InternalServerErrorResult : ObjectResult
        {
            public InternalServerErrorResult(object value) : base(value)
            {
                StatusCode = StatusCodes.Status500InternalServerError;
            }
        }

        [Function("FileReader")]
        public async Task<IActionResult> RunFileReader([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _geminiService.GetWelcomeMessage();
            string geminiApiKey = _geminiService.GetApiKey();
            if (string.IsNullOrEmpty(geminiApiKey))
            {
                _logger.LogError("Gemini API Key is not configured");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
            ProcessFileRequest? requestBody = null;
            try
            {
                requestBody = await req.ReadFromJsonAsync<ProcessFileRequest>();
            }
            catch (JsonException ex)
            {
                _logger.LogError($"Error deserializing request body: {ex.Message}");
                return new BadRequestObjectResult("Please provide a valid JSON request body");
            }

            if (requestBody == null || string.IsNullOrEmpty(requestBody.Prompt) || string.IsNullOrEmpty(requestBody.FilePath))
            {
                return new BadRequestObjectResult("Please provice file path and prompt in the request body");
            }

            string filePath = requestBody.FilePath;
            string prompt = requestBody.Prompt;
            string? fileContent = null;

            _logger.LogInformation($"Attempting to read file: '{filePath}'");

            try
            {

            }
        }
    }
}
