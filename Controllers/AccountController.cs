using Microsoft.AspNetCore.Mvc;
using SitradWebInterface.Models;
using SitradWebInterface.Services;

namespace SitradWebInterface.Controllers
{
    public class AccountController : Controller
    {
        private const string UserIdSessionKey = "UserId";
        private const string UserRoleSessionKey = "UserRole";
        private readonly IAuthService _authService;

        public AccountController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Si ya está autenticado, redirigir al dashboard
            if (IsAuthenticated())
            {
                return RedirectToAction("Index", "Sitrad");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Usuario y contraseña son requeridos");
                return View();
            }

            var user = await _authService.LoginAsync(username, password);
            if (user == null)
            {
                ModelState.AddModelError("", "Usuario o contraseña incorrectos");
                return View();
            }

            // Guardar en sesión
            HttpContext.Session.SetInt32(UserIdSessionKey, user.Id);
            HttpContext.Session.SetString(UserRoleSessionKey, user.Role.ToString());

            return RedirectToAction("Index", "Sitrad");
        }

        [HttpGet]
        public IActionResult Register()
        {
            // Si ya está autenticado, redirigir al dashboard
            if (IsAuthenticated())
            {
                return RedirectToAction("Index", "Sitrad");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password, string confirmPassword, string role, string? inviteCode)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "Usuario y contraseña son requeridos");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError("", "Las contraseñas no coinciden");
                return View();
            }

            if (password.Length < 4)
            {
                ModelState.AddModelError("", "La contraseña debe tener al menos 4 caracteres");
                return View();
            }

            if (!Enum.TryParse<UserRole>(role, out var userRole))
            {
                ModelState.AddModelError("", "Rol inválido");
                return View();
            }

            // Validar código de invitación para operarios
            if (userRole == UserRole.Operario)
            {
                var isValidCode = await _authService.ValidateInviteCodeAsync(inviteCode ?? "", userRole);
                if (!isValidCode)
                {
                    ModelState.AddModelError("", "Código de invitación inválido. Se requiere un código válido para registrarse como operario.");
                    return View();
                }
            }

            var success = await _authService.RegisterAsync(username, password, userRole, inviteCode);
            if (!success)
            {
                ModelState.AddModelError("", "El usuario ya existe o el código de invitación es inválido");
                return View();
            }

            TempData["SuccessMessage"] = "Usuario registrado exitosamente. Puede iniciar sesión ahora.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Métodos auxiliares
        public bool IsAuthenticated()
        {
            return HttpContext.Session.GetInt32(UserIdSessionKey).HasValue;
        }

        public UserRole? GetUserRole()
        {
            var roleString = HttpContext.Session.GetString(UserRoleSessionKey);
            if (Enum.TryParse<UserRole>(roleString, out var role))
            {
                return role;
            }
            return null;
        }

        public int? GetUserId()
        {
            return HttpContext.Session.GetInt32(UserIdSessionKey);
        }
    }
}

