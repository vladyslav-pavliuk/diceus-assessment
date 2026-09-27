using System.Reflection;
using AutoMapper;
using AutoMapper.Internal;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.Application.Tests.Mapping;

public sealed class MappingConfigurationTests
{
    private readonly IConfigurationProvider _configuration;

    public MappingConfigurationTests()
    {
        var services = new ServiceCollection().AddLogging().AddApplication();
        _configuration = services.BuildServiceProvider().GetRequiredService<IMapper>().ConfigurationProvider;
    }

    [Fact]
    public void CONV_15_AutoMapper_configuration_is_valid()
    {
        _configuration.AssertConfigurationIsValid();
    }

    /// <summary>
    /// Guard for the suppressed AutoMapper 14 advisory CVE-2026-32933 (D-37): the stack overflow needs
    /// a type that (transitively) contains itself. No mapped source or destination type may do so.
    /// </summary>
    [Fact]
    public void CONV_15_Mapped_types_are_not_self_referencing()
    {
        var offenders = _configuration.Internal().GetAllTypeMaps()
            .SelectMany(map => new[] { map.SourceType, map.DestinationType })
            .Distinct()
            .Where(type => ReferencesItself(type, []))
            .Select(type => type.FullName)
            .ToList();

        offenders.ShouldBeEmpty();
    }

    /// <summary>Mappings go from entities to DTOs; request input (commands/queries) is never mapped (D-37).</summary>
    [Fact]
    public void CONV_15_Requests_are_never_mapped()
    {
        _configuration.Internal().GetAllTypeMaps()
            .Where(map => typeof(IBaseRequest).IsAssignableFrom(map.SourceType))
            .Select(map => map.SourceType.FullName)
            .ShouldBeEmpty();
    }

    private static bool ReferencesItself(Type type, HashSet<Type> path)
    {
        type = ElementTypeOf(type);
        if (IsLeaf(type))
        {
            return false;
        }

        if (!path.Add(type))
        {
            return true;
        }

        var cycle = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(property => ReferencesItself(property.PropertyType, path));

        path.Remove(type);
        return cycle;
    }

    private static Type ElementTypeOf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsArray)
        {
            return type.GetElementType()!;
        }

        var enumerable = type.GetInterfaces().Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return type != typeof(string) && enumerable is not null ? enumerable.GetGenericArguments()[0] : type;
    }

    private static bool IsLeaf(Type type) =>
        type.IsPrimitive
        || type.IsEnum
        || type == typeof(string)
        || type == typeof(decimal)
        || type == typeof(Guid)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly)
        || type == typeof(TimeSpan);
}
