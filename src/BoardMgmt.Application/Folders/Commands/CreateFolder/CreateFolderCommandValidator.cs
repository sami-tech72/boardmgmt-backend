using FluentValidation;

namespace BoardMgmt.Application.Folders.Commands.CreateFolder;

public sealed class CreateFolderCommandValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
    }
}
