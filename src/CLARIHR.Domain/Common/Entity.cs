using System.Security.Cryptography;
using System.Text;

namespace CLARIHR.Domain.Common;

public abstract class Entity
{
    public long Id { get; protected set; }

    public Guid PublicId { get; protected set; } = Guid.NewGuid();

    public static Guid CreateDeterministicPublicId(string seed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seed);

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(seed.Trim()));
        return new Guid(hash);
    }
}
