using System.Text.RegularExpressions;
using AutoMapper;
using ClaimsModule.Application.Common.Messaging;
using MediatR;
using NetArchTest.Rules;

namespace ClaimsModule.IntegrationTests.Architecture;

public sealed partial class ConventionTests
{
    [Fact]
    public void CONV_13_Commands_are_named_VerbNounCommand()
    {
        RequestTypes()
            .Where(type => Implements(type, typeof(ICommand)) || Implements(type, typeof(ICommand<>)))
            .Where(type => !type.Name.EndsWith("Command", StringComparison.Ordinal))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_13_Queries_are_named_GetNounQuery_or_ListNounsQuery()
    {
        RequestTypes()
            .Where(type => Implements(type, typeof(IQuery<>)))
            .Where(type => !QueryName().IsMatch(type.Name))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    /// <summary>
    /// Every MediatR request is classified as a command or a query, so the Unit of Work behaviour
    /// (commands only) can never silently skip a write.
    /// </summary>
    [Fact]
    public void CONV_13_Every_request_is_a_command_or_a_query()
    {
        RequestTypes()
            .Where(type => !Implements(type, typeof(ICommand)) && !Implements(type, typeof(ICommand<>)) && !Implements(type, typeof(IQuery<>)))
            .Select(type => type.FullName)
            .ShouldBeEmpty();
    }

    [Fact]
    public void CONV_15_AutoMapper_profiles_live_only_in_Application()
    {
        var profilesOutsideApplication = Types.InAssemblies(
                [SolutionAssemblies.Domain, SolutionAssemblies.Infrastructure, SolutionAssemblies.Persistence, SolutionAssemblies.Api])
            .That().Inherit(typeof(Profile))
            .GetTypes();

        profilesOutsideApplication.Select(type => type.FullName).ShouldBeEmpty();
    }

    private static IEnumerable<Type> RequestTypes() =>
        SolutionAssemblies.Application.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(IBaseRequest).IsAssignableFrom(type));

    private static bool Implements(Type type, Type contract) =>
        type.GetInterfaces().Any(candidate =>
            candidate == contract || (candidate.IsGenericType && candidate.GetGenericTypeDefinition() == contract));

    [GeneratedRegex("^(Get|List)[A-Z][A-Za-z]*Query$")]
    private static partial Regex QueryName();
}
