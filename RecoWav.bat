@echo off
rem Lanza RecoWav con dotnet.exe (firmado por Microsoft).
rem   Sin argumentos   -> ventana grafica
rem   Con argumentos   -> modo consola (RecoWav.bat -h para ver la ayuda)
dotnet "%~dp0RecoWav.dll" %*
