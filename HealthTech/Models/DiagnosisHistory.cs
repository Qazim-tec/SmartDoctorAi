namespace HealthTech.Models
{
    public class DiagnosisHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string PatientInputJson { get; set; }
        public string DiagnosisResponseJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
