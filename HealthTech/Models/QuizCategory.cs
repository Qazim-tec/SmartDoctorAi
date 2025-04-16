namespace HealthTech.Models
{
    public class QuizCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsMedical { get; set; }
    }

    public class QuizQuestion
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
        public string CorrectOption { get; set; }
    }

    public class QuizScore
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int CategoryId { get; set; }
        public DateTime TakenAt { get; set; }
        public int Score { get; set; }
        public QuizCategory Category { get; set; }
    }

    public class QuizSession
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int CategoryId { get; set; }
        public List<QuizQuestion> Questions { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}