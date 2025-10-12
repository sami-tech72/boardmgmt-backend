using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardMgmt.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMeetingTranscriptCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingAttendees_Meetings_MeetingId",
                table: "MeetingAttendees");

            migrationBuilder.DropForeignKey(
                name: "FK_Transcripts_Meetings_MeetingId",
                table: "Transcripts");

            migrationBuilder.DropForeignKey(
                name: "FK_TranscriptUtterances_Transcripts_TranscriptId",
                table: "TranscriptUtterances");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingAttendees_Meetings_MeetingId",
                table: "MeetingAttendees",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Transcripts_Meetings_MeetingId",
                table: "Transcripts",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TranscriptUtterances_Transcripts_TranscriptId",
                table: "TranscriptUtterances",
                column: "TranscriptId",
                principalTable: "Transcripts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingAttendees_Meetings_MeetingId",
                table: "MeetingAttendees");

            migrationBuilder.DropForeignKey(
                name: "FK_Transcripts_Meetings_MeetingId",
                table: "Transcripts");

            migrationBuilder.DropForeignKey(
                name: "FK_TranscriptUtterances_Transcripts_TranscriptId",
                table: "TranscriptUtterances");

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingAttendees_Meetings_MeetingId",
                table: "MeetingAttendees",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Transcripts_Meetings_MeetingId",
                table: "Transcripts",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TranscriptUtterances_Transcripts_TranscriptId",
                table: "TranscriptUtterances",
                column: "TranscriptId",
                principalTable: "Transcripts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
