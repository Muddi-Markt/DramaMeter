using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muddi.DramaMeter.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddClickViewBoxPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ClickViewBoxX",
                table: "Votes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ClickViewBoxY",
                table: "Votes",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClickViewBoxX",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "ClickViewBoxY",
                table: "Votes");
        }
    }
}
