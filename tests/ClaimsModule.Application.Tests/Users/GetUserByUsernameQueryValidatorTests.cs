using ClaimsModule.Application.Users.Queries.GetUserByUsername;

namespace ClaimsModule.Application.Tests.Users;

public sealed class GetUserByUsernameQueryValidatorTests
{
    private readonly GetUserByUsernameQueryValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void API_28_Missing_username_is_rejected(string? username)
    {
        var result = _validator.Validate(new GetUserByUsernameQuery(username!));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("Username is required.");
    }

    [Fact]
    public void API_28_Username_longer_than_255_characters_is_rejected()
    {
        var result = _validator.Validate(new GetUserByUsernameQuery(new string('a', 256)));

        result.Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("Username must not exceed 255 characters.");
    }

    [Fact]
    public void API_28_Seeded_username_is_accepted()
    {
        _validator.Validate(new GetUserByUsernameQuery("handler.alex")).IsValid.ShouldBeTrue();
    }
}
