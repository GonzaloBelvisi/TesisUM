using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SitradWebInterface.Data;
using SitradWebInterface.Services;

var builder = WebApplication.CreateBuilder(args);

// Configura Kestrel: usa PORT de Railway si está disponible, sino 5220 local
var port = Environment.GetEnvironmentVariable("PORT") ?? "5220";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Agregar servicios al contenedor
builder.Services.AddControllersWithViews();

// Registrar HttpClient
builder.Services.AddHttpClient();

// Configurar SQLite Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=sitrad.db"));

// Registrar servicio de autenticación (con DbContext y configuración)
builder.Services.AddScoped<IAuthService, AuthService>();

// ** Habilitar sesiones **
builder.Services.AddSession(options =>
{
    // Puedes ajustar el timeout si quieres (por defecto 20 min)
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configurar el pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ** Añadir middleware de sesión **
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Crear base de datos si no existe y crear usuario admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated(); // Crear base de datos y tablas si no existen
    
    // Crear usuario admin si no existe
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
    await authService.EnsureAdminUserExistsAsync();
}

// Iniciar el procesamiento de reintentos en background
_ = Task.Run(async () => await SitradWebInterface.Controllers.SitradController.ProcessRetryQueue());

app.Run();
