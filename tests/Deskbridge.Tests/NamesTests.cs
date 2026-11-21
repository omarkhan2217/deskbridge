using Deskbridge.Core;

namespace Deskbridge.Tests;

public class NamesTests
{
    [Fact]
    public void Pipe_and_mutex_names_include_the_sid_and_optional_suffix()
    {
        Assert.Equal("deskbridge-S-1-5-21-1", Names.PipeName("S-1-5-21-1", null));
        Assert.Equal("deskbridge-S-1-5-21-1-e2e-01", Names.PipeName("S-1-5-21-1", "e2e-01"));
        Assert.Equal(@"Local\deskbridge-broker-S-1-5-21-1", Names.BrokerMutex("S-1-5-21-1", null));
        Assert.Equal(@"Local\deskbridge-broker-S-1-5-21-1-e2e-01", Names.BrokerMutex("S-1-5-21-1", "e2e-01"));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("a/b")]
    [InlineData("")]
    [InlineData("123456789012345678901234567890123")]
    public void Unsafe_suffixes_are_rejected(string suffix) =>
        Assert.Throws<ArgumentException>(() => Names.PipeName("S-1-5-21-1", suffix));

    [Fact]
    public void Current_user_sid_is_a_windows_sid() =>
        Assert.StartsWith("S-1-5-", Names.CurrentUserSid());
}
