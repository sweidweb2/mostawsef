using Mostawsef.Domain.Common;

namespace Mostawsef.Api.IntegrationTests.Users;

/// <summary>Pure normalization rules behind every Arabic-aware search.</summary>
public class SearchTextTests
{
    [Theory]
    [InlineData("أحمد", "احمد")]
    [InlineData("إيمان", "ايمان")]
    [InlineData("آمنة", "امنه")]
    [InlineData("مُحَمَّد", "محمد")]
    [InlineData("مــحمد", "محمد")]
    [InlineData("مصطفى", "مصطفي")]
    [InlineData("مؤمن", "مومن")]
    [InlineData("هيئة", "هييه")]
    [InlineData("٠١٢٣٤٥٦٧٨٩", "0123456789")]
    [InlineData("  Sara   MAHMOUD ", "sara mahmoud")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizes_spelling_variants_to_one_form(string? input, string expected) =>
        Assert.Equal(expected, SearchText.Normalize(input));
}
