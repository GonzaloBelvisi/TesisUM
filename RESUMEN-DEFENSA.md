# Resumen Ejecutivo - Sistema de Monitoreo IoT
## Cómo Funciona el Sistema - Para Defensa de Tesis

---

## 🎯 ¿QUÉ ES EL PROYECTO?

**Sistema web de monitoreo en tiempo real** de 30 cámaras frigoríficas donde se maduran bananas en Almar SRL. 

**Problema que resuelve:** Antes, los operarios tenían que ir físicamente a cada cámara para ver las lecturas en pantallas locales (HMI). Ahora, todo se centraliza en un dashboard web accesible desde cualquier dispositivo.

---

## 🏗️ ARQUITECTURA: TRES CAPAS

### **CAPA 1: Hardware (Sensores y Controladores)**
- **30 cámaras frigoríficas** con sensores de temperatura, humedad, evaporador
- **60 sensores NUONUO** (30 para CO₂, 30 para Etileno)
- **Controladores Sitrad** que concentran los datos

**¿Qué hace?** Los sensores miden las variables físicas y los controladores las envían a APIs.

---

### **CAPA 2: Backend (ASP.NET Core MVC)**

**Componentes principales:**

1. **SitradController:**
   - Obtiene datos de las cámaras desde la API de Sitrad PRO
   - Permite modificar setpoints (SET1 y SET3) para controlar temperatura
   - Valida permisos: solo operarios pueden modificar

2. **AccountController:**
   - Maneja login, registro y logout
   - Gestiona sesiones de usuario

3. **AuthService:**
   - Registra usuarios nuevos
   - Valida credenciales (hash SHA256)
   - Controla códigos de invitación para operarios

4. **Base de Datos SQLite:**
   - Almacena usuarios y sus roles
   - Se crea automáticamente al iniciar

**¿Qué hace?** Consume las APIs externas (Sitrad y NUONUO), combina los datos, aplica reglas de negocio y los prepara para mostrar.

---

### **CAPA 3: Frontend (Dashboard Web)**

**Componentes:**
- **Vistas Razor** (HTML generado en el servidor)
- **jQuery** para actualización automática cada 5 segundos
- **Tailwind CSS** para diseño responsive (móvil y escritorio)

**¿Qué hace?** Muestra una tabla con todas las cámaras y sus variables, actualiza automáticamente, y permite modificar setpoints (solo operarios).

---

## 🔄 FLUJO DE FUNCIONAMIENTO

### **1. Flujo de Datos (Lectura de Sensores)**

```
Sensores → Controladores → APIs Externas → Backend → Frontend → Usuario
```

**Paso a paso:**
1. Los sensores físicos miden temperatura, humedad, CO₂, etc.
2. Los controladores Sitrad/NUONUO envían estos datos a sus APIs
3. El backend (SitradController) consulta las APIs cada vez que se carga el dashboard
4. Los datos se combinan en un modelo unificado por cámara
5. El frontend muestra la tabla y la actualiza automáticamente cada 5 segundos

---

### **2. Flujo de Autenticación**

```
Usuario → Login → AccountController → AuthService → SQLite → Sesión → Dashboard
```

**Paso a paso:**
1. Usuario accede a la aplicación → redirigido a login
2. Ingresa usuario y contraseña
3. AccountController llama a AuthService
4. AuthService verifica en SQLite (contraseña con hash SHA256)
5. Si es válido, se crea una sesión con el ID y rol del usuario
6. Redirección al dashboard con permisos según el rol

---

### **3. Flujo de Modificación de Setpoints**

```
Operario → Dashboard → Ingresa nuevo valor → SitradController → API Sitrad → Verificación
```

**Paso a paso:**
1. Operario (no visualizador) ingresa nuevo valor de SET1 o SET3
2. JavaScript envía la petición al backend
3. SitradController valida que el usuario sea Operario
4. Construye el payload JSON y lo envía a la API de Sitrad
5. Sistema de reintentos verifica que el cambio se aplicó
6. Toast de confirmación al usuario

---

## 👥 SISTEMA DE ROLES

### **Operario:**
- ✅ Puede ver todas las cámaras
- ✅ Puede modificar setpoints (SET1, SET3)
- ✅ Requiere código de invitación para registrarse

### **Visualizador:**
- ✅ Puede ver todas las cámaras
- ❌ NO puede modificar setpoints (controles deshabilitados)
- ✅ No requiere código de invitación

**¿Por qué?** Para evitar que cualquier persona pueda modificar parámetros críticos del proceso de maduración.

---

## 🔐 SEGURIDAD

1. **Contraseñas:** Hash SHA256 (no se almacenan en texto plano)
2. **Sesiones:** Cookies HTTP-only (protección contra XSS)
3. **Código de invitación:** Solo quien conoce el código puede registrarse como operario
4. **Validación doble:** Permisos verificados en servidor Y cliente
5. **HTTPS:** Exposición a Internet mediante NGROK con encriptación

---

## 📊 VARIABLES MONITOREADAS

