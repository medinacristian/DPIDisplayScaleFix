# DPIDisplayScaleFix

Prototipo para comprobar si Windows 11 refresca correctamente la escala DPI del panel ASUS mediante un ciclo programático de 100% → 200%. Los archivos del proyecto están directamente en la raíz del repositorio.

## Compilar y usar

Requiere Windows x64 y .NET 8 SDK. Abre PowerShell en la raíz del repositorio (`C:\CrisCode\github\DPIDisplayScaleFix`) y ejecuta:

```powershell
dotnet build -c Release
dotnet run -c Release
```

El modo inicial solo diagnostica. Confirma que una línea `INTERNA` corresponda al panel ASUS y que el programa informe `Coincidencia única del ASUS confirmada`. Si el identificador no aparece en la ruta del monitor, abortará intencionalmente.

Cuando el diagnóstico confirme la identidad del ASUS y su escala inicial de 200%, ejecutar:

```powershell
dotnet run -c Release -- --cycle
```

El programa intenta cambiar el origen ASUS a 100%, espera 750 ms, vuelve a enumerar las pantallas, cambia a 200% y consulta el resultado. No modifica el registro ni establece la escala de monitores externos. No ejecutar hasta confirmar primero la identidad en modo diagnóstico.

## Límites

El cambio usa los paquetes privados `DisplayConfigGetDeviceInfo` tipo `-3` y `DisplayConfigSetDeviceInfo` tipo `-4`. El API general está documentado, pero estos paquetes y sus estructuras no lo están. La implementación toma como referencia el proyecto abierto [SetDPI](https://github.com/imniko/SetDPI), y debe validarse en este equipo antes de agregar el watcher.

La tabla de escalas admitidas (100–500%) sigue el formato usado por SetDPI. El watcher, debounce, log e inicio automático quedan para etapas posteriores.
