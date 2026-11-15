namespace GoruntuIslemeOdevleri;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 3 && args[0] == "--batch")
            return BatchRunner.Run(args[1], args[2]);

        Application.Run(new MainForm());
        return 0;
    }
}
