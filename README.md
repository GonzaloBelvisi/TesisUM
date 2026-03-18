# Sitrad Web Interface

Sistema web de monitoreo y control de cámaras de almacenamiento con autenticación basada en roles. Permite visualizar y gestionar parámetros de temperatura, humedad, CO₂ y otros sensores en tiempo real.

## 📋 Descripción

Aplicación web desarrollada en ASP.NET Core que proporciona una interfaz moderna para el monitoreo y control de sensores en cámaras de almacenamiento. El sistema incluye:

- **Dashboard en tiempo real** con actualización automática de datos
- **Sistema de autenticación** con roles (Operario y Visualizador)
- **Control de setpoints** para operarios autorizados
- **Vista responsive** optimizada para escritorio y móvil
- **Sistema de reintentos automáticos** para actualizaciones de setpoints

## 🚀 Características

### Roles de Usuario

- **Operario**: Puede modificar valores de setpoints y tiene acceso completo al sistema
- **Visualizador**: Solo puede visualizar datos, sin permisos de modificación

### Funcionalidades

- Monitoreo en tiempo real de temperatura, humedad, CO₂, etileno
- Visualización de temperatura de pulpa y evaporador
- Control de setpoints SET1 (frío) y SET3 (calor)
- Sistema de alertas visuales basado en umbrales
- Interfaz responsive con diseño moderno
- Autenticación segura con hash SHA256

## 🛠️ Tecnologías

- **.NET 8.0** - Framework principal
- **ASP.NET Core MVC** - Arquitectura web
- **Tailwind CSS** - Estilos y diseño responsive
- **jQuery** - Interactividad del frontend
- **Iconify** - Iconos SVG
- **Newtonsoft.Json** - Serialización JSON

## 📦 Requisitos

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o superior
- Navegador web moderno (Chrome, Firefox, Edge, Safari)

## 🔧 Instalación

1. **Clonar el repositorio**
   ```bash
   git clone https://github.com/tu-usuario/sitrad-web-interface.git
   cd sitrad-web-interface
   ```

2. **Configurar la aplicación**
   ```bash
   # Copiar el archivo de ejemplo de configuración
   cp appsettings.example.json appsettings.json
   ```

3. **Editar `appsettings.json`**
   
   Configurar las siguientes secciones:
   ```json
   {
     "AuthSettings": {
       "OperarioInviteCode": "TU_CODIGO_INVITACION",
       "AdminPassword": "CONTRASEÑA_ADMIN"
     },
     "SitradApi": {
       "BaseUrl": "https://tu-api-url.com/api/v1",
       "Username": "tu_usuario",
       "Password": "tu_contraseña"
     }
   }
   ```

4. **Restaurar dependencias**
   ```bash
   dotnet restore
   ```

5. **Ejecutar la aplicación**
   ```bash
   dotnet run
   ```

   La aplicación estará disponible en: `http://localhost:5220`

## ⚙️ Configuración

### Variables de Entorno (Opcional)

Puedes configurar las credenciales de la API usando variables de entorno:

```bash
# Windows PowerShell
$env:SITRAD_API_URL="https://tu-api.com/api/v1"
$env:SITRAD_API_USERNAME="tu_usuario"
$env:SITRAD_API_PASSWORD="tu_contraseña"

# Linux/Mac
export SITRAD_API_URL="https://tu-api.com/api/v1"
export SITRAD_API_USERNAME="tu_usuario"
export SITRAD_API_PASSWORD="tu_contraseña"
```

### Configuración de Puertos

Para cambiar el puerto, edita `Program.cs`:

```csharp
builder.WebHost.UseUrls("http://0.0.0.0:TU_PUERTO");
```

## 👤 Uso

### Primer Acceso

1. Al iniciar la aplicación, serás redirigido a la página de login
2. **Usuario inicial**: `admin`
3. **Contraseña**: La configurada en `appsettings.json` bajo `AuthSettings:AdminPassword`

### Registro de Nuevos Usuarios

1. Ir a "Regístrate aquí" desde la página de login
2. Completar el formulario:
   - **Usuario**: Nombre de usuario único
   - **Contraseña**: Mínimo 4 caracteres
   - **Tipo de Usuario**: 
     - **Operario**: Requiere código de invitación
     - **Visualizador**: No requiere código
3. Si seleccionas "Operario", deberás ingresar el código de invitación configurado en `appsettings.json`

### Operaciones

- **Visualizar datos**: Todos los usuarios pueden ver el dashboard
- **Modificar setpoints**: Solo operarios pueden cambiar valores de SET1 y SET3
- **Cerrar sesión**: Botón de logout en el header

## 📁 Estructura del Proyecto

```
SitradWebInterface/
├── Controllers/          # Controladores MVC
│   ├── AccountController.cs    # Autenticación y registro
│   └── SitradController.cs     # Lógica principal del dashboard
├── Models/               # Modelos de datos
│   ├── User.cs                 # Modelo de usuario
│   └── CameraViewModel.cs      # Modelo de vista de cámaras
├── Services/             # Servicios de negocio
│   └── AuthService.cs          # Servicio de autenticación
├── Views/                # Vistas Razor
│   ├── Account/                # Vistas de autenticación
│   ├── Sitrad/                 # Vistas del dashboard
│   └── Shared/                 # Layouts compartidos
├── wwwroot/             # Archivos estáticos
│   ├── css/                    # Estilos
│   ├── js/                     # Scripts JavaScript
│   └── lib/                    # Librerías externas
├── appsettings.json      # Configuración (NO SUBIR A GIT)
├── appsettings.example.json  # Ejemplo de configuración
├── Program.cs            # Punto de entrada
└── README.md             # Este archivo
```

## 🔒 Seguridad

- Las contraseñas se almacenan con hash SHA256
- Los archivos de configuración con datos sensibles están excluidos del repositorio (ver `.gitignore`)
- Sistema de código de invitación para registro de operarios
- Validación de permisos en servidor y cliente

## 📝 Notas Importantes

⚠️ **IMPORTANTE**: 
- Nunca subas `appsettings.json` o `appsettings.Development.json` al repositorio
- Cambia todas las contraseñas por defecto antes de usar en producción
- El código de invitación debe ser seguro y conocido solo por administradores

## 🐛 Solución de Problemas

### Error: "No se puede copiar el archivo porque está en uso"
- Detén la aplicación anterior antes de compilar
- En PowerShell: `Get-Process -Name "SitradWebInterface" | Stop-Process -Force`

### Error: "SDK de .NET no admite el destino .NET X.0"
- Verifica que tengas instalado el SDK correcto: `dotnet --version`
- El proyecto está configurado para .NET 8.0

### No se muestran datos
- Verifica la configuración de la API en `appsettings.json`
- Revisa que las credenciales sean correctas
- Verifica la conectividad de red

## 📄 Licencia

Este proyecto fue desarrollado como parte de una tesis académica.

## 👥 Autor

Desarrollado para el proyecto de tesis.

---

**Versión**: 1.0.0  
**Última actualización**: 2024

