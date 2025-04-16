using HealthTech.DTOs;
using HealthTech.IService;
using RestSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthTech.Models;

namespace HealthTech.Service
{
    public class ChatService : IChatService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatService> _logger;
        private readonly IHealthAIService _healthAIService;
        private readonly JsonSerializerOptions _jsonOptions;

        public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IHealthAIService healthAIService)
        {
            _configuration = configuration;
            _logger = logger;
            _healthAIService = healthAIService;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<ChatResponseDto> ChatWithDoctorAsync(ChatInput input, string conversationId = null)
        {
            try
            {
                // Validate input
                if (input == null || string.IsNullOrEmpty(input.UserId))
                {
                    throw new ArgumentException("UserId is required.");
                }

                // Initialize or restore conversation state
                ConversationState state;
                if (string.IsNullOrEmpty(conversationId))
                {
                    state = new ConversationState
                    {
                        Messages = new List<ChatMessage>(),
                        PatientInput = new PatientInput(),
                        AssistantQuestionCount = 0,
                        AskedSystemicReview = false,
                        AskedFamilyHistory = false,
                        AskedPastMedicalHistory = false,
                        AskedAdditionalInfo = false
                    };
                }
                else
                {
                    try
                    {
                        state = JsonSerializer.Deserialize<ConversationState>(conversationId, _jsonOptions);
                        if (state == null || state.Messages == null || state.PatientInput == null)
                        {
                            _logger.LogWarning("Invalid conversationId format; starting new conversation.");
                            state = NewConversationState();
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize conversationId: {ConversationId}", conversationId);
                        state = NewConversationState();
                    }
                }

                // Add user input to conversation
                if (!string.IsNullOrEmpty(input.Message))
                {
                    state.Messages.Add(new ChatMessage { Role = "user", Content = input.Message });
                    UpdatePatientInput(state, input.Message);
                }

                // Configure API client
                string apiKey = _configuration["GeminiAI:ApiKey"];
                string apiUrl = _configuration["GeminiAI:ApiUrl"];

                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl))
                {
                    _logger.LogError("Gemini AI configuration missing");
                    throw new Exception("AI service configuration is missing");
                }

                var options = new RestClientOptions($"{apiUrl}?key={apiKey}")
                {
                    MaxTimeout = 30000
                };
                var client = new RestClient(options);

                // Start conversation if empty
                if (state.Messages.Count == 0)
                {
                    var initialQuestion = "Please tell me your age and biological sex to get started.";
                    state.Messages.Add(new ChatMessage { Role = "assistant", Content = initialQuestion });
                    state.AssistantQuestionCount++;

                    return new ChatResponseDto
                    {
                        Message = initialQuestion,
                        ConversationId = JsonSerializer.Serialize(state, _jsonOptions),
                        IsComplete = false
                    };
                }

                // Check for mandatory questions towards the end
                if (!state.AskedPastMedicalHistory && state.AssistantQuestionCount >= 15)
                {
                    var pastMedicalHistoryQuestion = "Now, let's discuss your past medical history. What is your genotype, Do you have any history of major illnesses, injuries, previous surgeries, psychiatric problems, hospitalizations, or drug allergies. Any history of hypertension, diabetes, asthma, pepetic ulcer, epilepsy?";
                    state.Messages.Add(new ChatMessage { Role = "assistant", Content = pastMedicalHistoryQuestion });
                    state.AssistantQuestionCount++;
                    state.AskedPastMedicalHistory = true;

                    return new ChatResponseDto
                    {
                        Message = pastMedicalHistoryQuestion,
                        ConversationId = JsonSerializer.Serialize(state, _jsonOptions),
                        IsComplete = false
                    };
                }


                if (!state.AskedSystemicReview && state.AssistantQuestionCount >= 17)
                {
                    var systemicReviewQuestion = "To complete our assessment, let's perform a brief review of systems. Do you have any issues with your vision, hearing, breathing, digestion, urination,  or any other systems in your body?";
                    state.Messages.Add(new ChatMessage { Role = "assistant", Content = systemicReviewQuestion });
                    state.AssistantQuestionCount++;
                    state.AskedSystemicReview = true;

                    return new ChatResponseDto
                    {
                        Message = systemicReviewQuestion,
                        ConversationId = JsonSerializer.Serialize(state, _jsonOptions),
                        IsComplete = false
                    };
                }

                if (!state.AskedAdditionalInfo && state.AssistantQuestionCount >= 18)
                {
                    var additionalInfoQuestion = "Is there any additional medical information you'd like to share before I provide an assessment?";
                    state.Messages.Add(new ChatMessage { Role = "assistant", Content = additionalInfoQuestion });
                    state.AssistantQuestionCount++;
                    state.AskedAdditionalInfo = true;

                    return new ChatResponseDto
                    {
                        Message = additionalInfoQuestion,
                        ConversationId = JsonSerializer.Serialize(state, _jsonOptions),
                        IsComplete = false
                    };
                }

                // Check if maximum questions have been asked
                if (state.AssistantQuestionCount >= 20)
                {
                    var summary = await GenerateSummaryAsync(state, client);
                    var diagnosis = await _healthAIService.GetDiagnosesAsync(state.PatientInput, input.UserId);

                    return new ChatResponseDto
                    {
                        Message = "Here is my assessment based on your symptoms.",
                        Diagnosis = new ChatDiagnosisResultDto
                        {
                            Summary = summary,
                            Diagnoses = diagnosis.Diagnoses
                        },
                        IsComplete = true
                    };
                }

                // Generate the most relevant question
                var questionPrompt = $@"You are a professional medical AI acting as a doctor. Based on the following conversation:
{JsonSerializer.Serialize(state.Messages, _jsonOptions)}

Ask the single most relevant and critical follow-up question to clarify the patient's issue and gather essential information for a differential diagnosis. The question should be empathetic, professional, and highly specific to the patient's reported symptoms or context. If no specific symptoms are provided, prioritize questions about primary complaints or general health. Return ONLY the question as plain text.

Examples of relevant questions:
- For a headache: 'Is the headache on one side or both sides of your head?'
- For chest pain: 'Does the chest pain feel sharp, dull, or like pressure?'
- For vague input: 'What specific symptoms are you experiencing right now?'";

                var questionRequest = new RestRequest("", Method.Post);
                questionRequest.AddHeader("Content-Type", "application/json");
                questionRequest.AddJsonBody(new
                {
                    contents = new[] { new { role = "user", parts = new[] { new { text = questionPrompt } } } },
                    generationConfig = new { temperature = 0.7, topP = 0.9, maxOutputTokens = 100 }
                });

                var questionResponse = await client.ExecuteAsync(questionRequest);
                if (!questionResponse.IsSuccessful)
                {
                    _logger.LogError("Gemini API call failed. Status: {StatusCode}, Content: {Content}",
                        questionResponse.StatusCode, questionResponse.Content);
                    throw new Exception($"API Error: {questionResponse.StatusCode} - {questionResponse.Content}");
                }

                var questionResult = JsonSerializer.Deserialize<GeminiApiResponse>(questionResponse.Content, _jsonOptions);
                if (questionResult?.Candidates == null || questionResult.Candidates.Length == 0)
                {
                    _logger.LogError("No candidates in question response: {Content}", questionResponse.Content);
                    throw new Exception("AI service returned no candidates");
                }

                var nextQuestion = questionResult.Candidates[0].Content.Parts[0].Text.Trim();
                state.Messages.Add(new ChatMessage { Role = "assistant", Content = nextQuestion });
                state.AssistantQuestionCount++;

                return new ChatResponseDto
                {
                    Message = nextQuestion,
                    ConversationId = JsonSerializer.Serialize(state, _jsonOptions),
                    IsComplete = false
                };
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Invalid input: {Message}", ex.Message);
                throw;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing failed");
                throw new Exception("Failed to process AI response: invalid data format");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request");
                throw new Exception($"Error processing chat: {ex.Message}");
            }
        }

        private ConversationState NewConversationState()
        {
            return new ConversationState
            {
                Messages = new List<ChatMessage>(),
                PatientInput = new PatientInput(),
                AssistantQuestionCount = 0,
                AskedSystemicReview = false,
                AskedFamilyHistory = false,
                AskedPastMedicalHistory = false,
                AskedAdditionalInfo = false
            };
        }

        private async Task<string> GenerateSummaryAsync(ConversationState state, RestClient client)
        {
            var summaryPrompt = $@"You are a professional medical AI acting as a doctor. Based on the following conversation:
{JsonSerializer.Serialize(state.Messages, _jsonOptions)}

Summarize the patient's complaints in a concise paragraph. If no specific symptoms were provided, note the lack of detailed information. Return ONLY the summary as plain text.";

            var summaryRequest = new RestRequest("", Method.Post);
            summaryRequest.AddHeader("Content-Type", "application/json");
            summaryRequest.AddJsonBody(new
            {
                contents = new[] { new { role = "user", parts = new[] { new { text = summaryPrompt } } } },
                generationConfig = new { temperature = 0.7, topP = 0.9, maxOutputTokens = 200 }
            });

            var summaryResponse = await client.ExecuteAsync(summaryRequest);
            if (!summaryResponse.IsSuccessful)
            {
                _logger.LogError("Gemini API call failed. Status: {StatusCode}, Content: {Content}",
                    summaryResponse.StatusCode, summaryResponse.Content);
                throw new Exception($"API Error: {summaryResponse.StatusCode} - {summaryResponse.Content}");
            }

            var summaryResult = JsonSerializer.Deserialize<GeminiApiResponse>(summaryResponse.Content, _jsonOptions);
            if (summaryResult?.Candidates == null || summaryResult.Candidates.Length == 0)
            {
                _logger.LogError("No candidates in summary response: {Content}", summaryResponse.Content);
                throw new Exception("AI service returned no candidates");
            }

            return summaryResult.Candidates[0].Content.Parts[0].Text.Trim();
        }

        private void UpdatePatientInput(ConversationState state, string userMessage)
        {
            if (string.IsNullOrEmpty(userMessage) || userMessage.Trim().ToLower() == "hello")
            {
                return;
            }

            var messages = string.Join("\n", state.Messages.Select(m => $"{m.Role}: {m.Content}"));
            if (state.Messages.Any(m => m.Content.ToLower().Contains("headache")))
            {
                if (string.IsNullOrEmpty(state.PatientInput.PresentingComplaint))
                {
                    state.PatientInput.PresentingComplaint = "Headache";
                }
                if (userMessage.ToLower().Contains("throbbing"))
                {
                    state.PatientInput.AssociatedSymptoms = "Throbbing quality";
                }
                if (userMessage.ToLower().Contains("day") || userMessage.ToLower().Contains("days"))
                {
                    state.PatientInput.Duration = userMessage;
                }
                if (userMessage.ToLower().Contains("side") || userMessage.ToLower().Contains("left") || userMessage.ToLower().Contains("right"))
                {
                    state.PatientInput.AdditionalInformation = $"Location: {userMessage}";
                }
                if (userMessage.ToLower().Contains("sudden") || userMessage.ToLower().Contains("gradual"))
                {
                    state.PatientInput.Onset = userMessage;
                }
            }
            else if (!string.IsNullOrEmpty(userMessage) && string.IsNullOrEmpty(state.PatientInput.PresentingComplaint))
            {
                state.PatientInput.PresentingComplaint = userMessage; // Capture initial symptom
            }
            state.PatientInput.OtherMedicalOrDentalInformation = messages;
        }
    }

    public class ConversationState
    {
        public List<ChatMessage> Messages { get; set; }
        public PatientInput PatientInput { get; set; }
        public int AssistantQuestionCount { get; set; }
        public bool AskedSystemicReview { get; set; }
        public bool AskedFamilyHistory { get; set; }
        public bool AskedPastMedicalHistory { get; set; }
        public bool AskedAdditionalInfo { get; set; }
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }
}