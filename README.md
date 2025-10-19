## Servidor Web Simple (PSR)

Servidor HTTP minimalista en C# usando sockets crudos (sin frameworks HTTP), cumpliendo los requisitos del trabajo final.

### Requisitos implementados
- Concurrencia por conexión (cada solicitud se maneja en un `Task`).
- Servir `index.html` por defecto si no se especifica archivo.
- Carpeta de archivos y puerto configurables vía `serverconfig.json` externo.
- 404 con documento personalizado `archivos-a-servir/404.html`.
- Soporte para métodos GET y POST. Para POST se loguea el cuerpo recibido.
- Manejo y log de parámetros de consulta (query string).
- Compresión GZip opcional por `Accept-Encoding: gzip`.
- Log diario en carpeta `logs/` con IP de origen y datos.
- Parser HTTP propio y sockets (`TcpListener`/`TcpClient`).

### Estructura
- `ServerCore/`: lógica de servidor, parser, logger, respuestas, config.
- `ServerApp/`: ejecutable del servidor (entry point) y `serverconfig.json` de ejemplo.
- `archivos-a-servir/`: estáticos por defecto (`index.html`, `404.html`).
- `TestHarness/`: mini test de integración que arranca servidor y hace GET/POST.

### Configuración (`serverconfig.json`)
```json
{
  "port": 8080,
  "webRoot": "archivos-a-servir",
  "logDirectory": "logs",
  "enableGzip": true
}
```
- `webRoot` y `logDirectory` pueden ser rutas relativas al archivo de config o absolutas.
- El servidor crea directorios si no existen.

### Ejecutar servidor
```bash
dotnet run --project .\ServerApp\ServerApp.csproj -- serverconfig.json
```
- Si el archivo de configuración no existe, el `ServerApp` creará uno por defecto.
- Accede a `http://127.0.0.1:8080/`.

### Probar (harness de integración)
```bash
dotnet run --project .\TestHarness\TestHarness.csproj
```
Muestra el estado de respuestas para: `/`, `/nope.txt`, `/?a=1&b=dos`, y `POST /`.

### Logs
- Se generan en `logs/YYYY-MM-DD.log` (ruta relativa al archivo de configuración activo).
- Incluyen: IP origen, método, ruta, query y, en POST, cuerpo (form-data como texto, otros como Base64).

### Notas
- Previene traversal de rutas fuera de `webRoot`.
- Resuelve tipos MIME comunes.
- Timeout de lectura/escritura para evitar conexiones colgadas.


