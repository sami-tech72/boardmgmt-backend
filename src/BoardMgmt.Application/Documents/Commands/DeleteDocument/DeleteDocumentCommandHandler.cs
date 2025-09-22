// Application/Documents/Commands/DeleteDocument/DeleteDocumentCommandHandler.cs
using BoardMgmt.Application.Common.Interfaces;
using BoardMgmt.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace BoardMgmt.Application.Documents.Commands.DeleteDocument;

public class DeleteDocumentCommandHandler(
    IAppDbContext db,
    IPermissionService perms,
    IFileStorage storage
) : IRequestHandler<DeleteDocumentCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        await perms.EnsureMineAsync(AppModule.Documents, Permission.Delete, ct);

        var doc = await db.Documents
            .Include(d => d.RoleAccesses)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);

        if (doc is null) return Unit.Value; // idempotent

        // Optionally delete underlying blob
        try { await storage.DeleteAsync(doc.FileName, ct); } catch { /* ignore */ }

        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
