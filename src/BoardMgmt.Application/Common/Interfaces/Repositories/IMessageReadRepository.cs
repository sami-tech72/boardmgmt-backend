namespace BoardMgmt.Application.Common.Interfaces.Repositories;

public interface IMessageReadRepository
{
    Task<int> CountUnreadAsync(Guid? userId, CancellationToken ct); // pass null if not scoping to user
}
