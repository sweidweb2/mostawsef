using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Mostawsef.Contracts.Common;
using Mostawsef.Contracts.Settings;
using Mostawsef.Desktop.Converters;
using Mostawsef.Desktop.Services.Local;
using Mostawsef.Desktop.Tests.Infrastructure;

namespace Mostawsef.Desktop.Tests.Local;

[Collection(UiTestCollection.Name)]
public class MoneyAndDateTests : UiTestBase
{
    [Theory]
    [InlineData(1250.50, 125050)]
    [InlineData(0.005, 1)]
    [InlineData(19.994, 1999)]
    public void Amounts_become_whole_minor_units(double amount, long minorUnits)
    {
        Assert.Equal(minorUnits, Money.ToMinor((decimal)amount));
        Assert.Equal(Math.Round((decimal)amount, 2, MidpointRounding.AwayFromZero), Money.ToMajor(minorUnits));
    }

    [AvaloniaFact]
    public void Money_shows_latin_digits_and_the_translated_currency()
    {
        using var app = new TestServices();

        Assert.Equal("1,250.50 EGP", MoneyFormat.Format(125050, Currencies.Egp, app.Localization));
        Assert.Equal("1,250.50", MoneyFormat.Format(125050, null, app.Localization));

        app.Localization.SetLanguage(AppLanguage.Arabic);

        Assert.Equal("1,250.50 ج.م", MoneyFormat.Format(125050, Currencies.Egp, app.Localization));
    }

    [AvaloniaFact]
    public void Money_markup_follows_the_language_and_the_clinic_currency()
    {
        using var app = new TestServices();
        var clinic = app.Get<ClinicContext>();
        clinic.Update(Settings(Currencies.Egp));
        var text = new TextBlock { DataContext = new AmountRow(990) };
        var amount = Avalonia.Data.CompiledBinding.Create<AmountRow, long>(r => r.Total);
        text.Bind(TextBlock.TextProperty, (Avalonia.Data.BindingBase)new MoneyExtension(amount).ProvideValue(null!));
        app.Show(text);

        Assert.Equal("9.90 EGP", text.Text);

        app.Localization.SetLanguage(AppLanguage.Arabic);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("9.90 ج.م", text.Text);

        clinic.Update(Settings(Currencies.Usd));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("9.90 دولار", text.Text);
    }

    [Fact]
    public void Calendar_dates_convert_to_and_from_the_date_picker()
    {
        var converter = ValueConverters.DateOnly;
        var birthDate = new DateOnly(1990, 5, 17);

        var shown = converter.Convert(birthDate, typeof(DateTime?), null, System.Globalization.CultureInfo.InvariantCulture);
        var back = converter.ConvertBack(new DateTime(1990, 5, 17, 13, 45, 0), typeof(DateOnly?), null, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(new DateTime(1990, 5, 17), shown);
        Assert.Equal(birthDate, back);
        Assert.Null(converter.Convert(null, typeof(DateTime?), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Null(converter.ConvertBack(null, typeof(DateOnly?), null, System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed record AmountRow(long Total);

    private static ClinicSettingsDto Settings(string currency) => new("Clinic", null, null, currency, 0, null, null);
}
