using MiniWallet.Users.Domain.Entities;
using MiniWallet.Users.Infrastructure;
using Xunit;

namespace MiniWallet.Tests.Users;

public sealed class UserDomainTests
{
    [Theory]
    [InlineData(" Player@Example.Test ", "player@example.test")]
    [InlineData("a+b@example.test", "a+b@example.test")]
    public void Email_normalizes_consistently(string input, string expected) =>
        Assert.Equal(expected, EmailAddress.Parse(input).Value);

    [Theory]
    [InlineData("invalid")]
    [InlineData("Name <user@example.test>")]
    [InlineData("user @example.test")]
    public void Invalid_email_is_rejected(string email) => Assert.Throws<ArgumentException>(() => EmailAddress.Parse(email));

    [Fact]
    public void Verification_expiry_and_reuse_are_domain_rules()
    {
        var now = DateTimeOffset.UtcNow;
        var user = User.Register(EmailAddress.Parse("domain@example.test"), "hash");
        user.SetVerification("one", now.AddMinutes(1), now);
        Assert.False(user.Verify("wrong", now));
        Assert.False(user.Verify("one", now.AddMinutes(1)));
        Assert.True(user.Verify("one", now));
        Assert.False(user.Verify("one", now));
        Assert.Null(user.VerificationTokenHash);
        Assert.Throws<InvalidOperationException>(() => user.SetVerification("two", now.AddHours(1), now));
    }

    [Fact]
    public void Passwords_are_salted_and_checked_safely()
    {
        var passwords = new PasswordService();
        var first = passwords.Hash("Safe-password-123!");
        Assert.NotEqual(first, passwords.Hash("Safe-password-123!"));
        Assert.True(passwords.Verify("Safe-password-123!", first));
        Assert.False(passwords.Verify("wrong", first));
        Assert.False(passwords.Verify("wrong", "malformed"));
    }
}

