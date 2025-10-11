using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardMgmt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalOnlineMeetingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalOnlineMeetingId",
                table: "Meetings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_ExternalOnlineMeetingId",
                table: "Meetings",
                column: "ExternalOnlineMeetingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Meetings_ExternalOnlineMeetingId",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "ExternalOnlineMeetingId",
                table: "Meetings");
        }
    }
}
