# DPIDisplayScaleFix

Herramienta para diagnosticar y corregir problemas de escala DPI en las pantallas internas de un ASUS Zenbook Duo cuando Windows no actualiza correctamente la escala después de conectar, desconectar o cambiar monitores.

El programa detecta las pantallas internas ASUS y refresca su escala mediante un ciclo temporal a 100%, para luego restaurar el porcentaje que cada pantalla tenía al comenzar. Los monitores externos pueden ser de distintas marcas y no reciben cambios de escala.

## Características

- Diagnóstico de pantallas activas, tipo, ruta y escala actual.
- Ciclo manual para refrescar la escala de las pantallas internas.
- Vigilancia de cambios de topología y espera hasta que la configuración permanezca estable durante cinco segundos.
- Compatibilidad con una o dos pantallas internas ASUS y cero o más monitores externos.
- Registro de eventos con rotación automática.
- Pruebas automatizadas para la lógica y las estructuras de interoperabilidad de Windows.

## Tecnologías

- C# y .NET 10.
- Windows x64 y API de configuración de pantalla de Windows mediante P/Invoke.
- xUnit para pruebas automatizadas.

## Requisitos

Windows x64 y .NET 10 SDK. Clona el repositorio y abre PowerShell en la carpeta local donde quedó el proyecto. Ejecuta los comandos de esta guía desde la raíz del repositorio.

NuGet debe estar accesible para restaurar los paquetes durante la primera compilación o al ejecutar las pruebas.

## Compilar y diagnosticar

```powershell
dotnet build DPIDisplayScaleFix.sln -c Release
dotnet run --project AsusDpiProbe.csproj -c Release
```

El diagnóstico enumera las pantallas activas y sus escalas sin cambiarlas. El programa identifica las pantallas internas ASUS por la tecnología de salida o por el prefijo `NB140B9M-T`; no depende del nombre de los monitores externos.

## Ciclo manual

```powershell
dotnet run --project AsusDpiProbe.csproj -c Release -- --cycle
```

Para cada pantalla interna activa distinta de 100%, guarda su escala, la cambia temporalmente a 100%, espera 750 ms y restaura el valor guardado. Al final verifica el resultado. No modifica la escala de las pantallas externas.

## Vigilancia de cambios

```powershell
dotnet run --project AsusDpiProbe.csproj -c Release -- --watch
```

La vigilancia hace un ciclo al iniciar. Después revisa la configuración cada medio segundo y ejecuta otro ciclo cuando la topología permanece estable durante cinco segundos. La consola debe seguir abierta; Ctrl+C la detiene. Si otra instancia ya está activa, una segunda instancia se cierra.

Para usarla sin una consola, publica el ejecutable independiente y abre `IniciarVigilancia.vbs`:

```powershell
dotnet publish AsusDpiProbe.csproj -c Release -r win-x64 --self-contained true
```

El lanzador ejecuta el archivo publicado desde `bin\Release\net10.0-windows\win-x64\publish`. Para iniciar la vigilancia al entrar a Windows, crea un acceso directo a `IniciarVigilancia.vbs` en la carpeta que abre `shell:startup`.

El registro se guarda en `%LOCALAPPDATA%\DPIDisplayScaleFix\watch.log`. Al superar 2 MB, el registro anterior se conserva como `watch.log.1`.

## Pruebas

Ejecuta toda la suite en Windows x64:

```powershell
dotnet test DPIDisplayScaleFix.sln -c Release
```

`DPIDisplayScaleFix.Core.Tests` prueba la lógica sin llamar a APIs de Windows: conversión de escala, clasificación y deduplicación de pantallas, ciclo y recuperación, argumentos, reintentos acotados y estabilidad de topología con reloj simulado.

`DPIDisplayScaleFix.Interop.Tests` comprueba tamaños y offsets de las estructuras P/Invoke usadas por Windows. Debe ejecutarse en Windows x64.

## Validación manual de topologías

Antes de confiar en la vigilancia en un nuevo equipo o configuración, comprueba estas combinaciones y revisa el registro:

| Pantallas internas ASUS activas | Monitores externos | Resultado esperado |
| --- | --- | --- |
| 1 | Ninguno | Conserva la escala interna |
| 1 | 1 LG | Refresca solo la pantalla interna al estabilizarse la conexión |
| 1 | 2 LG | Refresca solo la pantalla interna después del cambio completo |
| 2 | Ninguno | Conserva/restaura por separado las dos escalas internas |
| 2 | 1 ARZOPA | Refresca las dos internas; conserva la externa |
| 2 | 3 externos de distintas marcas | Refresca las dos internas; conserva los externos |

En cada caso confirma que el ciclo restaure los porcentajes originales, que ninguna pantalla externa reciba llamadas de cambio DPI y que una desconexión/reconexión active un único ciclo después de cinco segundos estables.

## Estructura del código

- `Program.cs`: valida argumentos y selecciona el modo.
- `Core/`: lógica independiente de Windows para escalas, clasificación, topología, refresco y reintentos.
- `DisplayConfigurationService.cs`: adaptador P/Invoke a la API de configuración de pantalla.
- `DisplayWatcher.cs`: proceso de vigilancia, bloqueo de instancias y registro.
- `WatchLog.cs`: salida con fecha y hora y rotación del registro.
- `tests/DPIDisplayScaleFix.Core.Tests/`: pruebas de lógica pura.
- `tests/DPIDisplayScaleFix.Interop.Tests/`: pruebas ABI para Windows x64.

## Limitaciones

El ajuste DPI se basa en los paquetes no documentados `DisplayConfigGetDeviceInfo` tipo `-3` y `DisplayConfigSetDeviceInfo` tipo `-4`. El comportamiento se desarrolló para resolver un problema observado en un ASUS Zenbook Duo; conviene validar el resultado en cada equipo y configuración de pantallas antes de dejar activa la vigilancia. Las pruebas automatizadas validan la lógica y el diseño de memoria; no sustituyen las pruebas físicas de conexión y desconexión de monitores.
