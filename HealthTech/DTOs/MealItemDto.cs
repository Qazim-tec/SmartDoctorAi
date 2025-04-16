namespace HealthTech.DTOs
{
    public class MealItemDto
    {
        public string Description { get; set; }
        public string Quantity { get; set; } // e.g., "200g", "1 cup"
        public int Calories { get; set; } // In kilocalories (kcal)
        public string CalorieReason { get; set; } // Why calories support goal
    }

    public class DailyMealPlanDto
    {
        public string Day { get; set; }
        public MealItemDto Breakfast { get; set; }
        public MealItemDto Lunch { get; set; }
        public MealItemDto Dinner { get; set; }
        public MealItemDto Snacks { get; set; }
    }

    public class LifestyleRecommendationDto
    {
        public DailyMealPlanDto[] WeeklyMealPlan { get; set; } // 7 days
        public string ExerciseRoutine { get; set; }
        public string LifestyleAdvice { get; set; } // Sleep, stress, hydration
    }
}
