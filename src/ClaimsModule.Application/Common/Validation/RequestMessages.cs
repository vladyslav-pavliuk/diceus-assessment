namespace ClaimsModule.Application.Common.Validation;

/// <summary>Request-shape messages (D-40). Business-rule wording lives in <see cref="Domain.Claims.DomainMessages"/>.</summary>
internal static class RequestMessages
{
    public const string PolicyNotFound = "Policy was not found.";
    public const string PolicyRequired = "A policy is required.";
    public const string UserNotFound = "User was not found.";
    public const string UserRequired = "A user is required.";
    public const string TargetStatusRequired = "Target status is required.";
    public const string InvalidClaimStatus = "Invalid claim status.";
    public const string InvalidRole = "Invalid role.";
    public const string InvalidPerilCategory = "Invalid peril category.";
    public const string PageMustBePositive = "Page must be 1 or greater.";
    public const string LossDateRangeInvalid = "dateFrom must not be later than dateTo.";
    public const string SearchTermRequired = "A search term is required.";
    public const string NewAmountRequired = "The new reserve amount is required.";
    public const string OverrideEnabledRequired = "Enabled is required.";

    public static string PageSizeOutOfRange(int max) => $"Page size must be between 1 and {max}.";

    public static string TooLong(string field, int maxLength) => $"{field} must not exceed {maxLength} characters.";
}
