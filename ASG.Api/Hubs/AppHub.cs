using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ASG.Api.Hubs
{
    using ASG.Api.Data;
    using ASG.Api.Models;
    using Microsoft.EntityFrameworkCore;

    public class AppHub : Hub
    {
        private readonly ApplicationDbContext _db;

        public AppHub(ApplicationDbContext db)
        {
            _db = db;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
            }
            await base.OnConnectedAsync();
        }

        public async Task SendDirectMessage(string toUserId, string content)
        {
            var fromUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(fromUserId) || string.IsNullOrEmpty(toUserId) || string.IsNullOrWhiteSpace(content)) return;
            if (toUserId == fromUserId)
            {
                await Clients.Group($"user:{fromUserId}").SendAsync("ReceiveDirectMessageError", new { toUserId, message = "不能给自己发送消息" });
                return;
            }
            var isBlocked = await _db.UserBlocks.AnyAsync(b =>
                (b.BlockerUserId == toUserId && b.BlockedUserId == fromUserId) ||
                (b.BlockerUserId == fromUserId && b.BlockedUserId == toUserId));
            if (isBlocked)
            {
                await Clients.Group($"user:{fromUserId}").SendAsync("ReceiveDirectMessageError", new { toUserId, message = "对方或你已拉黑，无法发送消息" });
                return;
            }
            var convId = await GetOrCreateDirectConversationAsync(fromUserId, toUserId);
            var msg = new Message
            {
                ConversationId = convId,
                SenderUserId = fromUserId,
                Content = content,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var payload = new { conversationId = convId, fromUserId, toUserId, content, createdAt = msg.CreatedAt, messageId = msg.Id };
            await Clients.Groups($"user:{fromUserId}", $"user:{toUserId}").SendAsync("ReceiveDirectMessage", payload);
        }

        public async Task SubscribeEvent(Guid eventId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"event:{eventId}");
        }

        public async Task MarkConversationRead(Guid conversationId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return;
            var unread = await _db.Messages.Where(m => m.ConversationId == conversationId && m.SenderUserId != userId && !m.IsRead).ToListAsync();
            foreach (var m in unread)
            {
                m.IsRead = true;
                m.ReadAt = DateTime.UtcNow;
            }
            if (unread.Count > 0) await _db.SaveChangesAsync();
        }

        private async Task<Guid> GetOrCreateDirectConversationAsync(string a, string b)
        {
            var aConvs = await _db.ConversationMembers.Where(x => x.UserId == a).Select(x => x.ConversationId).ToListAsync();
            var conv = await _db.ConversationMembers.Where(x => x.UserId == b && aConvs.Contains(x.ConversationId)).Select(x => x.ConversationId).FirstOrDefaultAsync();
            if (conv != default)
            {
                var memA = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == conv && x.UserId == a);
                if (memA == null)
                {
                    _db.ConversationMembers.Add(new ConversationMember { ConversationId = conv, UserId = a, JoinedAt = DateTime.UtcNow, IsDeleted = false });
                }
                else if (memA.IsDeleted)
                {
                    memA.IsDeleted = false;
                }
                if (b != a)
                {
                    var memB = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == conv && x.UserId == b);
                    if (memB == null)
                    {
                        _db.ConversationMembers.Add(new ConversationMember { ConversationId = conv, UserId = b, JoinedAt = DateTime.UtcNow, IsDeleted = false });
                    }
                    else if (memB.IsDeleted)
                    {
                        memB.IsDeleted = false;
                    }
                }
                await _db.SaveChangesAsync();
                return conv;
            }
            var c = new Conversation { IsDirect = true, CreatedAt = DateTime.UtcNow };
            _db.Conversations.Add(c);
            await _db.SaveChangesAsync();
            _db.ConversationMembers.Add(new ConversationMember { ConversationId = c.Id, UserId = a, JoinedAt = DateTime.UtcNow, IsDeleted = false });
            if (b != a)
            {
                _db.ConversationMembers.Add(new ConversationMember { ConversationId = c.Id, UserId = b, JoinedAt = DateTime.UtcNow, IsDeleted = false });
            }
            await _db.SaveChangesAsync();
            return c.Id;
        }
    }
}