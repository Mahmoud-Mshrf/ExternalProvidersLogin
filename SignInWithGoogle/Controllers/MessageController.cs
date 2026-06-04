using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SignInWithGoogle.Models;
using SignInWithGoogle.Services;
using System.Security.Claims;

namespace SignInWithGoogle.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/messages")]
    public class MessagesController : ControllerBase
    {
        private readonly MessageService _messages;
        private readonly ConnectionTracker _tracker;

        public MessagesController(MessageService messages, ConnectionTracker tracker)
        {
            _messages = messages;
            _tracker = tracker;
        }

        // GET /api/messages/{otherUserId}?page=1&pageSize=50
        [HttpGet("{otherUserId:guid}")]
        public async Task<IActionResult> GetConversation(
            Guid otherUserId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var myId = GetUserId();
            pageSize = Math.Clamp(pageSize, 1, 100);

            var messages = await _messages.GetConversationAsync(
                myId, otherUserId, page, pageSize);

            return Ok(messages.Select(m => new
            {
                id = m.Id,
                senderId = m.SenderId,
                receiverId = m.ReceiverId,
                content = m.Content,
                sentAt = m.SentAt,
                isRead = m.IsRead,
            }));
        }

        // GET /api/messages/unread
        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _messages.GetUnreadCountAsync(GetUserId());
            return Ok(new { unreadCount = count });
        }

        // GET /api/messages/online/{userId}
        [HttpGet("online/{userId:guid}")]
        public IActionResult IsOnline(Guid userId)
        {
            return Ok(new { isOnline = _tracker.IsOnline(userId) });
        }

        private Guid GetUserId()
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(raw, out var userId))
                throw new UnauthorizedAccessException();

            return userId;
        }
    }
}
