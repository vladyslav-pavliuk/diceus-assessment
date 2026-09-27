using System.Buffers.Binary;

namespace ClaimsModule.Domain.Common;

/// <summary>
/// Generates GUIDs that increase in SQL Server's uniqueidentifier sort order, so inserts append to
/// the clustered index instead of fragmenting it (FRS §15.1 "use sequential GUIDs", D-30).
/// This is the algorithm of EF Core's SequentialGuidValueGenerator: random bytes, with a
/// monotonically increasing counter written into bytes 10–15 and 8–9, which SQL Server compares first.
/// Guid.CreateVersion7 is not used: its timestamp sits in the first bytes, which SQL Server compares last.
/// </summary>
public static class SequentialGuid
{
    // Seeded from the clock so that values keep increasing across process restarts. This is not
    // business time, so it does not go through an injected TimeProvider (CLAUDE.md rule 9 is about
    // time-based rules); TimeProvider.System keeps the architecture test's clock scan meaningful.
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
