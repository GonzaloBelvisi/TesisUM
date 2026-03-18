using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SitradWebInterface.Data;
using SitradWebInterface.Models;

namespace SitradWebInterface.Services
{
    public interface IAuthService
    {
        Task<bool> RegisterAsync(string username, string password, UserRole role, string? inviteCode = null);
        Task<bool> ValidateInviteCodeAsync(string inviteCode, UserRole role);
        Task<User?> LoginAsync(string username, string password);
        Task<User?> GetUserByIdAsync(int userId);
        Task<List<User>> GetAllUsersAsync();
        Task<bool> DeleteUserAsync(int userId);
        Task EnsureAdminUserExistsAsync();
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly string _operarioInviteCode;
        private readonly string _adminPassword;

        public AuthService(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _operarioInviteCode = configuration["AuthSettings:OperarioInviteCode"] ?? "OPERARIO2025";
            _adminPassword = configuration["AuthSettings:AdminPassword"] ?? "CHANGE_THIS_PASSWORD";
        }

        public async Task EnsureAdminUserExistsAsync()
        {
            var adminExists = await _db.Users
                .AnyAsync(u => u.Username.ToLower() == "admin".ToLower());
            
            if (!adminExists)
            {
                var adminUser = new User
                {
                    Username = "admin",
                    PasswordHash = HashPassword(_adminPassword),
                    Role = UserRole.Operario,
                    CreatedAt = DateTime.UtcNow
                };
                
                _db.Users.Add(adminUser);
                await _db.SaveChangesAsync();
            }
        }

        public Task<bool> ValidateInviteCodeAsync(string inviteCode, UserRole role)
        {
            // Solo se requiere código de invitación para operarios
            if (role == UserRole.Operario)
            {
                return Task.FromResult(
                    !string.IsNullOrWhiteSpace(inviteCode) &&
                    inviteCode.Equals(_operarioInviteCode, StringComparison.OrdinalIgnoreCase)
                );
            }
            // Los visualizadores no necesitan código
            return Task.FromResult(true);
        }

        public async Task<bool> RegisterAsync(string username, string password, UserRole role, string? inviteCode = null)
        {
            // Validar código de invitación si es operario
            if (role == UserRole.Operario)
            {
                var isValidCode = await ValidateInviteCodeAsync(inviteCode ?? "", role);
                if (!isValidCode)
                {
                    return false;
                }
            }

            // Verificar si el usuario ya existe
            var userExists = await _db.Users
                .AnyAsync(u => u.Username.ToLower() == username.ToLower());
            
            if (userExists)
            {
                return false;
            }

            var user = new User
            {
                Username = username,
                PasswordHash = HashPassword(password),
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            var passwordHash = HashPassword(password);
            var user = await _db.Users
                .FirstOrDefaultAsync(u =>
                    u.Username.ToLower() == username.ToLower() &&
                    u.PasswordHash == passwordHash);

            return user;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _db.Users.FindAsync(userId);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _db.Users.ToListAsync();
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}

