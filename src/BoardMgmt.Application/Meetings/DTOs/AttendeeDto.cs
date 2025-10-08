// Application/Meetings/DTOs/AttendeeDto.cs
namespace BoardMgmt.Application.Meetings.DTOs;

public record AttendeeDto(
    Guid Id,
    string Name,
    string? Email,
    string? Role,
    string? UserId,
    string RowVersionBase64
);
