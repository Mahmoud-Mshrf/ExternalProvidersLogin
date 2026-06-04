using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SignInWithGoogle.Services;
using System.Security.Claims;

namespace SignInWithGoogle.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ConnectionTracker _tracker;
        private readonly MessageService _messages;

        public ChatHub(ConnectionTracker tracker, MessageService messages)
        {
            _tracker = tracker;
            _messages = messages;
        }

        // ── Fires when a client connects ──────────────────────────────────────────
        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();
            _tracker.Add(userId, Context.ConnectionId);

            // Tell this user how many unread messages they have waiting
            var unread = await _messages.GetUnreadCountAsync(userId);
            await Clients.Caller.SendAsync("UnreadCount", unread);

            await base.OnConnectedAsync();
        }

        // ── Fires when a client disconnects (tab closed, network drop, etc.) ──────
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();
            _tracker.Remove(userId, Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        // ── Client calls this to send a message ───────────────────────────────────
        // Hub method name the client invokes: "SendMessage"
        public async Task SendMessage(Guid receiverId, string content)
        {
            if (string.IsNullOrWhiteSpace(content) || content.Length > 4000)
                throw new HubException("Message content is invalid.");

            var senderId = GetUserId();

            if (senderId == receiverId)
                throw new HubException("Cannot send a message to yourself.");

            // 1. Persist to DB
            var message = await _messages.SaveAsync(senderId, receiverId, content);

            // 2. Build the payload both parties will receive
            var payload = new
            {
                messageId = message.Id,
                senderId = message.SenderId,
                receiverId = message.ReceiverId,
                content = message.Content,
                sentAt = message.SentAt,
            };

            // 3. Deliver to every connection the receiver currently has open
            var receiverConnections = _tracker.GetConnections(receiverId);
            if (receiverConnections.Count > 0)
            {
                await Clients
                    .Clients(receiverConnections.ToList())
                    .SendAsync("ReceiveMessage", payload);

                // Also send a notification event so the receiver can show a badge
                await Clients
                    .Clients(receiverConnections.ToList())
                    .SendAsync("NewMessageNotification", new
                    {
                        from = senderId,
                        preview = content.Length > 60
                                        ? content[..60] + "…"
                                        : content,
                        sentAt = message.SentAt,
                    });
            }

            // 4. Echo back to the sender's own connections (other tabs/devices)
            var senderConnections = _tracker
                .GetConnections(senderId)
                .Where(c => c != Context.ConnectionId)
                .ToList();

            if (senderConnections.Count > 0)
            {
                await Clients
                    .Clients(senderConnections)
                    .SendAsync("ReceiveMessage", payload);
            }

            // 5. Confirm delivery to the calling connection
            await Clients.Caller.SendAsync("MessageSent", payload);
        }

        // ── Client calls this when they open a conversation ───────────────────────
        // Marks all messages from the other user as read
        public async Task MarkRead(Guid senderId)
        {
            var receiverId = GetUserId();
            await _messages.MarkAsReadAsync(senderId, receiverId);

            // Tell the original sender their messages were read
            var senderConnections = _tracker.GetConnections(senderId);
            if (senderConnections.Count > 0)
            {
                await Clients
                    .Clients(senderConnections.ToList())
                    .SendAsync("MessagesRead", new { by = receiverId });
            }
        }

        // ── Helper ────────────────────────────────────────────────────────────────
        private Guid GetUserId()
        {
            var raw = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? Context.User?.FindFirst("sub")?.Value;

            if (!Guid.TryParse(raw, out var userId))
                throw new HubException("Unauthorized.");

            return userId;
        }
    }
}
