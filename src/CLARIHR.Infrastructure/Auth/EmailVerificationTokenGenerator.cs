using System.Security.Cryptography;
using CLARIHR.Application.Abstractions.Auth;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class EmailVerificationTokenGenerator : IEmailVerificationTokenGenerator
{
    public string Generate() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
}
