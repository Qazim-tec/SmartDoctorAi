using HealthTech.Models;

namespace HealthTech.DTOs
{
    public class QuizCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsMedical { get; set; }
    }

    public class QuizQuestionDto
    {
        public int Id { get; set; }
        public string Text { get; set; }
        public string OptionA { get; set; }
        public string OptionB { get; set; }
        public string OptionC { get; set; }
        public string OptionD { get; set; }
    }

    public class QuizSubmissionDto
    {
        public int CategoryId { get; set; }
        public Dictionary<int, char> Answers { get; set; } // e.g., {1: 'A', 2: 'C'}
    }

    public class QuizResultDto
    {
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public List<MissedQuestionDto> MissedQuestions { get; set; }
    }

    public class MissedQuestionDto
    {
        public string Text { get; set; }
        public string UserAnswer { get; set; }
        public string CorrectAnswer { get; set; }
    }

    public class QuizScoreDto
    {
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public DateTime TakenAt { get; set; }
        public int Score { get; set; }
    }
}