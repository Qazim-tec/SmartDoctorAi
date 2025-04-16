using HealthTech.Models;

namespace HealthTech.DTOs
{
    public class ChatInput
    {
        public string Message { get; set; }
        public string? ConversationId { get; set; }
        public string UserId { get; set; }
    }

    public class ChatResponseDto
    {
        public string Message { get; set; }
        public string ConversationId { get; set; }
        public ChatDiagnosisResultDto Diagnosis { get; set; }
        public bool IsComplete { get; set; }
    }

    public class ChatDiagnosisResultDto
    {
        public string Summary { get; set; }
        public Diagnosis[] Diagnoses { get; set; }
    }
}