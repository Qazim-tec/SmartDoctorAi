namespace HealthTech.Models
{
    public class LifestyleHistory
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string LifestyleInputJson { get; set; }
        public string LifestyleResponseJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}