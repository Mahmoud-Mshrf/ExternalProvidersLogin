using Microsoft.EntityFrameworkCore;
using SignInWithGoogle.Data;
using SignInWithGoogle.Models;

namespace SignInWithGoogle.Services
{
    public class MessageService
    {
        private readonly AppDbContext _db;

        public MessageService(AppDbContext db) => _db = db;

        public async Task<Message> SaveAsync(
            Guid senderId, Guid receiverId, string content)
        {
            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                SentAt = DateTime.UtcNow,
                IsRead = false,
            };

            _db.Messages.Add(message);
            await _db.SaveChangesAsync();
            return message;
        }

        // Fetch the conversation between two users — most recent last
        public async Task<List<Message>> GetConversationAsync(
            Guid userA, Guid userB, int page = 1, int pageSize = 50)
        {
            return await _db.Messages
                .Where(m =>
                    (m.SenderId == userA && m.ReceiverId == userB) ||
                    (m.SenderId == userB && m.ReceiverId == userA))
                .OrderBy(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .ToListAsync();
        }

        // Mark all messages from a sender to a receiver as read
        public async Task MarkAsReadAsync(Guid senderId, Guid receiverId)
        {
            await _db.Messages
                .Where(m => m.SenderId == senderId &&
                            m.ReceiverId == receiverId &&
                            !m.IsRead)
                .ExecuteUpdateAsync(s =>
                    s.SetProperty(m => m.IsRead, true));
        }

        // Count unread messages for a user
        public async Task<int> GetUnreadCountAsync(Guid receiverId)
        {
            return await _db.Messages
                .CountAsync(m => m.ReceiverId == receiverId && !m.IsRead);
        }
    }
}
