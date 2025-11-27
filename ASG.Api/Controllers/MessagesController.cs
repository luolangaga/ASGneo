using ASG.Api.Data;
using ASG.Api.DTOs;
using ASG.Api.Hubs;
using ASG.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<AppHub> _hub;

        public MessagesController(ApplicationDbContext db, IHubContext<AppHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        [HttpGet("conversations")]
        public async Task<ActionResult<IEnumerable<ConversationDto>>> GetConversations()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            var convIds = await _db.ConversationMembers.Where(x => x.UserId == userId && !x.IsDeleted).Select(x => x.ConversationId).ToListAsync();
            var convs = await _db.Conversations.Where(c => convIds.Contains(c.Id)).ToListAsync();
            var result = new List<ConversationDto>();
            foreach (var c in convs)
            {
                var members = await _db.ConversationMembers.Where(x => x.ConversationId == c.Id).Select(x => x.UserId).ToListAsync();
                var peerId = members.FirstOrDefault(x => x != userId);
                var peer = string.IsNullOrEmpty(peerId) ? null : await _db.Users.FirstOrDefaultAsync(u => u.Id == peerId);
                var msgs = await _db.Messages.Where(m => m.ConversationId == c.Id).OrderByDescending(m => m.CreatedAt).Take(1).ToListAsync();
                var unread = await _db.Messages.CountAsync(m => m.ConversationId == c.Id && m.SenderUserId != userId && !m.IsRead);
                result.Add(new ConversationDto
                {
                    Id = c.Id,
                    PeerUserId = peerId,
                    PeerName = peer?.FullName ?? peer?.UserName,
                    LastMessage = msgs.FirstOrDefault()?.Content,
                    LastTime = msgs.FirstOrDefault()?.CreatedAt,
                    UnreadCount = unread
                });
            }
            var lastN = await _db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync();
            var unreadN = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            result.Add(new ConversationDto
            {
                Id = Guid.Empty,
                PeerUserId = "system",
                PeerName = "系统通知",
                LastMessage = lastN?.Type,
                LastTime = lastN?.CreatedAt,
                UnreadCount = unreadN
            });
            return Ok(result.OrderByDescending(x => x.LastTime));
        }

        [HttpGet("conversations/{id}/messages")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMessages(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            if (id == Guid.Empty)
            {
                var nlist = await _db.Notifications.Where(n => n.UserId == userId).OrderBy(n => n.CreatedAt).ToListAsync();
                var mapped = nlist.Select(n => new MessageDto { Id = n.Id, ConversationId = Guid.Empty, SenderUserId = "system", Content = string.IsNullOrEmpty(n.Payload) ? n.Type : n.Payload, CreatedAt = n.CreatedAt, IsRead = n.IsRead }).ToList();
                return Ok(mapped);
            }
            else
            {
                var mem = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == id && x.UserId == userId);
                if (mem == null) return Forbid();
                if (mem.IsDeleted) return Ok(new List<MessageDto>());
                var list = await _db.Messages.Where(m => m.ConversationId == id).OrderBy(m => m.CreatedAt).Select(m => new MessageDto { Id = m.Id, ConversationId = m.ConversationId, SenderUserId = m.SenderUserId, Content = m.Content, CreatedAt = m.CreatedAt, IsRead = m.IsRead }).ToListAsync();
                return Ok(list);
            }
        }

        [HttpPost("conversations/{id}/read")]
        public async Task<IActionResult> MarkConversationRead(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            if (id == Guid.Empty)
            {
                var unreadN = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
                foreach (var n in unreadN) { n.IsRead = true; }
                if (unreadN.Count > 0) await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            var mem = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == id && x.UserId == userId);
            if (mem == null) return NotFound(new { message = "会话不存在" });
            var unread = await _db.Messages.Where(m => m.ConversationId == id && m.SenderUserId != userId && !m.IsRead).ToListAsync();
            foreach (var m in unread)
            {
                m.IsRead = true;
                m.ReadAt = DateTime.UtcNow;
            }
            if (unread.Count > 0) await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("user/{toUserId}/message")]
        public async Task<ActionResult<MessageDto>> SendToUser(string toUserId, [FromBody] SendMessageDto dto)
        {
            var fromUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(fromUserId)) return Unauthorized(new { message = "未登录" });
            if (string.IsNullOrWhiteSpace(dto?.Content)) return BadRequest(new { message = "内容不能为空" });
            if (toUserId == fromUserId) return BadRequest(new { message = "不能给自己发送消息" });
            var isBlocked = await _db.UserBlocks.AnyAsync(b =>
                (b.BlockerUserId == toUserId && b.BlockedUserId == fromUserId) ||
                (b.BlockerUserId == fromUserId && b.BlockedUserId == toUserId));
            if (isBlocked) return BadRequest(new { message = "对方或你已拉黑，无法发送消息" });
            var aConvs = await _db.ConversationMembers.Where(x => x.UserId == fromUserId).Select(x => x.ConversationId).ToListAsync();
            var convId = await _db.ConversationMembers.Where(x => x.UserId == toUserId && aConvs.Contains(x.ConversationId)).Select(x => x.ConversationId).FirstOrDefaultAsync();
            if (convId == default)
            {
                var c = new Conversation { IsDirect = true, CreatedAt = DateTime.UtcNow };
                _db.Conversations.Add(c);
                await _db.SaveChangesAsync();
                _db.ConversationMembers.Add(new ConversationMember { ConversationId = c.Id, UserId = fromUserId, JoinedAt = DateTime.UtcNow });
                if (toUserId != fromUserId)
                {
                    _db.ConversationMembers.Add(new ConversationMember { ConversationId = c.Id, UserId = toUserId, JoinedAt = DateTime.UtcNow });
                }
                await _db.SaveChangesAsync();
                convId = c.Id;
            }
            var myMem = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == convId && x.UserId == fromUserId);
            if (myMem != null && myMem.IsDeleted) myMem.IsDeleted = false;
            var peerMem = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == convId && x.UserId == toUserId);
            if (peerMem != null && peerMem.IsDeleted) peerMem.IsDeleted = false;
            await _db.SaveChangesAsync();
            var msg = new Message { ConversationId = convId, SenderUserId = fromUserId, Content = dto.Content, CreatedAt = DateTime.UtcNow, IsRead = false };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();
            var payload = new { conversationId = convId, fromUserId, toUserId, content = dto.Content, createdAt = msg.CreatedAt, messageId = msg.Id };
            await _hub.Clients.Groups($"user:{fromUserId}", $"user:{toUserId}").SendAsync("ReceiveDirectMessage", payload);
            return Ok(new MessageDto { Id = msg.Id, ConversationId = msg.ConversationId, SenderUserId = msg.SenderUserId, Content = msg.Content, CreatedAt = msg.CreatedAt, IsRead = msg.IsRead });
        }

        [HttpDelete("conversations/{id}/clear")]
        public async Task<IActionResult> ClearConversation(Guid id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            if (id == Guid.Empty) return BadRequest(new { message = "不能清空系统通知" });
            var mem = await _db.ConversationMembers.FirstOrDefaultAsync(x => x.ConversationId == id && x.UserId == userId);
            if (mem == null) return NotFound(new { message = "会话不存在" });
            mem.IsDeleted = true;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpPost("blocks/{blockedUserId}")]
        public async Task<IActionResult> BlockUser(string blockedUserId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            if (string.IsNullOrEmpty(blockedUserId) || blockedUserId == userId) return BadRequest(new { message = "参数错误" });
            var exists = await _db.UserBlocks.AnyAsync(b => b.BlockerUserId == userId && b.BlockedUserId == blockedUserId);
            if (!exists)
            {
                _db.UserBlocks.Add(new UserBlock { BlockerUserId = userId, BlockedUserId = blockedUserId, CreatedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpDelete("blocks/{blockedUserId}")]
        public async Task<IActionResult> UnblockUser(string blockedUserId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            var blk = await _db.UserBlocks.FirstOrDefaultAsync(b => b.BlockerUserId == userId && b.BlockedUserId == blockedUserId);
            if (blk != null)
            {
                _db.UserBlocks.Remove(blk);
                await _db.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        [HttpGet("blocks/{peerUserId}")]
        public async Task<IActionResult> CheckBlock(string peerUserId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { message = "未登录" });
            var youBlocked = await _db.UserBlocks.AnyAsync(b => b.BlockerUserId == userId && b.BlockedUserId == peerUserId);
            var blockedYou = await _db.UserBlocks.AnyAsync(b => b.BlockerUserId == peerUserId && b.BlockedUserId == userId);
            return Ok(new { youBlocked, blockedYou });
        }
    }

    public class SendMessageDto
    {
        public string Content { get; set; } = string.Empty;
    }
}