using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SignInWithGoogle.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace SignInWithGoogle.Controllers
{
    [ApiController]
    [Route("auth/google")]
    public class GoogleAuthController : ControllerBase
    {
        private readonly GoogleAuthService _googleAuth;
        private readonly JwtService _jwt;

        public GoogleAuthController(GoogleAuthService googleAuth, JwtService jwt)
        {
            _googleAuth = googleAuth;
            _jwt = jwt;
        }

        // ── Step 1: Redirect the browser to Google ────────────────────────────────
        // Open this in a browser: https://localhost:5001/auth/google/login
        [HttpGet("login")]
        public IActionResult Login()
        {
            // Generate a random state value and store it in a short-lived cookie
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            Response.Cookies.Append("oauth_state", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,    // Lax allows the redirect back from Google
                MaxAge = TimeSpan.FromMinutes(10),
            });

            var redirectUrl = _googleAuth.BuildRedirectUrl(state);
            return Redirect(redirectUrl);
        }

        // ── Step 2: Google redirects back here with ?code=…&state=… ──────────────
        [HttpGet("callback")]
        public async Task<IActionResult> Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error)
        {
            // Handle user denying consent on Google's screen
            if (!string.IsNullOrEmpty(error))
                return BadRequest($"Google sign-in was denied: {error}");

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
                return BadRequest("Missing code or state.");

            // ── CSRF check ────────────────────────────────────────────────────────
            if (!Request.Cookies.TryGetValue("oauth_state", out var storedState))
                return BadRequest("State cookie missing or expired.");

            Response.Cookies.Delete("oauth_state");     // one-use only

            if (state != storedState)
                return BadRequest("State mismatch. Request may have been tampered with.");

            // ── Exchange code for tokens ───────────────────────────────────────────
            var tokenResponse = await _googleAuth.ExchangeCodeAsync(code);
            if (tokenResponse is null)
                return StatusCode(502, "Failed to exchange authorization code with Google.");

            // ── Validate the id_token ─────────────────────────────────────────────
            var payload = await _googleAuth.ValidateIdTokenAsync(tokenResponse.IdToken);
            if (payload is null)
                return Unauthorized("Google id_token validation failed.");

            // ── Find or create the user in your database ──────────────────────────
            var user = await _googleAuth.FindOrCreateUserAsync(payload);

            // ── Issue your own JWT ────────────────────────────────────────────────
            var jwt = _jwt.Generate(user);

            return Ok(new
            {
                token = jwt,
                name = user.Name,
                email = user.Email,
                picture = user.PictureUrl,
                userId = user.Id,
            });
        }

        // ── Protected endpoint to verify everything works ─────────────────────────
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;
            var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                      ?? User.FindFirst("email")?.Value;

            return Ok(new { userId, email });
        }
    }

    // DTO matching Google's token response
    public class TokenResponse
    {
        [JsonPropertyName("id_token")] public string IdToken { get; set; } = "";
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("token_type")] public string TokenType { get; set; } = "";
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }
}
