using BoardMgmt.Application.Folders.DTOs;
using MediatR;

namespace BoardMgmt.Application.Folders.Commands.CreateFolder;

public sealed record CreateFolderCommand(string Name) : IRequest<FolderDto>;
