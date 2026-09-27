using System.Reflection;
using ClaimsModule.Domain.Claims;
using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Tests;


public sealed class DomainModelTests
{
    /// <summary>
    /// CLAUDE.md rule 3: entities protect their invariants, so no entity exposes a public or protected
    /// setter; state changes only through domain methods.
    /// </summary>
    [Fact]
    public void DOM_01_Entities_have_no_public_setters()
    {
        var entityTypes = typeof(Claim).Assembly.GetTypes().Where(type => typeof(Entity).IsAssignableFrom(type)).ToList();
        entityTypes.Count.ShouldBeGreaterThan(10);

        entityTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(property => property.SetMethod is { IsPrivate: false })
                .Select(property => $"{type.Name}.{property.Name}"))
            .ShouldBeEmpty();
    }

    /// <summary>Child collections are exposed read-only, so nothing outside the aggregate can add to them.</summary>
    [Fact]
    public void DOM_01_Aggregate_collections_are_read_only()
    {
        var claim = TestData.DraftClaim();

        // Even a caller that casts the list back cannot add to it.
        Should.Throw<NotSupportedException>(() => ((ICollection<ClaimParty>)claim.Parties).Add(claim.Parties[0]));
        ((ICollection<ClaimsModule.Domain.Reserves.ReserveComponent>)claim.ReserveComponents).IsReadOnly.ShouldBeTrue();
    }
}
