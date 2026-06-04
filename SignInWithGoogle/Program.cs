
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SignInWithGoogle.Data;
using SignInWithGoogle.Hubs;
using SignInWithGoogle.Services;
using System.Text;

namespace SignInWithGoogle
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();
            builder.Services.AddSwaggerGen();
            // ── Database ──────────────────────────────────────────────────────────────────
            builder.Services.AddDbContext<AppDbContext>(o =>
                o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

            // ── Services ──────────────────────────────────────────────────────────────────
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<GoogleAuthService>();
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<MessageService>();

            // ConnectionTracker must be singleton — it holds in-memory connection state
            builder.Services.AddSingleton<ConnectionTracker>();

            // ── JWT Bearer auth ───────────────────────────────────────────────────────────
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
                        ClockSkew = TimeSpan.Zero,
                    };

                    // ── CRITICAL: SignalR sends the JWT in the query string, not a header ──
                    // Browsers cannot set Authorization headers on WebSocket upgrades.
                    // SignalR's JS client automatically appends ?access_token=<jwt>
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var token = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;

                            // This string must exactly match what you passed to MapHub<>()
                            if (!string.IsNullOrEmpty(token) &&
                                path.StartsWithSegments("/hubs/chat"))
                            {
                                context.Token = token;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });


            builder.Services.AddAuthorization();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("Dev", policy =>
                {
                    policy
                        .SetIsOriginAllowed(_ => true)   // allow null origin from about:blank
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();             // required for SignalR
                });
            });
            // ── SignalR ───────────────────────────────────────────────────────────────────
            builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB max per message
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            // ── Auto-apply EF migrations on startup ───────────────────────────────────────
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }
            //app.UseHttpsRedirection();
            app.UseCors("Dev");
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();
            app.MapHub<ChatHub>("/hubs/chat");
            app.Run();
        }
    }
}
