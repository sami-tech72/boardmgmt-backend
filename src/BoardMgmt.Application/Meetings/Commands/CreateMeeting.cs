using BoardMgmt.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;


namespace BoardMgmt.Application.Meetings.Commands;


public record CreateMeetingCommand(string Title, DateTimeOffset ScheduledAt, string Location) : IRequest<Guid>;


public class CreateMeetingValidator : AbstractValidator<CreateMeetingCommand>
{
    public CreateMeetingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }
}


public class CreateMeetingHandler : IRequestHandler<CreateMeetingCommand, Guid>
{
    private readonly DbContext _db;
    public CreateMeetingHandler(DbContext db) => _db = db;


    public async Task<Guid> Handle(CreateMeetingCommand request, CancellationToken ct)
    {
        var entity = new Meeting
        {
            Title = request.Title,
            Location = request.Location,
            ScheduledAt = request.ScheduledAt,
            Status = MeetingStatus.Scheduled
        };
        _db.Set<Meeting>().Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }
}