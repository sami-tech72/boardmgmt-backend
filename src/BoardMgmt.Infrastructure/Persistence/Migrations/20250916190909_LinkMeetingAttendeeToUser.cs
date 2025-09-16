using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardMgmt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkMeetingAttendeeToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "MeetingAttendees",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "MeetingAttendees",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendees_MeetingId_UserId",
                table: "MeetingAttendees",
                columns: new[] { "MeetingId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendees_UserId",
                table: "MeetingAttendees",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingAttendees_AspNetUsers_UserId",
                table: "MeetingAttendees",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingAttendees_AspNetUsers_UserId",
                table: "MeetingAttendees");

            migrationBuilder.DropIndex(
                name: "IX_MeetingAttendees_MeetingId_UserId",
                table: "MeetingAttendees");

            migrationBuilder.DropIndex(
                name: "IX_MeetingAttendees_UserId",
                table: "MeetingAttendees");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "MeetingAttendees");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "MeetingAttendees");
        }
    }
}
