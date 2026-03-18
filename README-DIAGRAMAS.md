# Diagramas del Proyecto

Este documento contiene los diagramas PlantUML que describen la arquitectura y funcionamiento del sistema Sitrad Web Interface.

## Diagramas Disponibles

### 1. `diagrama-arquitectura.puml`
Diagrama de arquitectura general del sistema que muestra:
- Capas de la aplicación (Presentación, Controladores, Servicios, Datos)
- Componentes principales
- Relaciones entre componentes
- Integración con API externa

**Cómo visualizar:**
- Usa un editor que soporte PlantUML (VS Code con extensión PlantUML, IntelliJ IDEA, etc.)
- O usa un servicio online como [PlantUML Web Server](http://www.plantuml.com/plantuml/uml/)

### 2. `diagrama-flujo-autenticacion.puml`
Diagrama de flujo que describe:
- Proceso de login
- Proceso de registro
- Validación de códigos de invitación
- Control de acceso por roles

### 3. `diagrama-clases.puml`
Diagrama de clases que muestra:
- Modelos de datos (User, CameraViewModel)
- Servicios (AuthService, IAuthService)
- Controladores (AccountController, SitradController)
- Relaciones entre clases

### 4. `diagrama-base-datos.puml`
Diagrama del modelo de base de datos:
- Estructura de la tabla Users
- Índices y restricciones
- Tipos de datos

## Visualización Rápida

### Opción 1: VS Code
1. Instala la extensión "PlantUML" de jebbs
2. Abre cualquier archivo `.puml`
3. Presiona `Alt+D` para previsualizar

### Opción 2: Online
1. Ve a http://www.plantuml.com/plantuml/uml/
2. Copia el contenido del archivo `.puml`
3. Pégalo en el editor online
4. El diagrama se generará automáticamente

### Opción 3: IntelliJ IDEA
1. Abre el archivo `.puml`
2. El IDE mostrará el diagrama automáticamente
3. O haz clic derecho → "Preview PlantUML Diagram"

## Incluir en Documentación

Para incluir estos diagramas en tu documentación de tesis:

1. **Exportar como imagen:**
   - En VS Code: Clic derecho en el diagrama → "Export Current Diagram"
   - Selecciona formato PNG o SVG

2. **Incluir en LaTeX:**
   ```latex
   \includegraphics[width=\textwidth]{diagrama-arquitectura.png}
   ```

3. **Incluir en Word:**
   - Inserta → Imagen → Selecciona el PNG exportado

## Personalización

Los diagramas están en formato PlantUML estándar. Puedes modificarlos editando los archivos `.puml` y ajustando:
- Colores y estilos
- Posicionamiento de elementos
- Información mostrada
- Relaciones entre componentes

