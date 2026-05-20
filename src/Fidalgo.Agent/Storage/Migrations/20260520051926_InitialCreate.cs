using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidalgo.Agent.Storage.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    InternalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    Employer = table.Column<string>(type: "TEXT", nullable: false),
                    PostedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EmployerJobId = table.Column<string>(type: "TEXT", nullable: true),
                    SalaryRangeLow = table.Column<decimal>(type: "TEXT", nullable: true),
                    SalaryRangeHigh = table.Column<decimal>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Pros = table.Column<string>(type: "TEXT", nullable: false),
                    Cons = table.Column<string>(type: "TEXT", nullable: false),
                    ResumeHints = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Recommendation = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DateNotified = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceWebsite = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.InternalId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Email",
                table: "Jobs",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Email_EmployerJobId",
                table: "Jobs",
                columns: new[] { "Email", "EmployerJobId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
