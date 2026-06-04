namespace SignInWithGoogle.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string GoogleId { get; set; } = "";   // payload.Subject — never changes
        public string Email { get; set; } = "";
        public string? Name { get; set; }
        public string? PictureUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastLoginAt { get; set; }
    }
}
