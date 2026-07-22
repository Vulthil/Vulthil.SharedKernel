using System.Security.Cryptography;

namespace Vulthil.Messaging.Queues;

internal static class RandomNumberGeneratorExtensions
{
    /// <summary>
    /// Generates a cryptographically random <see langword="double"/> in the range [0, 1). Retry jitter does
    /// not need cryptographic strength, but the repository enforces CA5394 (no <see cref="Random"/>) as an
    /// error, so the secure generator is used here as well.
    /// </summary>
    public static double GetDouble()
    {
        var bytes = RandomNumberGenerator.GetBytes(8);
        var ul = BitConverter.ToUInt64(bytes, 0) / (1 << 11);
        return ul / (double)(1UL << 53);
    }
}
