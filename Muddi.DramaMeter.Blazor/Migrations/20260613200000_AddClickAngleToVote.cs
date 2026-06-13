using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Muddi.DramaMeter.Blazor.Migrations
{
    /// <inheritdoc />
    public partial class AddClickAngleToVote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ClickAngle",
                table: "Votes",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClickAngle",
                table: "Votes");
        }
    }
}
