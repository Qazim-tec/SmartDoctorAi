using HealthTech.Data;
using HealthTech.DTOs;
using HealthTech.IService;
using HealthTech.Models;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthTech.Service
{
    public class HealthAIService : IHealthAIService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthAIService> _logger;
        private readonly ApplicationDbContext _context;
        private readonly JsonSerializerOptions _jsonOptions;

        public HealthAIService(IConfiguration configuration, ILogger<HealthAIService> logger, ApplicationDbContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;

            // Configure JSON serialization options
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<DiagnosisResponseDto> GetDiagnosesAsync(PatientInput input, string userId)
        {
            RestResponse response = null;
            try
            {
                // Validate configuration
                string apiKey = _configuration["GeminiAI:ApiKey"];
                string apiUrl = _configuration["GeminiAI:ApiUrl"];

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl))
                {
                    _logger.LogError("Gemini AI configuration missing");
                    throw new Exception("AI service configuration is missing");
                }

                // Create and configure the API client with RestClientOptions
                var options = new RestClientOptions($"{apiUrl}?key={apiKey}")
                {
                    MaxTimeout = 30000 // 30 seconds timeout (in milliseconds)
                };

                var client = new RestClient(options);

                var request = new RestRequest("", Method.Post);
                request.AddHeader("Content-Type", "application/json");

                // Build the prompt with explicit JSON format instructions
                string prompt = $@"You are a professional medical AI. Based on the following patient data:
Presenting Complaint: {input.PresentingComplaint}
Associated Symptoms: {input.AssociatedSymptoms}
Onset: {input.Onset}
Duration: {input.Duration}
Additional Information: {input.AdditionalInformation}
Other Medical or Dental Information: {input.OtherMedicalOrDentalInformation}

Provide 6 differential diagnoses with:
1. A brief explanation for each
2. A possible treatment plan for each diagnosis

Return ONLY the following JSON format (no Markdown, no additional text):
{{
  ""diagnoses"": [
    {{
      ""name"": ""Diagnosis Name"",
      ""explanation"": ""Brief explanation"",
      ""treatmentPlan"": ""Treatment plan details""
    }}
  ]
}}";

                var body = new
                {
                    contents = new[]
                    {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topP = 0.9,
                        maxOutputTokens = 2000
                    }
                };

                request.AddJsonBody(body);

                // Execute the API call
                response = await client.ExecuteAsync(request);

                if (!response.IsSuccessful)
                {
                    _logger.LogError("Gemini API call failed. Status: {StatusCode}, Content: {Content}",
                        response.StatusCode, response.Content);
                    throw new Exception($"API Error: {response.StatusCode} - {response.Content}");
                }

                _logger.LogDebug("Raw API response: {Response}", response.Content);

                // Parse the outer response structure
                var geminiResponse = JsonSerializer.Deserialize<GeminiApiResponse>(response.Content, _jsonOptions);

                if (geminiResponse?.Candidates == null || geminiResponse.Candidates.Length == 0)
                {
                    _logger.LogError("No candidates in response. Full response: {Response}", response.Content);
                    throw new Exception("AI service returned no candidates");
                }

                // Extract and clean the JSON response
                string rawText = geminiResponse.Candidates[0].Content.Parts[0].Text;
                string jsonText = rawText
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                _logger.LogDebug("Cleaned JSON text: {JsonText}", jsonText);

                // Parse the cleaned JSON into diagnoses
                var diagnosisResponse = JsonSerializer.Deserialize<DiagnosisResponseDto>(jsonText, _jsonOptions);

                if (diagnosisResponse?.Diagnoses == null || diagnosisResponse.Diagnoses.Length == 0)
                {
                    _logger.LogError("Failed to parse diagnoses from: {JsonText}", jsonText);
                    throw new Exception("AI returned no valid diagnoses");
                }

                // Save to database
                var history = new DiagnosisHistory
                {
                    UserId = userId,
                    PatientInputJson = JsonSerializer.Serialize(input, _jsonOptions),
                    DiagnosisResponseJson = jsonText,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.DiagnosisHistories.AddAsync(history);
                await _context.SaveChangesAsync();

                return diagnosisResponse;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing failed. Response: {Response}", response?.Content);
                throw new Exception("Failed to process AI response: invalid data format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing diagnosis request");
                throw;
            }
        }
    }
}