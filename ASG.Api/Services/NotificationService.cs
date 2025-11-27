using ASG.Api.Data;
using ASG.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Services
{
    public interface INotificationService
    {
        Task NotifyUserAsync(string userId, string type, string payload);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<ASG.Api.Hubs.AppHub> _hub;

        public NotificationService(ApplicationDbContext db, IHubContext<ASG.Api.Hubs.AppHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        public async Task NotifyUserAsync(string userId, string type, string payload)
        {
            var n = new Notification { UserId = userId, Type = type, Payload = payload, CreatedAt = DateTime.UtcNow, IsRead = false };
            _db.Notifications.Add(n);
            await _db.SaveChangesAsync();
            var data = new { id = n.Id, type = n.Type, payload = n.Payload, createdAt = n.CreatedAt };
            await _hub.Clients.Group($"user:{userId}").SendAsync("ReceiveNotification", data);
        }
    }
}