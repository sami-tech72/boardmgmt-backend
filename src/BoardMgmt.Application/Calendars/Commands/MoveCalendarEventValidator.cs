// Application/Calendars/Commands/MoveCalendarEventValidator.cs
using FluentValidation;

namespace BoardMgmt.Application.Calendars.Commands
{
    public sealed class MoveCalendarEventValidator : AbstractValidator<MoveCalendarEventCommand>
    {
        public MoveCalendarEventValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.NewStartUtc).NotEmpty();
            RuleFor(x => x.NewEndUtc)
                .Must((cmd, end) => !end.HasValue || end.Value > cmd.NewStartUtc)
                .WithMessage("NewEndUtc must be after NewStartUtc.");
        }
    }
}
