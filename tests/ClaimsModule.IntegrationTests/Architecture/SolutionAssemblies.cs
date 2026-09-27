using System.Reflection;
using ClaimsModule.Domain.Common;
using ClaimsModule.Persistence;

namespace ClaimsModule.IntegrationTests.Architecture;

internal static class SolutionAssemblies
{
    public static readonly Assembly Domain = typeof(Entity).Assembly;
    public static readonly Assembly Application = typeof(Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(Infrastructure.DependencyInjection).Assembly;
    public static readonly Assembly Persistence = typeof(ClaimsDbContext).Assembly;
    public static readonly Assembly Api = typeof(Program).Assembly;

    public static readonly Assembly[] All = [Domain, Application, Infrastructure, Persistence, Api];
}
