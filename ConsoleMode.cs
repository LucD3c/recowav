using System.Globalization;

namespace RecoWav;

/// <summary>Grabación desde la línea de comandos, sin ventana.</summary>
static class ConsoleMode
{
    private const string Usage = """
        Uso:
          RecoWav.bat                      Abre la ventana gráfica
          RecoWav.bat -c                   Modo consola: pregunta la duración
          RecoWav.bat -d <duración> [-o <archivo.mp3>] [-q <kbps>]

        Opciones:
          -d, --duracion   Duración: segundos (90), mm:ss (1:30) o hh:mm:ss (1:00:00)
          -o, --salida     Archivo MP3 de salida (por defecto: Música\Grabacion_fecha.mp3)
          -q, --calidad    Bitrate en kbps: 128, 192, 256 o 320 (por defecto 192)
          -c, --consola    Modo consola interactivo
          -h, --ayuda      Muestra esta ayuda

        Pulsa Ctrl+C durante la grabación para detenerla antes y guardar lo grabado.
        """;

    public static int Run(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        TimeSpan? duration = null;
        string? output = null;
        int bitrate = 192;

        try
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-d": case "--duracion":
                        duration = ParseDuration(Next(args, ref i)); break;
                    case "-o": case "--salida":
                        output = Next(args, ref i); break;
                    case "-q": case "--calidad":
                        bitrate = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture); break;
                    case "-c": case "--consola":
                        break;
                    case "-h": case "--ayuda": case "/?":
                        Console.WriteLine(Usage); return 0;
                    default:
                        throw new ArgumentException($"Opción desconocida: {args[i]}");
                }
            }
            if (bitrate is not (128 or 192 or 256 or 320))
                throw new ArgumentException("La calidad debe ser 128, 192, 256 o 320.");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            Console.WriteLine(Usage);
            return 2;
        }

        while (duration is null || duration <= TimeSpan.Zero)
        {
            Console.Write("Duración (segundos, mm:ss o hh:mm:ss): ");
            string? line = Console.ReadLine();
            if (line == null) return 2;
            try { duration = ParseDuration(line); }
            catch (FormatException) { Console.WriteLine("  Formato no válido."); }
        }

        output ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            $"Grabacion_{DateTime.Now:yyyyMMdd_HHmmss}.mp3");
        if (!output.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) output += ".mp3";
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        return Record(duration.Value, output, bitrate);
    }

    private static int Record(TimeSpan duration, string output, int bitrate)
    {
        using var recorder = new LoopbackRecorder();
        using var finished = new ManualResetEventSlim();
        Exception? captureError = null;
        float level = 0;

        recorder.LevelChanged += l => level = l;
        recorder.CaptureFinished += ex => { captureError = ex; finished.Set(); };

        ConsoleCancelEventHandler onCancel = (_, e) => { e.Cancel = true; recorder.Stop(); };
        Console.CancelKeyPress += onCancel;

        Console.WriteLine($"Grabando el audio del sistema durante {duration:hh\\:mm\\:ss}  (Ctrl+C para detener)");
        recorder.Start(duration);

        while (!finished.Wait(250))
            PrintProgress(recorder.Recorded, duration, level);
        PrintProgress(recorder.Recorded, duration, 0);
        Console.WriteLine();
        Console.CancelKeyPress -= onCancel;

        if (captureError != null)
        {
            Console.Error.WriteLine("La grabación se interrumpió: " + captureError.Message);
            return 1;
        }
        if (recorder.Recorded <= TimeSpan.Zero)
        {
            Console.Error.WriteLine("No se grabó nada.");
            return 1;
        }

        Console.WriteLine("Convirtiendo a MP3...");
        try
        {
            recorder.SaveMp3(output, bitrate);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("No se pudo guardar el MP3: " + ex.Message);
            return 1;
        }
        Console.WriteLine($"Guardado: {output}");
        return 0;
    }

    private static void PrintProgress(TimeSpan recorded, TimeSpan total, float level)
    {
        int pct = (int)Math.Clamp(recorded.TotalMilliseconds / total.TotalMilliseconds * 100, 0, 100);
        string bar = new string('#', pct / 5).PadRight(20, '-');
        string vu = new string('|', (int)(level * 10)).PadRight(10);
        Console.Write($"\r  [{bar}] {pct,3}%  {recorded:hh\\:mm\\:ss} / {total:hh\\:mm\\:ss}  nivel [{vu}]");
    }

    private static string Next(string[] args, ref int i) =>
        ++i < args.Length ? args[i] : throw new ArgumentException($"Falta el valor de {args[i - 1]}");

    private static TimeSpan ParseDuration(string text)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int secs))
            return TimeSpan.FromSeconds(secs);

        string[] parts = text.Split(':');
        if (parts.Length is 2 or 3 && parts.All(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            int[] n = parts.Select(p => int.Parse(p, CultureInfo.InvariantCulture)).ToArray();
            return parts.Length == 2
                ? new TimeSpan(0, n[0], n[1])
                : new TimeSpan(n[0], n[1], n[2]);
        }
        throw new FormatException($"Duración no válida: \"{text}\"");
    }
}
