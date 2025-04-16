namespace HealthTech.Models
{
    public class PatientInput
    {
        public string PresentingComplaint { get; set; }
        public string AssociatedSymptoms { get; set; }
        public string Onset { get; set; }
        public string Duration { get; set; }
        public string AdditionalInformation { get; set; }
        public string OtherMedicalOrDentalInformation { get; set; }
    }
}