Por cada cámara se muestran:
- **Temperatura de aire** (Sitrad)
- **Temperatura de pulpa** (Sitrad) - Variable crítica
- **Humedad relativa** (Sitrad)
- **Estado del evaporador** (Sitrad)
- **CO₂** (NUONUO)
- **Etileno** (NUONUO)
- **SET1 y SET3** (Setpoints de temperatura - Sitrad)

---

## 🎨 CARACTERÍSTICAS DEL DASHBOARD

1. **Actualización automática:** Cada 5 segundos sin recargar la página
2. **Semáforo de temperatura de pulpa:** Colores según umbrales (amarillo/naranja/rojo)
3. **Responsive:** Funciona en móvil y escritorio
4. **Toasts de notificación:** Confirmaciones visuales de cambios
5. **Sistema de reintentos:** Si falla una actualización, reintenta automáticamente

---

## 🛠️ TECNOLOGÍAS UTILIZADAS

**Backend:**
- ASP.NET Core 8.0 MVC
- Entity Framework Core (ORM)
- SQLite (base de datos local)
- HttpClient (consumo de APIs)

**Frontend:**
- Razor Views (HTML dinámico)
- jQuery (AJAX y manipulación DOM)
- Tailwind CSS (diseño responsive)

**Seguridad:**
- SHA256 (hash de contraseñas)
- Sesiones HTTP
- Código de invitación

---

## 💡 PUNTOS CLAVE PARA LA DEFENSA

### **1. Problema Resuelto:**
"Centralizamos el monitoreo que antes era manual y distribuido, permitiendo acceso remoto y decisiones más rápidas."

### **2. Arquitectura:**
"Tres capas claramente separadas: hardware, backend y frontend. Esto permite escalar y mantener el sistema fácilmente."

### **3. Seguridad:**
"Implementamos autenticación con roles, hash de contraseñas, y código de invitación para controlar quién puede modificar parámetros críticos."

### **4. Integración:**
"Integramos dos APIs diferentes (Sitrad y NUONUO) en un modelo unificado, normalizando formatos y unidades."

### **5. Usabilidad:**
"Dashboard responsive que se actualiza automáticamente, con semáforos visuales para interpretación rápida del estado."

### **6. Persistencia:**
"SQLite local para usuarios, sin necesidad de servidor de base de datos externo, simplificando el despliegue."

---

## 🚀 FUNCIONALIDADES IMPLEMENTADAS

✅ Dashboard en tiempo real  
✅ Autenticación con roles (Operario/Visualizador)  
✅ Modificación de setpoints (solo operarios)  
✅ Integración con APIs de Sitrad y NUONUO  
✅ Semáforo de temperatura de pulpa  
✅ Sistema de reintentos automáticos  
✅ Persistencia de usuarios en SQLite  
✅ Diseño responsive  

---

## 📈 FUNCIONALIDADES PLANIFICADAS

⏳ Historial de lecturas (logs)  
⏳ Agregaciones estadísticas (mín/máx/promedio)  
⏳ Sistema de alertas avanzado  
⏳ Reportes exportables (PDF, Excel)  
⏳ Buffer local para cortes de red  

---

## 🎯 CONCLUSIÓN PARA LA DEFENSA

"Desarrollé un sistema web que centraliza el monitoreo de 30 cámaras frigoríficas, integrando datos de múltiples fuentes (Sitrad y NUONUO) en un dashboard accesible desde cualquier dispositivo. El sistema implementa autenticación robusta con control de acceso por roles, permitiendo que solo operarios autorizados modifiquen parámetros críticos. La arquitectura de tres capas garantiza escalabilidad y mantenibilidad, mientras que el uso de SQLite simplifica el despliegue sin requerir infraestructura compleja."

---

## 📝 PREGUNTAS FRECUENTES (Para preparar la defensa)

**P: ¿Por qué SQLite y no una base de datos más robusta?**  
R: Para simplificar el despliegue. SQLite es un archivo local, no requiere servidor de BD, y es suficiente para el volumen de usuarios y futuros logs. Si crece, se puede migrar fácilmente.

**P: ¿Cómo se manejan los cortes de red?**  
R: Implementamos sistema de reintentos exponenciales. El buffer local está planificado para almacenar lecturas pendientes y reenviarlas cuando se restablezca la conexión.

**P: ¿Por qué dos APIs diferentes?**  
R: Sitrad PRO maneja temperatura/humedad, pero NUONUO es especialista en sensores de gases (CO₂/Etileno). Integramos ambas en un modelo unificado.

**P: ¿Cómo garantizan la seguridad?**  
R: Hash SHA256 de contraseñas, sesiones HTTP-only, código de invitación para operarios, validación de permisos en servidor y cliente, y HTTPS mediante NGROK.

**P: ¿Qué pasa si un operario modifica un setpoint incorrectamente?**  
R: El sistema valida el formato numérico y verifica que el cambio se aplicó correctamente mediante reintentos. Los logs futuros permitirán auditoría de cambios.

---

**¡Éxito en tu defensa! 🎓**

