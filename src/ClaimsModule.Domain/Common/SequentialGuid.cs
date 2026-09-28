using System.Buffers.Binary;

namespace ClaimsModule.Domain.Common;

/// <summary>
/// GUIDs ordered for SQL Server's uniqueidentifier sort, so inserts append to the clustered index (D-30).
/// Same algorithm as EF Core's SequentialGuidValueGenerator. Guid.CreateVersion7 does not work here:
/// its timestamp sits in the bytes SQL Server compares last.
/// </summary>
public static class SequentialGuid
{
    // Seeded from the clock so values keep increasing across restarts. Not business time, so no injected TimeProvider.
    private static long _counter = TimeProvider.System.GetUtcNow().UtcTicks;

    public static Guid NewGuid()
    {
        Span<byte> guidBytes = stackalloc byte[16];
        Guid.NewGuid().TryWriteBytes(guidBytes);

        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(counterBytes, Interlocked.Increment(ref _counter));

        // SQL Server orders by bytes 10–15 first (most significant first), then 8–9.
        guidBytes[8] = counterBytes[1];
        guidBytes[9] = counterBytes[0];
        guidBytes[10] = counterBytes[7];
        guidBytes[11] = counterBytes[6];
        guidBytes[12] = counterBytes[5];
        guidBytes[13] = counterBytes[4];
        guidBytes[14] = counterBytes[3];
        guidBytes[15] = counterBytes[2];

        return new Guid(guidBytes);
    }
}
