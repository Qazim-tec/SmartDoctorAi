namespace HealthTech.DTOs
{
    public class LifestyleInputDto
    {
        public string AgeGroup { get; set; } // e.g., "20-40"
        public double HeightCm { get; set; }
        public double WeightKg { get; set; }
        public string Location { get; set; } // e.g., "Lagos"
        public string HealthGoal { get; set; } // e.g., "Lose weight"
        public string DietaryRestrictions { get; set; } // e.g., "Nuts, Vegetarian"
        public string MedicalConditions { get; set; } // e.g., "Diabetes" or "None"
    }
}