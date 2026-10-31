using NAudio.MediaFoundation;

namespace RecoWav;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        MediaFoundationApi.Startup();

        if (args.Length > 0)
            return ConsoleMode.Run(args);

        Console.WriteLine("RecoWav: abriendo la ventana. Cierra la ventana para salir.");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
