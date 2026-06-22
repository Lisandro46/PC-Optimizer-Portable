# PC Optimizer Portable — Modo experto

Aplicación portable para inventariar aplicaciones y componentes de Windows, explicar su función y preparar un plan explícito antes de desinstalar o desactivar.

También mide en vivo el consumo atribuible a cada aplicación: CPU, memoria RAM, actividad de disco y cantidad de procesos. La lectura se actualiza automáticamente cada 10 segundos.

Todos los encabezados útiles permiten ordenar la tabla. Un clic aplica el orden inicial y el siguiente invierte entre ascendente y descendente. Las columnas de consumo y riesgo se ordenan por su valor real; `N/D` permanece al final.

## Descargar versión portable

**[Descargar PC Optimizer Portable v1.2.0](https://github.com/Lisandro46/PC-Optimizer-Portable/raw/main/downloads/PCOptimizerPortable-v1.2.0.zip)**

Extraé el ZIP y ejecutá `PCOptimizerPortable.exe` como administrador. Windows puede mostrar una advertencia de SmartScreen porque el ejecutable todavía no tiene firma digital.

## Uso

1. Descargá y extraé el ZIP de la versión portable.
2. Abrí `PCOptimizerPortable.exe` y aceptá el permiso de administrador.
3. Esperá el inventario completo. Las consultas de características de Windows pueden tardar varios minutos.
4. Usá `?` para entender cada elemento y su riesgo.
5. Marcá exclusivamente lo que quieras cambiar y elegí `Desinstalar` o `Desactivar`.
6. Presioná **REVISAR PLAN**. La aplicación mostrará el método y la acción exacta antes de pedir la confirmación escrita.

Nada aparece seleccionado al iniciar. Las acciones no compatibles se muestran en el plan y se omiten.

## Qué detecta

- Programas tradicionales, incluidos registros ocultos del sistema.
- Aplicaciones AppX/MSIX de Microsoft Store para todos los usuarios.
- Aplicaciones preinstaladas que Windows entrega a cuentas nuevas.
- Características opcionales de Windows.
- Capacidades de Windows.
- Microsoft Edge como componente especial, con desinstalación experta o desactivación de precarga y segundo plano.

## Consumo de recursos

- `CPU ahora`, `RAM`, `Disco` y `Procesos` suman los procesos que pueden relacionarse de forma confiable con la carpeta o ejecutable de cada aplicación.
- Una aplicación cerrada muestra cero.
- `N/D` indica que el componente no tiene un proceso propio identificable o comparte procesos internos de Windows; no se inventa una atribución dudosa.
- Las cifras son una foto del momento y pueden variar entre mediciones.

## Seguridad y límites

- El modo experto permite intentar quitar paquetes que Windows marca como protegidos. Windows todavía puede rechazar la operación.
- Desactivar Edge evita su precarga y ejecución en segundo plano; no garantiza que Windows deje de invocarlo en funciones internas.
- Las actualizaciones de Windows pueden reinstalar ciertos componentes.
- Un punto de restauración depende de que Protección del sistema esté habilitada.
- El inventario, el plan aprobado y los resultados quedan en `PCOptimizer-Logs`, junto al ejecutable o en Documentos si esa carpeta no es escribible.
- La desinstalación puede borrar preferencias y datos propios del programa. Conservá una copia de tus archivos importantes.

## Portabilidad

El ejecutable no requiere instalación. Está compilado para Windows 10/11 de 64 bits y usa .NET Framework 4.x, incluido normalmente en esas versiones de Windows.

## Compilar nuevamente

En PowerShell, desde esta carpeta:

```powershell
.\build.ps1
```
