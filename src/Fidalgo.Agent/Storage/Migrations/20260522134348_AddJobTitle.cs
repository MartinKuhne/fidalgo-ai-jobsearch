using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fidalgo.Agent.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddJobTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Jobs",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "Jobs");
        }
    }
}
