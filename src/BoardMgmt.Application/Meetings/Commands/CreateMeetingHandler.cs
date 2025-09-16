using BoardMgmt.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Meetings.Commands;

public class CreateMeetingValidator : AbstractValidator<CreateMeetingCommand>
{
    public CreateMeetingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
        RuleFor(x => x.EndAt)
            .Must((cmd, end) => end == null || end > cmd.ScheduledAt)
            .WithMessage("End time must be after start time");
    }
}

public class CreateMeetingHandler : IRequestHandler<CreateMeetingCommand, Guid>
{
    private readonly DbContext _db;
    public CreateMeetingHandler(DbContext db) => _db = db;

    public async Task<Guid> Handle(CreateMeetingCommand request, CancellationToken ct)
    {
        MeetingType? MapType(string? t) => t?.ToLowerInvariant() switch
        {
            "board" => MeetingType.Board,
            "committee" => MeetingType.Committee,
            "emergency" => MeetingType.Emergency,
            _ => null
        };

        var entity = new Meeting
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description!.Trim(),
            Type = request.Type,
            ScheduledAt = request.ScheduledAt,
            EndAt = request.EndAt,
            Location = string.IsNullOrWhiteSpace(request.Location) ? "TBD" : request.Location.Trim(),
            Status = MeetingStatus.Scheduled
        };

        if (request.Attendees is { Count: > 0 })
        {
            foreach (var raw in request.Attendees.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var full = raw.Trim();
                string name = full;
                string? role = null;
                var open = full.IndexOf('(');
                var close = full.IndexOf(')');
                if (open > 0 && close > open)
                {
                    name = full[..open].Trim();
                    role = full[(open + 1)..close].Trim();
                }
                entity.Attendees.Add(new MeetingAttendee { Name = name, Role = role });
            }
        }

        _db.Set<Meeting>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}
