using Deskbridge.Engines.Os.Files;

namespace Deskbridge.Tests;

public class SecretFilesTests
{
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
    [InlineData(@"C:\notes\notes.txt", false)]
    [InlineData(@"C:\notes\environment.md", false)]
    [InlineData(@"C:\code\keyboard.cs", false)]
    public void Recognises_secret_files(string path, bool secret) => Assert.Equal(secret, SecretFiles.IsSecret(path));
}
