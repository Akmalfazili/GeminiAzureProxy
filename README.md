# Gemini AI File/Directory Processor (Local Azure Functions)

This project is a C# .NET Core Azure Functions application designed to act as a local proxy for the Google Gemini AI API. Its primary purpose is for **local development and testing**, 
allowing you to interact with the Gemini API and process **text content** from files within a specified local directory.

Supported text files are: txt, csv, json, xml, html, css, js, py, java, pdf, docx, xlsx & xls... or maybe others (you can try).

It includes basic session management to maintain conversation history during testing.

**⚠️ SECURITY WARNING ⚠️**
**This project is designed for LOCAL DEVELOPMENT AND TESTING ONLY.**

It accepts file and directory paths directly from incoming HTTP requests and reads content from your local filesystem based on those paths.
**This is a severe security vulnerability and should NEVER be deployed to a production environment or exposed to the public internet.**

## Features
*   Proxies text prompts to the Google Gemini API.
*   Reads content from files within a user-specified local directory.
  *   Supports text extraction from various file types:
    *   Plain Text (`.txt`, `.csv`, `.json`, `.xml`, `.html`, `.css`, `.js`, `.cs`, `.py`, etc.)
    *   PDF (`.pdf`) via PdfPig
    *   Microsoft Word (`.docx`) via NPOI
    *   Microsoft Excel (`.xlsx`, `.xls`) via NPOI
*   Maintains conversation history between turns by saving/loading JSON files locally based on a session ID.
*   Provides a simple HTTP-triggered endpoint for interaction.

## Prerequisites
*   **.NET Core SDK:** Version 6.0 or later.
*   **Azure Functions Core Tools:** For running Azure Functions locally. Install via npm: `npm install -g azure-functions-core-tools@4 --unsafe-perm true` (or follow official docs for your OS).
*   **A Google Gemini API Key:** Obtain one from Google AI Studio or Google Cloud Vertex AI.
*   **An IDE:** Visual Studio or VS Code (with Azure Functions extension).

## Setup and Installation  
1.  **Clone the repository:**
    ```bash
    git clone https://github.com/Akmalfazili/GeminiAzureProxy.git
    cd <project_directory> 
    ```
2.  **Restore NuGet packages:**
    The necessary packages (Google.AI.GenerativeAI, Newtonsoft.Json, Microsoft.Extensions.Configuration, PdfPig, NPOI) should be restored automatically when you build the project or run `dotnet restore`.
    ```bash
    dotnet restore
    ```

## Configuration

You need to configure your Google Gemini API Key and define the local directory for session history files.

1.  **Create `local.settings.json`:**
    *   There should be a `local.settings.json.example` file in the project root.
    *   Copy this file and rename the copy to `local.settings.json`.
    ```bash
    cp local.settings.json.example local.settings.json
    ```
    *   **Important:** `local.settings.json` is typically included in `.gitignore` and should **not** be committed to source control, as it contains sensitive keys.

2.  **Add your Gemini API Key:**
    *   Open `local.settings.json`.
    *   Locate the `"GeminiApiKey"` setting.
    *   Replace `"YOUR_GEMINI_API_KEY_HERE"` with your actual Google Gemini API key.

    ```json
    {
      "IsEncrypted": false,
      "Values": {
        "AzureWebJobsStorage": "", // Not strictly needed for HTTP trigger with local state
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",
        "GeminiApiKey": "YOUR_GEMINI_API_KEY_HERE", // <-- Paste your key here
      },
      "Host": { "CORS": "*" }
    }
    ```

3.  **Session History Directory:**
    *   The function is configured to save session history JSON files to a sub-directory named `sessionCache` within the function app's root directory.
    *   Create the `sessionCache` folder within the function app's root directory.

## Running Locally

1.  **Open your terminal** in the project directory (where the `.csproj` file is).
2.  **Start the Azure Functions host:**
    ```bash
    func start
    ```
3.  The host will compile your function and start listening for requests. You will see output indicating the local URL and port (typically `http://localhost:7071`) and the endpoint for your function.

    ```
    Functions:

            FileReader: [POST] [POST] http://localhost:7092/api/FileReader
    ```

## API Endpoint

The function exposes a single HTTP POST endpoint.

*   **URL:** `http://localhost:7071/api/GeminiProxyFunction` (Verify the exact URL and port from `func start` output)
*   **Method:** `POST`
*   **Content-Type:** `application/json`

**Request Body (`application/json`):**

```json
{
  "sessionId": "string | null",  // Optional: Provide an existing session ID to continue, leave null/empty for a new session.
  "directoryPath": "string | null", // Optional: Path to a local directory to process files from for this turn. Usually only needed for the first turn about a directory.
  "currentPrompt": "string"      // Required: The user's prompt for this turn. Can be empty if only loading history for a sessionId.
}
```
**Response Body (`application/json`):**
```json
{
  "sessionId": "string",                 // The current session ID (newly created or the one provided)
  "modelResponse": "string",             // The latest text response from the Gemini API
  "fullConversationHistory": [           // The complete conversation history up to the latest turn
    {
      "role": "string",                  // "user" or "model"
      "parts": [
        {
          "text": "string"               // Text content of the part (can be file content or prompt)
        }
      ]
    }
    // ... more ConversationTurn objects ...
  ]
}
```
