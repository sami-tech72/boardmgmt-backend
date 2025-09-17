using BoardMgmt.Application.Folders.DTOs;
using MediatR;


namespace BoardMgmt.Application.Folders.Queries.GetFolders;


public sealed record GetFoldersQuery() : IRequest<IReadOnlyList<FolderDto>>;