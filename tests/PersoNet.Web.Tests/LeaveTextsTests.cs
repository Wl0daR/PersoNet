using PersoNet.Web.Portal;

namespace PersoNet.Web.Tests;

public sealed class LeaveTextsTests
{
    [Theory]
    [InlineData(1, "wniosek")]
    [InlineData(2, "wnioski")]
    [InlineData(4, "wnioski")]
    [InlineData(5, "wniosków")]
    [InlineData(12, "wniosków")]
    [InlineData(14, "wniosków")]
    [InlineData(22, "wnioski")]
    [InlineData(25, "wniosków")]
    [InlineData(0, "wniosków")]
    public void Plural_UsesThePolishForm(int count, string expected) =>
        LeaveTexts.Plural(count, "wniosek", "wnioski", "wniosków").ShouldBe(expected);

    [Theory]
    [InlineData("2", 2)]
    [InlineData("2,5", 2.5)]
    [InlineData("2.5", 2.5)]
    [InlineData("2:30", 2.5)]
    [InlineData(" 4 ", 4)]
    public void ParseHours_ReadsTypedHours(string text, decimal expected) =>
        LeaveTexts.ParseHours(text).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("2:75")]
    public void ParseHours_RejectsWhatIsNotHours(string? text) =>
        LeaveTexts.ParseHours(text).ShouldBeNull();

    [Theory]
    [InlineData(0, "0 dni")]
    [InlineData(8, "1 dzień")]
    [InlineData(112, "14 dni")]
    [InlineData(12, "1 dzień 4 godz.")]
    [InlineData(6, "6 godz.")]
    [InlineData(-3, "0 dni")]
    public void Amount_ShowsDaysOfEightHours(decimal hours, string expected) =>
        LeaveTexts.Amount(hours).ShouldBe(expected);
}
