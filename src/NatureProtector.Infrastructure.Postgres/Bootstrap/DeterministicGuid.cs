using System.Security.Cryptography;
using System.Text;

namespace NatureProtector.Infrastructure.Postgres.Bootstrap;

internal static class DeterministicGuid
{
    public static Guid FromString(string scope, string value)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes($"{scope}:{value}");
        var hash = md5.ComputeHash(bytes);

        return new Guid(hash);
    }
}
