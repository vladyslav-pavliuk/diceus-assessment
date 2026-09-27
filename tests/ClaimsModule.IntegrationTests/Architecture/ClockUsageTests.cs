using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ClaimsModule.IntegrationTests.Architecture;

/// <summary>
/// CLAUDE.md rule 9: time comes from an injected TimeProvider. Reading the system clock directly
/// makes time-based rules (48h SLA, 24h dedupe, loss date not in the future) untestable.
/// The test scans the IL of every production assembly for the forbidden property getters.
/// </summary>
public sealed class ClockUsageTests
{
    private static readonly HashSet<string> ForbiddenCalls =
    [
        "System.DateTime System.DateTime::get_Now()",
        "System.DateTime System.DateTime::get_UtcNow()",
        "System.DateTime System.DateTime::get_Today()",
        "System.DateTimeOffset System.DateTimeOffset::get_Now()",
        "System.DateTimeOffset System.DateTimeOffset::get_UtcNow()",
    ];

    [Fact]
    public void CONV_16_Production_code_never_reads_the_system_clock_directly()
    {
        var violations = new List<string>();

        foreach (var assembly in SolutionAssemblies.All)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);
            foreach (var method in module.GetTypes().SelectMany(type => type.Methods).Where(method => method.HasBody))
            {
                violations.AddRange(method.Body.Instructions
                    .Where(instruction => instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                    .Select(instruction => instruction.Operand)
                    .OfType<MethodReference>()
                    .Where(called => ForbiddenCalls.Contains(called.FullName))
                    .Select(called => $"{method.FullName} calls {called.FullName}"));
            }
        }

        violations.ShouldBeEmpty();
    }
}
