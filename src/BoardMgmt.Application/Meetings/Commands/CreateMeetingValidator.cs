using FluentValidation;

namespace BoardMgmt.Application.Meetings.Commands;

public sealed class CreateMeetingValidator : AbstractValidator<CreateMeetingCommand>
{
    public CreateMeetingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScheduledAt).NotEmpty();
        RuleFor(x => x.EndAt)
            .Must((cmd, endAt) => endAt is null || endAt > cmd.ScheduledAt)
            .WithMessage("EndAt must be after ScheduledAt.");
        RuleFor(x => x.Location).MaximumLength(500);
    }
}

public sealed class UpdateMeetingValidator : AbstractValidator<UpdateMeetingCommand>
{
    public UpdateMeetingValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScheduledAt).NotEmpty();
        RuleFor(x => x.EndAt)
            .Must((cmd, endAt) => endAt is null || endAt > cmd.ScheduledAt)
            .WithMessage("EndAt must be after ScheduledAt.");
        RuleFor(x => x.Location).MaximumLength(500);
    }
}
