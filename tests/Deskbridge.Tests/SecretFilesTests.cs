using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Deskbridge.Core;
using Deskbridge.Engines.Os.Files;

namespace Deskbridge.Tests;

public sealed class SecretFilesTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "deskbridge-files-" + Guid.NewGuid().ToString("N")[..8]);

    public SecretFilesTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        foreach (var junction in Directory.GetDirectories(dir).Where(d => new DirectoryInfo(d).LinkTarget is not null))
        {
            Directory.Delete(junction); // the link only, never what it points to
        }

        Directory.Delete(dir, recursive: true);
    }

    [Theory]
    [InlineData(@"C:\code\app\.env", true)]
    [InlineData(@"C:\code\app\.env.local", true)]
    [InlineData(@"C:\keys\server.pem", true)]
    [InlineData(@"C:\keys\server.KEY", true)]
    [InlineData(@"C:\certs\me.pfx", true)]
    [InlineData(@"C:\certs\me.p12", true)]
    [InlineData(@"C:\Users\me\.ssh\id_rsa", true)]
    [InlineData(@"C:\Users\me\.ssh\id_ed25519.pub", true)]
    [InlineData(@"C:\vault\passwords.kdbx", true)]
    [InlineData(@"C:\code\aws-credentials.json", true)]
    [InlineData(@"C:\code\client_secret.json", true)]
    [InlineData(@"C:\code\secrets\db.txt", true)]
    [InlineData(@"C:\Users\me\AppData\Roaming\Microsoft\Credentials\1A2B", true)]
    [InlineData(@"C:\notes\notes.txt", false)]
    [InlineData(@"C:\notes\environment.md", false)]
    [InlineData(@"C:\code\keyboard.cs", false)]
    public void Recognises_secret_files(string path, bool secret) => Assert.Equal(secret, SecretFiles.IsSecret(path));

    [Fact]
    public void An_alternate_data_stream_is_refused()
    {
        File.WriteAllText(Path.Combine(dir, ".env"), "TOKEN=1");
        File.WriteAllText(Path.Combine(dir, "notes.txt"), "hello");

        // "::$DATA" names the file's main stream, so without a check this reads the .env file.
        Assert.ThrowsAny<DeskbridgeException>(() => FileTools.Read(Path.Combine(dir, ".env::$DATA"), 1_000));
        Assert.ThrowsAny<DeskbridgeException>(() => FileTools.Read(Path.Combine(dir, "notes.txt:hidden"), 1_000));
        Assert.ThrowsAny<DeskbridgeException>(() => FileTools.Write(Path.Combine(dir, "notes.txt:hidden"), "x", WriteMode.Create));
    }

    [Fact]
    public void A_short_name_of_a_secret_file_is_refused()
    {
        var secret = Path.Combine(dir, "credentials.json");
        File.WriteAllText(secret, "{}");
        var shortPath = ShortPath(secret); // like CREDEN~1.JSO where the volume keeps 8.3 names

        Assert.Throws<SecretFileException>(() => FileTools.Read(shortPath, 1_000));
        Assert.Throws<SecretFileException>(() => FileTools.Move(shortPath, Path.Combine(dir, "plain.json")));
    }

    [Fact]
    public void A_folder_link_into_a_secret_folder_is_refused()
    {
        Directory.CreateDirectory(Path.Combine(dir, "secrets"));
        File.WriteAllText(Path.Combine(dir, "secrets", "db.txt"), "password=1");
        MakeJunction(Path.Combine(dir, "docs"), Path.Combine(dir, "secrets"));

        Assert.Throws<SecretFileException>(() => FileTools.Read(Path.Combine(dir, "docs", "db.txt"), 1_000));
        Assert.Throws<SecretFileException>(() => FileTools.Write(Path.Combine(dir, "docs", "new.txt"), "x", WriteMode.Create));
    }

    [Fact]
    public void Ordinary_files_still_read_and_write()
    {
        var path = Path.Combine(dir, "notes.txt");
        FileTools.Write(path, "hello", WriteMode.Create);
        Assert.Equal("hello", FileTools.Read(path, 1_000).Text);
    }

    static string ShortPath(string path)
    {
        var buffer = new StringBuilder(1024);
        var length = GetShortPathNameW(path, buffer, buffer.Capacity);
        return length > 0 ? buffer.ToString() : path;
    }

    static void MakeJunction(string link, string target)
    {
        // A directory junction needs no special privilege, unlike a symbolic link.
        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        mklink.WaitForExit();
        Assert.Equal(0, mklink.ExitCode);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern int GetShortPathNameW(string longPath, StringBuilder shortPath, int length);
}
