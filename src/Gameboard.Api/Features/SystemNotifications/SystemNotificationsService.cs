using System.Linq;
using System.Threading.Tasks;
using Gameboard.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gameboard.Api.Features.SystemNotifications;

public interface ISystemNotificationsService
{
    Task<ViewSystemNotification> Get(string id);
    ViewSystemNotification ToViewSystemNotification(SystemNotification notification);
}

internal class SystemNotificationsService : ISystemNotificationsService
{
    private readonly IStore _store;

    public SystemNotificationsService(IStore store)
    {
        _store = store;
    }

    public Task<ViewSystemNotification> Get(string id)
        => _store
            .WithNoTracking<SystemNotification>()
                .Include(n => n.CreatedByUser)
            .Select(n => ToViewSystemNotification(n))
            .SingleOrDefaultAsync(n => n.Id == id);

    public ViewSystemNotification ToViewSystemNotification(SystemNotification notification)
        => new()
        {
            Id = notification.Id,
            Title = notification.Title,
            MarkdownContent = notification.MarkdownContent,
            StartsOn = notification.StartsOn,
            EndsOn = notification.EndsOn,
            NotificationType = notification.NotificationType,
            CreatedBy = new SimpleEntity { Id = notification.CreatedByUserId, Name = notification.CreatedByUser.ApprovedName }
        };
}
