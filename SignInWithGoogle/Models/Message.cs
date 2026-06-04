namespace SignInWithGoogle.Models
{
    public class Message
    {
        public Guid Id { get; set; }
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; }
        public string Content { get; set; } = "";
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }

        public User Sender { get; set; } = null!;
        public User Receiver { get; set; } = null!;
    }
}
