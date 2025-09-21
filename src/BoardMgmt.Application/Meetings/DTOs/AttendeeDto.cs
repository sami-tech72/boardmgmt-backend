// Application/Meetings/DTOs/AttendeeDto.cs
namespace BoardMgmt.Application.Meetings.DTOs;

public sealed record AttendeeDto(
    Guid Id,
    string Name,
    string? Email,
    string? Role,
    string? UserId, // <-- identity user id (nullable for external/non-user attendees)
    string RowVersionBase64
);
