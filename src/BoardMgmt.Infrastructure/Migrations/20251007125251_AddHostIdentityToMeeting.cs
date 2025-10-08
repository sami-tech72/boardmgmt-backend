using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardMgmt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHostIdentityToMeeting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HostIdentity",
                table: "Meetings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HostIdentity",
                table: "Meetings");
        }
    }
}
