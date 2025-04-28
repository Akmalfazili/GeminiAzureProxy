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
using NPOI.OpenXmlFormats.Shared;
using GenerativeAI.Types;
using MathNet.Numerics.LinearAlgebra;
using Org.BouncyCastle.Crypto.Macs;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;
using Newtonsoft.Json;


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
            catch (System.Text.Json.JsonException ex)
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
        public async Task<IActionResult> RunFileReader([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ExecutionContext context)
        {
            _geminiService.GetWelcomeMessage();

            string sessionCacheLocation = _geminiService.GetSessionCache();
            if (string.IsNullOrEmpty(sessionCacheLocation))
            {
                _logger.LogError("Session cache directory is not configured");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            string geminiApiKey = _geminiService.GetApiKey();
            if (string.IsNullOrEmpty(geminiApiKey))
            {
                _logger.LogError("Gemini API Key is not configured");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            string data = await new StreamReader(req.Body).ReadToEndAsync();
            SessionRequest? requestBody = null;
            try
            {
                //requestBody = await req.ReadFromJsonAsync<SessionRequest>();
                requestBody = JsonConvert.DeserializeObject<SessionRequest>(data);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogError($"Error deserializing request body: {ex.Message}");
                return new BadRequestObjectResult("Please provide a valid JSON request body structure including sessionID, currentPrompt and directory Path (optional)");
            }

            if (requestBody == null || string.IsNullOrEmpty(requestBody.CurrentPrompt))
            {
                return new BadRequestObjectResult("Please provide current prompt in the request body");
            }

            // session management
            string? sessionId = requestBody.SessionId;
            List<ConversationTurn>? conversationHistory = new List<ConversationTurn>();
            string? cachePath = null;

            if (string.IsNullOrEmpty(sessionId))
            {
                //new session
                sessionId = Guid.NewGuid().ToString();
                _logger.LogInformation($"Starting new session with ID: {sessionId}");
            }
            else
            {
                //existing session, load history
                cachePath = Path.Combine(sessionCacheLocation, $"{sessionId}.json");

                if (File.Exists(cachePath))
                {
                    try
                    {
                        string cacheJson = await File.ReadAllTextAsync(cachePath);
                        conversationHistory = JsonConvert.DeserializeObject<List<ConversationTurn>>(cacheJson);
                        _logger.LogInformation($"Loaded conversation history for session {sessionId} with {conversationHistory?.Count} turns");
                    }
                    catch (Exception ex) 
                    {
                        _logger.LogError($"Error loading history for session {sessionId}: {ex.Message}");
                        return new InternalServerErrorResult($"Could not load history for session {sessionId}: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogWarning($"Session ID {sessionId} but cache file not found at {cachePath}");
                    return new NotFoundObjectResult($"Session ID {sessionId} not found or cache expired.");
                }

            }

            cachePath = Path.Combine(sessionCacheLocation, $"{sessionId}.json");

            string? directoryPath = requestBody.DirectoryPath;
            //string prompt = requestBody.Prompt;
            string? combinedFileContent = null;

            if (!string.IsNullOrEmpty(directoryPath))
            {
                _logger.LogInformation($"Attempting to read files in: '{directoryPath}'");

                try
                {
                    if (!Directory.Exists(directoryPath))
                    {
                        _logger.LogError($"Directory not found: {directoryPath}");
                        return new NotFoundObjectResult($"Directory not found: {directoryPath}");
                    }
                    //Get list of files in directory (excluding subdirectories)
                    string[] files = Directory.GetFiles(directoryPath);
                    if(files.Length == 0)
                    {
                        _logger.LogWarning($"No files found in directory: {directoryPath}");
                        combinedFileContent = $"[Note: No files found in directory '{Path.GetFileName(directoryPath)}']\n\n";
                    }
                    else
                    {
                        _logger.LogInformation($"Found {files.Length} files in '{directoryPath}'. Attempting to extract text.");
                        StringBuilder contentAccumulator = new StringBuilder();
                        List<string> skippedFiles = new List<string>();

                        foreach(string file in files)
                        {
                            try
                            {
                                string fileContent = await ExtractTextFromFileAsync(file, _logger);
                                if (fileContent != null)
                                {
                                    contentAccumulator.Append($"--- Start File: {Path.GetFileName(file)} ---\n");
                                    contentAccumulator.Append(fileContent);
                                    contentAccumulator.Append($"\n--- End File: {Path.GetFileName(file)} ---\n\n"); // Add newline after each file block
                                }
                                else
                                {
                                    skippedFiles.Add(Path.GetFileName(file));
                                }
                            }catch(Exception ex)
                            {
                                _logger.LogError($"Error processing single file '{file}': {ex.Message}");
                                skippedFiles.Add(Path.GetFileName(file));
                            }
                        }
                        if(contentAccumulator.Length > 0)
                        {
                            combinedFileContent = contentAccumulator.ToString();
                            _logger.LogInformation($"Successfully extracted content from {files.Length - skippedFiles.Count} files. Combined length: {combinedFileContent.Length}");
                        }else if(skippedFiles.Count == files.Length)
                        {
                            _logger.LogWarning($"Could not extract text from ANY of the {files.Length} files in '{directoryPath}'.");
                            return new BadRequestObjectResult($"Could not extract text from any files in directory '{Path.GetFileName(directoryPath)}'. Supported formats: txt, pdf, docx, xls, xlsx...etc or is a readable text");
                        }

                        if (skippedFiles.Any())
                        {
                            if (combinedFileContent == null)
                            {
                                combinedFileContent = "";
                                combinedFileContent = $"[Note: Skipped {skippedFiles.Count} file(s) due to errors or unsupported format: {string.Join(", ", skippedFiles)}]\n\n" + combinedFileContent;
                                _logger.LogWarning($"Skipped {skippedFiles.Count} files: {string.Join(", ", skippedFiles)}");
                            }else if (contentAccumulator.Length > 0)
                            {
                                _logger.LogInformation($"Processed all {files.Length} files successfully from '{directoryPath}'.");
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError($"Permission denied to read file: {directoryPath}. Error: {ex.Message}");
                    return new UnauthorizedResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"An error occured while reading the file {directoryPath}. Error: {ex.Message}");
                    return new InternalServerErrorResult($"An error occured reading the file: {ex.Message}");
                }
            }

            List<Content> apiContents = new List<Content>();

            foreach (var turn in conversationHistory)
            {
                string? role = turn.Role?.ToLowerInvariant();
                if (role != "user" && role != "model")
                {
                    _logger.LogWarning($"Skipping chat history turn with invalid role: {turn.Role}");
                    continue;
                }
                var content = new Content { Role = role };
                foreach (var part in turn.Parts)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        content.Parts.Add(new Part { Text = part.Text });
                    }
                }

                if (content.Parts.Any())
                {
                    apiContents.Add(content);
                }
            }

            var currentUserContent = new Content { Role = "user" };
            if (!string.IsNullOrEmpty(combinedFileContent))
            {
                currentUserContent.Parts.Add(new Part { Text = $"Regarding the content of the files in the directory '{Path.GetFileName(directoryPath)}': \n\n---\n{combinedFileContent}\n---\n\n" });

            }
            currentUserContent.Parts.Add(new Part { Text = requestBody.CurrentPrompt });

            if (!currentUserContent.Parts.Any())
            {
                return new BadRequestObjectResult("Current prompt cannot be empty");
            }

            apiContents.Add(currentUserContent);

            string? generatedText = null;

            try
            {
                var googleAi = new GoogleAi(geminiApiKey);
                var googleModel = googleAi.CreateGeminiModel("models/gemini-2.0-flash");
                //string fullPrompt = $"Regarding the content of the file {Path.GetFileName(filePath)}: \n\n---\n{fileContent}\n---\n\n{prompt}";
                //var googleResponse = await googleModel.GenerateContentAsync(fullPrompt);
                var googleResponse = await googleModel.GenerateContentAsync(new GenerateContentRequest { Contents = apiContents });
                //generatedText = googleResponse?.Candidates?[0]?.Content?.Parts?[0]?.Text;
                generatedText = googleResponse?.Candidates?[0]?.Content?.Parts?.FirstOrDefault()?.Text;

                if (string.IsNullOrEmpty(generatedText))
                {
                    _logger.LogWarning($"Gemini API returned an empy response for the session {sessionId}");
                    //return new OkObjectResult(new { message = $"No content generated by Gemini regarding {Path.GetFileName(filePath)}" });
                    generatedText = "[No response generated by model]";
                }

                _logger.LogInformation($"Successfully generated text from Gemini for session {sessionId}");
                //return new OkObjectResult(new { generatedText = generatedText });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occured calling Gemini API for the session {sessionId}:{ex.Message}");

                if( ex.InnerException != null)
                {
                    _logger.LogError($"Inner Exception: {ex.InnerException}");
                }

                return new InternalServerErrorResult($"An error occured while calling the Gemini API: {ex.Message}");
            }

            //update and save history
            try
            {
                conversationHistory.Add(new ConversationTurn
                {
                    Role = "user",
                    Parts = currentUserContent.Parts.Select(p => new ConversationPart { Text = p.Text }).ToList()
                });
                conversationHistory.Add(new ConversationTurn
                {
                    Role = "model",
                    Parts = new List<ConversationPart> { new ConversationPart { Text = generatedText } }
                });

                //serialize and save the upated history
                string updatedHistoryJson = JsonConvert.SerializeObject(conversationHistory, Formatting.Indented);
                await File.WriteAllTextAsync(cachePath, updatedHistoryJson);
                _logger.LogInformation($"Saved updated history for session {sessionId} to {cachePath}. Total turns: {conversationHistory.Count}");
            }catch(Exception ex)
            {
                _logger.LogError($"Error saving history for session {sessionId} to {cachePath}: {ex.Message}");
            }

            var responseModel = new SessionResponse
            {
                SessionId = sessionId,
                ModelResponse = generatedText
            };
            return new OkObjectResult(responseModel);

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


                                    int maxColumn = 0;
                                    int maxRow = sheet.LastRowNum;
                                    for (int rowIndex= 0; rowIndex <= maxRow; rowIndex++)
                                    {
                                        if (sheet.GetRow(rowIndex).LastCellNum > maxColumn)
                                        {
                                            maxColumn= sheet.GetRow(rowIndex).LastCellNum;
                                        }
                                    }

                                    for(int rowIndex=0;rowIndex<=maxRow; rowIndex++)
                                    {
                                        IRow row= sheet.GetRow(rowIndex);
                                        for(int cellIndex=0;cellIndex < maxColumn; cellIndex++)
                                        {
                                            if(cellIndex > 0)
                                            {
                                                textBuilder.Append("\t");
                                            }
                                            NPOI.SS.UserModel.ICell? cell = row?.GetCell(cellIndex,MissingCellPolicy.CREATE_NULL_AS_BLANK);
                                            textBuilder.Append(cell?.ToString()??" ");
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
