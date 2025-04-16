using HealthTech.DTOs;

namespace HealthTech.IService
{
    public interface IChatService
    {
        Task<ChatResponseDto> ChatWithDoctorAsync(ChatInput input, string conversationId = null);
    }
}