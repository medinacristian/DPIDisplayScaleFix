# DPIDisplayScaleFix

Prototipo para comprobar el refresco programático de la escala DPI de las pantallas internas de un ASUS Zenbook Duo.

## Requisitos

Windows x64 y .NET 8 SDK. Abre PowerShell en la raíz del repositorio (`C:\Users\Cristian\github\DPIDisplayScaleFix`).

## Diagnóstico

Compila y enumera las pantallas activas sin cambiar sus escalas:

```powershell
dotnet build -c Release
dotnet run -c Release
```

El programa identifica una o dos pantallas internas ASUS. Usa los tipos de salida integrados y el nombre de modelo `NB140B9M-T`: Windows puede reportar la segunda pantalla interna como una salida externa. No depende de `DISPLAY1` ni de la marca o cantidad de monitores externos.

Revisa que el diagnóstico muestre correctamente las pantallas y sus escalas antes de ejecutar el ciclo.

## Ciclo de refresco

```powershell
dotnet run -c Release -- --cycle
```

Para cada pantalla interna que no esté al 100%, el programa guarda su escala actual, la cambia temporalmente a 100%, espera 750 ms y restaura el valor guardado. Si todas las pantallas internas ya están al 100%, no hace nada. No modifica el registro ni cambia la escala de los monitores externos.

## Limitaciones

El cambio DPI usa los paquetes no documentados `DisplayConfigGetDeviceInfo` tipo `-3` y `DisplayConfigSetDeviceInfo` tipo `-4`. La enumeración y el ciclo manual se probaron con las dos pantallas internas ASUS, un monitor LG y un ARZOPA. El watcher automático de conexiones y el inicio con Windows todavía no están implementados.