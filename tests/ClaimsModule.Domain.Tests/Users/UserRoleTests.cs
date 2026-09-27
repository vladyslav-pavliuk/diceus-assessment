using ClaimsModule.Domain.Users;

namespace ClaimsModule.Domain.Tests.Users;

public sealed class UserRoleTests
{
    [Theory]
    [InlineData(UserRole.Handler, UserRole.Handler, true)]
    [InlineData(UserRole.Handler, UserRole.Supervisor, false)]
    [InlineData(UserRole.Handler, UserRole.Manager, false)]
    [InlineData(UserRole.Supervisor, UserRole.Handler, true)]
    [InlineData(UserRole.Supervisor, UserRole.Supervisor, true)]
    [InlineData(UserRole.Supervisor, UserRole.Manager, false)]
    [InlineData(UserRole.Manager, UserRole.Handler, true)]
    [InlineData(UserRole.Manager, UserRole.Supervisor, true)]
    [InlineData(UserRole.Manager, UserRole.Manager, true)]
    public void SEC_01_Roles_are_hierarchical_handler_supervisor_manager(UserRole role, UserRole minimum, bool expected)
    {
        role.IsAtLeast(minimum).ShouldBe(expected);
    }

    [Fact]
    public void SEC_01_Undefined_role_is_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ((UserRole)0).IsAtLeast(UserRole.Handler));
    }

    [Theory]
    [InlineData(UserRole.Handler, "handler")]
    [InlineData(UserRole.Supervisor, "supervisor")]
    [InlineData(UserRole.Manager, "manager")]
    public void SEC_01_Role_codes_match_FRS_section_3(UserRole role, string code)
    {
        role.ToCode().ShouldBe(code);
        UserRoleExtensions.TryParseCode(code, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Handler")]
    [InlineData("admin")]
    public void SEC_01_Unknown_role_code_is_not_parsed(string? code)
    {
        UserRoleExtensions.TryParseCode(code, out _).ShouldBeFalse();
    }
}
