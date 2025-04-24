using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using GeminiAzureProxy.Model;
using Microsoft.Extensions.Configuration;
using GeminiAzureProxy.Service;
using GenerativeAI;
using System.Text.Json;
using UglyToad.PdfPig;
using System.Text;
using NPOI.XWPF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Formula.Functions;


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
                return new BadRequestObjectResult("Please provide file path and prompt in the request body");
            }

            string filePath = requestBody.FilePath;
            string prompt = requestBody.Prompt;
            string? fileContent = null;

            _logger.LogInformation($"Attempting to read file: '{filePath}'");

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError($"File not found: {filePath}");
                    return new NotFoundObjectResult($"File not found: {filePath}");
                }

                //fileContent = await File.ReadAllTextAsync(filePath);
                fileContent = await ExtractTextFromFileAsync(filePath, _logger);
                if (fileContent == null)
                {
                    return new BadRequestObjectResult($"Could not extract text from file {Path.GetFileName(filePath)}. Ensure it is a supported format (txt, pdf, docx, xlsx/xls etc..) or is a readable text");
                }
                _logger.LogInformation($"Successfully read content from {filePath}. Content length: {fileContent.Length}");
            }catch(UnauthorizedAccessException ex)
            {
                _logger.LogError($"Permission denied to read file: {filePath}. Error: {ex.Message}");
                return new UnauthorizedResult();
            }catch(Exception ex)
            {
                _logger.LogError($"An error occured while reading the file {filePath}. Error: {ex.Message}");
                return new InternalServerErrorResult($"An error occured reading the file: {ex.Message}");
            }

            try
            {
                var googleAi = new GoogleAi(geminiApiKey);
                var googleModel = googleAi.CreateGeminiModel("models/gemini-2.0-flash");
                string fullPrompt = $"Regarding the content of the file {Path.GetFileName(filePath)}: \n\n---\n{fileContent}\n---\n\n{prompt}";
                var googleResponse = await googleModel.GenerateContentAsync(fullPrompt);
                string? generatedText = googleResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;

                if (string.IsNullOrEmpty(generatedText))
                {
                    _logger.LogWarning($"Gemini API returned an empy response for the file {filePath}");
                    return new OkObjectResult(new { message = $"No content generated by Gemini regarding {Path.GetFileName(filePath)}" });
                }

                _logger.LogInformation($"Successfully generated text from Gemini for file {filePath}");
                return new OkObjectResult(new { generatedText = generatedText });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occured calling Gemini API for the file {filePath}:{ex.Message}");

                if( ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException}");
                }

                return new InternalServerErrorResult($"An error occured while calling the Gemini API: {ex.Message}");
            }


        }

        private static async Task<string> ExtractTextFromFileAsync(string filePath, ILogger logger)
        {
            string? fileExtension = Path.GetExtension(filePath)?.ToLowerInvariant();

            try
            {
                switch (fileExtension)
                {
                    case ".txt":
                    case ".csv":
                    case ".json":
                    case ".xml":
                    case ".html":
                    case ".css":
                    case ".js":
                    case ".py":
                    case ".java":
                        logger.LogInformation($"Attempting to read {filePath} as plain text");
                        return await File.ReadAllTextAsync(filePath);
                    case ".pdf":
                        logger.LogInformation($"Attempting to extract text from PDF: {filePath}");
                        using (var document = PdfDocument.Open(filePath))
                        {
                            StringBuilder textBuilder = new StringBuilder();
                            foreach (var page in document.GetPages())
                            {
                                textBuilder.Append(page.Text);
                                textBuilder.Append("\n--- Page Break ---\n");
                            }
                            return textBuilder.ToString();
                        }
                    case ".docx":
                        logger.LogInformation($"Attempting to extract text from DOCX: {filePath}");
                        using(FileStream file = new FileStream(filePath,FileMode.Open, FileAccess.Read))
                        {
                            XWPFDocument document = new XWPFDocument(file);
                            StringBuilder textBuilder = new StringBuilder();
                            foreach( var para in document.Paragraphs)
                            {
                                textBuilder.Append(para.Text);
                                textBuilder.Append("\n");
                            }
                            return textBuilder.ToString();
                        }
                    case ".xlsx":
                    case ".xls":
                        logger.LogInformation($"Attempting to extract text from Excel: {filePath}");
                        using (FileStream file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            IWorkbook workbook;
                            StringBuilder textBuilder = new StringBuilder();
                            try
                            {
                                workbook = WorkbookFactory.Create(file);

                                for (int i = 0; i < workbook.NumberOfSheets; i++)
                                {
                                    ISheet sheet = workbook.GetSheetAt(i);
                                    if (sheet == null) continue;
                                    textBuilder.Append($"\n--- Sheet: {sheet.SheetName} ---\n");

                                    // Determine the maximum column index used on this sheet
                                    int maxCellIndex = -1;
                                    // Iterate through all potential rows in the sheet's used range
                                    for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
                                    {
                                        IRow tempRow = sheet.GetRow(rowIndex);
                                        if (tempRow != null)
                                        {
                                            maxCellIndex = Math.Max(maxCellIndex, tempRow.LastCellNum - 1);
                                        }
                                    }

                                    if (maxCellIndex < 0)
                                    {
                                        logger.LogInformation($"Sheet '{sheet.SheetName}' appears to have no data cells within rows {sheet.FirstRowNum} to {sheet.LastRowNum}. Max cell index remains -1.");
                                    }
                                    else
                                    {
                                        logger.LogInformation($"Sheet '{sheet.SheetName}': Max cell index determined as {maxCellIndex}. Processing rows {sheet.FirstRowNum} to {sheet.LastRowNum}.");
                                    }

                                    for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
                                    {
                                        IRow row = sheet.GetRow(rowIndex);
                                        for (int cellIndex = 0; cellIndex <= maxCellIndex; cellIndex++)
                                        {
                                            if (cellIndex > 0)
                                            {
                                                textBuilder.Append("\t"); // Append tab separator between columns
                                            }
                                            NPOI.SS.UserModel.ICell cell = row?.GetCell(cellIndex);
                                            textBuilder.Append(cell?.ToString() ?? "");
                                        }
                                        textBuilder.Append("\n");
                                    }
                                }
                                return textBuilder.ToString();
                            }

                            catch (Exception ex)
                            {
                                logger.LogError($"Error processing Excel data for {filePath}: {ex.Message}");
                                throw;
                            }
                        }
                        
                    default:
                        logger.LogWarning($"Unsupported file extension for specific text extraction logic: {fileExtension} in {filePath}. Attempting to read as plain text as a fallback.");
                        try
                        {
                            return await File.ReadAllTextAsync(filePath);
                        }catch(Exception ex)
                        {
                            logger.LogError($"Fallback text read failed for {filePath}: {ex.Message}");
                            return null;
                        }
                }
            }catch(Exception ex)
            {
                logger.LogError($"Error extracting text from {filePath} using specific handler for {fileExtension}:{ex.Message}");
                return null;
            }
        }
    }
}
