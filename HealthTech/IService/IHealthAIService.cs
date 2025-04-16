using HealthTech.DTOs;
using HealthTech.Models;

namespace HealthTech.IService
{
    public interface IHealthAIService
    {
        Task<DiagnosisResponseDto> GetDiagnosesAsync(PatientInput input, string userId);
    }
}
