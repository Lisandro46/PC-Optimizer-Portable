# PC Optimizer Portable

Optimizador de Windows todo-en-uno, portable y de modo experto. Inventaría, diagnostica y optimiza el equipo desde una sola app, sin instalar nada: el runtime de .NET viene incluido dentro del `.exe`.

> **v2.0.0** — reescrito en .NET 8 + WPF con interfaz de panel (sidebar + dashboard) y 8 módulos. Las versiones 1.x eran un desinstalador WinForms; esta versión lo amplía a un optimizador completo.

## Descargar

El portable es un único `PCOptimizer.exe` (self-contained, ~63 MB). Ejecutalo como administrador. Windows puede mostrar una advertencia de SmartScreen porque el ejecutable todavía no tiene firma digital.

## Módulos

| Módulo | Qué hace |
|---|---|
| **Dashboard** | Score de salud + RAM, disco, apps al inicio y uptime reales. Accesos de optimización rápida. |
| **Aplicaciones** | Inventario completo (programas, Store/AppX, preinstaladas, características y capacidades de Windows, Edge) con consumo en vivo (CPU/RAM/disco). Desinstalar o desactivar con plan + confirmación + punto de restauración + logs. |
| **Arranque** | Apps que arrancan con Windows. Habilitar/deshabilitar (reversible vía `StartupApproved`). |
| **Limpieza** | Temporales, caché de Windows Update, papelera, miniaturas, volcados de error y prefetch. Calcula el espacio recuperable real antes de borrar. |
| **Servicios** | Servicios en Automático; pasarlos a Manual y volver (reversible). |
| **Privacidad** | Tweaks reversibles: telemetría, ID de publicidad, historial de actividad, sugerencias, Bing en Inicio. |
| **Rendimiento** | Plan de energía, efectos visuales, apps en segundo plano, retraso de inicio, transparencias. |
| **Salud** | SMART de discos, reinicio pendiente, y análisis de integridad con DISM / reparación con SFC. |

## Seguridad y diseño

- **Nada se ejecuta ni se selecciona solo.** Cada cambio se confirma.
- Cada elemento muestra **nivel de riesgo** (BAJO/MEDIO/ALTO/CRÍTICO) con explicación.
- Arranque, Servicios, Privacidad y Rendimiento son **reversibles** desde la app.
- La desinstalación de programas **sí** es definitiva (puede borrar configuración y datos).
- Inventario, plan aprobado y resultados quedan en `PCOptimizer-Logs`, junto al `.exe` o en Documentos.
- **Cero dependencias externas**: todo sale de APIs nativas de Windows y de PowerShell. Sin paquetes NuGet de terceros.

## Estructura del proyecto

```
app/
├── PCOptimizer.csproj      # .NET 8 (net8.0-windows), WPF
├── app.manifest            # requireAdministrator + PerMonitorV2
├── App.xaml(.cs)           # arranque, chequeo de admin, --selftest
├── Theme/Styles.xaml       # paleta y estilos (dark)
├── MainWindow.xaml(.cs)    # shell: sidebar + navegación
├── Models/                 # AppItem y modelos de operación
├── Services/               # Inventory, Action, ResourceMonitor, SystemInfo,
│                           # Cleanup, Startup, Services, Tweaks, Health, Log, PowerShellRunner
└── Views/                  # Dashboard, Apps, Cleanup, Startup, Services, Tweaks, Health, Plan
```

## Requisitos

Windows 10/11 de 64 bits. El portable no requiere instalar nada. Para **compilar** hace falta el SDK de .NET 8.

## Compilar

Con el SDK de .NET 8 instalado (`winget install Microsoft.DotNet.SDK.8`):

```powershell
# Desarrollo
dotnet build app/PCOptimizer.csproj

# Portable self-contained (deja el .exe en release/PCOptimizerPortable-vX.Y.Z)
.\build.ps1
```
