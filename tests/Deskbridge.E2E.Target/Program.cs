namespace Deskbridge.E2E.Target;

/// <summary>
/// A window for end-to-end tests that opens without taking focus and holds known text.
/// Arguments: --title t (required), --text t, --password, --big n, --hang-ms n, --hidden.
/// </summary>
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var options = TargetOptions.Parse(args);
        ApplicationConfiguration.Initialize();
        var form = new TargetForm(options);
        if (options.Hidden)
        {
            _ = form.Handle; // the window exists but is never shown
            Application.Run();
        }
        else
        {
            Application.Run(form);
        }
    }
}

sealed record TargetOptions(string Title, string Text, bool Password, int Big, int HangMs, bool Hidden)
{
    public static TargetOptions Parse(string[] args)
    {
        string? Value(string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        return new TargetOptions(
            Value("--title") ?? throw new ArgumentException("--title is required"),
            Value("--text") ?? "",
            args.Contains("--password"),
            int.Parse(Value("--big") ?? "0"),
            int.Parse(Value("--hang-ms") ?? "0"),
            args.Contains("--hidden"));
    }
}
