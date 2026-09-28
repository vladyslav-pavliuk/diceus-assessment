using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ClaimsModule.IntegrationTests.Architecture;

/// <summary>
/// FRS §14.2: "All writes to ClaimAuditLog must go through a dedicated IAuditLogService; no direct
/// DbContext writes from handlers." A ClaimAuditLog row can only be built by ClaimAuditLog.Create (its
/// constructors are private), so scanning the IL for callers of Create proves the rule.
/// </summary>
public sealed class AuditWriteTests
{
    private const string Create = "ClaimsModule.Domain.Audit.ClaimAuditLog ClaimsModule.Domain.Audit.ClaimAuditLog::Create(";

    [Fact]
    public void AUD_I1_Only_the_audit_log_service_creates_audit_rows()
    {
        var callers = new List<string>();

        foreach (var assembly in SolutionAssemblies.All)
        {
            using var module = ModuleDefinition.ReadModule(assembly.Location);
            foreach (var method in module.GetTypes().SelectMany(type => type.Methods).Where(method => method.HasBody))
            {
                if (method.Body.Instructions.Any(instruction =>
                        (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt)
                        && instruction.Operand is MethodReference called
                        && called.FullName.StartsWith(Create, StringComparison.Ordinal)))
                {
                    callers.Add(method.DeclaringType.FullName);
                }
            }
        }

        callers.Distinct().ShouldBe(["ClaimsModule.Persistence.AuditLogService"]);
    }
}
