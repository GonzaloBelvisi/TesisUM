# Guía de Configuración Inicial

Esta guía te ayudará a configurar el proyecto por primera vez.

## Paso 1: Configurar appsettings.json

1. Copia el archivo de ejemplo:
   ```bash
   cp appsettings.example.json appsettings.json
   ```

2. Edita `appsettings.json` y configura:

### AuthSettings
```json
"AuthSettings": {
  "OperarioInviteCode": "TU_CODIGO_SEGURO_AQUI",
  "AdminPassword": "CONTRASEÑA_SEGURA_ADMIN"
}
```

**Recomendaciones:**
- Usa un código de invitación fuerte (mínimo 8 caracteres, alfanumérico)
- Usa una contraseña segura para el admin (mínimo 8 caracteres)

### SitradApi
```json
"SitradApi": {
  "BaseUrl": "https://tu-api-url.com/api/v1",
  "Username": "tu_usuario_api",
  "Password": "tu_contraseña_api"
}
```

## Paso 2: Verificar .NET SDK

```bash
dotnet --version
```

Debe mostrar 8.0.x o superior.

## Paso 3: Restaurar Dependencias

```bash
dotnet restore
```

## Paso 4: Compilar y Ejecutar

```bash
dotnet build
dotnet run
```

## Paso 5: Primer Acceso

1. Abre el navegador en: `http://localhost:5220`
2. Inicia sesión con:
   - Usuario: `admin`
   - Contraseña: La que configuraste en `AuthSettings:AdminPassword`

## Paso 6: Crear Usuarios

### Crear un Operario

1. Ve a "Regístrate aquí"
2. Completa el formulario
3. Selecciona "Operario"
4. Ingresa el código de invitación configurado en `AuthSettings:OperarioInviteCode`

### Crear un Visualizador

1. Ve a "Regístrate aquí"
2. Completa el formulario
3. Selecciona "Visualizador"
4. No necesitas código de invitación

## Variables de Entorno (Alternativa)

En lugar de usar `appsettings.json`, puedes usar variables de entorno:

### Windows PowerShell
```powershell
$env:SITRAD_API_URL="https://tu-api.com/api/v1"
$env:SITRAD_API_USERNAME="usuario"
$env:SITRAD_API_PASSWORD="contraseña"
```

### Linux/Mac
```bash
export SITRAD_API_URL="https://tu-api.com/api/v1"
export SITRAD_API_USERNAME="usuario"
export SITRAD_API_PASSWORD="contraseña"
```

## Troubleshooting

### Error de compilación
- Verifica que tengas .NET 8.0 SDK instalado
- Ejecuta `dotnet clean` y luego `dotnet build`

### Error de conexión a la API
- Verifica que la URL de la API sea correcta
- Verifica las credenciales
- Revisa la conectividad de red

### No puedo iniciar sesión
- Verifica que hayas configurado `AuthSettings:AdminPassword` correctamente
- El usuario inicial es siempre `admin`

