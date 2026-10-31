# RecoWav

Grabador del **audio que suena en el PC** (loopback WASAPI del dispositivo de salida predeterminado) que lo guarda directamente como **MP3**. Windows Forms + [NAudio](https://github.com/naudio/NAudio), .NET 10.

- Ventana gráfica con medidor de nivel y progreso, o modo consola.
- Duración fija: segundos (`90`), `mm:ss` o `hh:mm:ss`. `Ctrl+C` detiene antes y conserva lo grabado.
- Bitrate configurable: 128, 192, 256 o 320 kbps (192 por defecto).
- Salida por defecto en `Música\Grabacion_<fecha>.mp3`.
- Reproduce silencio en segundo plano para que WASAPI no corte la captura cuando no suena nada.

## Uso

```bat
RecoWav.bat                                  :: ventana gráfica
RecoWav.bat -c                               :: consola interactiva
RecoWav.bat -d 1:30 -o cancion.mp3 -q 320    :: 1 min 30 s a 320 kbps
RecoWav.bat -h                               :: ayuda
```

Compilar y publicar (requiere .NET 10 SDK):

```bash
dotnet publish -c Release -o publish
```

No genera `.exe` propio: se lanza con `dotnet RecoWav.dll` (`dotnet.exe` está firmado por Microsoft), lo que evita falsos positivos de antivirus.
