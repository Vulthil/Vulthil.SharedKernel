using Vulthil.Results;

namespace Catalog.Search;

public static class SearchErrors
{
    public const int MinimumTermLength = 2;

    public static readonly Error TermTooShort =
        Error.Validation("Search.TermTooShort", $"A search term needs at least {MinimumTermLength} characters.");

    public static readonly Error NameRequired =
        Error.Validation("Search.NameRequired", "An indexed product needs a name.");

    public static readonly Error PriceNegative =
        Error.Validation("Search.PriceNegative", "An indexed product cannot have a negative price.");
}
