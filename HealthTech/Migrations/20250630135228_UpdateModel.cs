using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTech.Migrations
{
    /// <inheritdoc />
    public partial class UpdateModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizScores_QuizCategories_CategoryId",
                table: "QuizScores");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizScores_QuizCategories_CategoryId",
                table: "QuizScores",
                column: "CategoryId",
                principalTable: "QuizCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuizScores_QuizCategories_CategoryId",
                table: "QuizScores");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizScores_QuizCategories_CategoryId",
                table: "QuizScores",
                column: "CategoryId",
                principalTable: "QuizCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
