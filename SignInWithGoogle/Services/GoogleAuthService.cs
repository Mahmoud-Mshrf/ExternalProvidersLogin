using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using SignInWithGoogle.Data;
using SignInWithGoogle.Dtos;
using SignInWithGoogle.Models;

namespace SignInWithGoogle.Services
{
    public class GoogleAuthService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;
        private readonly HttpClient _http;

        public GoogleAuthService(IConfiguration config, AppDbContext db, IHttpClientFactory factory)
        {
            _config = config;
            _db = db;
            _http = factory.CreateClient();
        }

        // ── 1. Build the Google redirect URL ─────────────────────────────────────

        public string BuildRedirectUrl(string state)
        {
            var clientId = _config["Google:ClientId"]!;
            var redirectUri = _config["Google:RedirectUri"]!;

            var parameters = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["access_type"] = "offline",
                ["prompt"] = "consent",
                ["state"] = state,
            };

            var query = string.Join("&",
                parameters.Select(kvp =>
                    $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

            return "https://accounts.google.com/o/oauth2/v2/auth?" + query;
        }

        // ── 2. Exchange authorization code for tokens ─────────────────────────────

        public async Task<GoogleTokenResponse?> ExchangeCodeAsync(string code)
        {
            var body = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _config["Google:ClientId"]!,
                ["client_secret"] = _config["Google:ClientSecret"]!,
                ["redirect_uri"] = _config["Google:RedirectUri"]!,
                ["grant_type"] = "authorization_code",
            };

            var response = await _http.PostAsync(
                "https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(body));

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content
                .ReadFromJsonAsync<GoogleTokenResponse>();
        }

        // ── 3. Validate the id_token using Google's library ───────────────────────

        public async Task<GoogleJsonWebSignature.Payload?> ValidateIdTokenAsync(string idToken)
        {
            try
            {
                return await GoogleJsonWebSignature.ValidateAsync(
                    idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { _config["Google:ClientId"]! }
                    });
            }
            catch (InvalidJwtException)
            {
                return null;
            }
        }

        // ── 4. Find existing user or create a new one ─────────────────────────────

        public async Task<User> FindOrCreateUserAsync(GoogleJsonWebSignature.Payload payload)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.GoogleId == payload.Subject);

            if (user is null)
            {
                user = new User
                {
                    GoogleId = payload.Subject,
                    Email = payload.Email,
                    Name = payload.Name,
                    PictureUrl = payload.Picture,
                    CreatedAt = DateTime.UtcNow,
                };
                _db.Users.Add(user);
            }
            else
            {
                // Keep profile info in sync with Google on every login
                user.Email = payload.Email;
                user.Name = payload.Name;
                user.PictureUrl = payload.Picture;
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return user;
        }
    }
}
