Set shell = CreateObject("WScript.Shell")
exe = "C:\Users\Cristian\github\DPIDisplayScaleFix\bin\Release\net10.0-windows\win-x64\publish\AsusDpiProbe.exe"
shell.Run """" & exe & """ --watch", 0, False
