namespace GoruntuIslemeOdevleri;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 3 && args[0] == "--batch")
            return BatchRunner.Run(args[1], args[2]);
        if (args.Length == 3 && args[0] == "--coineval")
            return CoinEvaluation.Run(args[1], args[2]);
        if (args.Length == 2 && args[0] == "--coinsynthetic")
            return CoinEvaluation.RunSynthetic(args[1]);

        Application.Run(new MainForm());
        return 0;
    }
}
