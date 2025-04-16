using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GeminiAzureProxy.Models
{
    public class GeminiApiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }

        [JsonPropertyName("promptFeedback")]
        public PromptFeedback? PromptFeedback { get; set; }

        [JsonPropertyName("error")]
        public GeminiApiError? Error { get; set; }
    }

    public class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string? FinishReason { get; set; }

        [JsonPropertyName("index")]
        public int? Index { get; set; }

        [JsonPropertyName("safetyRatings")]
        public List<SafetyRating>? SafetyRatings { get; set; }
    }

    public class SafetyRating
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("probability")]
        public string? Probability { get; set; }
    }

    public class PromptFeedback
    {
        [JsonPropertyName("safetyratings")]
        public List<SafetyRating>? SafetyRating { get; set; }

        [JsonPropertyName("blockReason")]
        public string? BlockReason { get; set; }
    }

    public class GeminiApiError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

    }
}
