using System;
using System.Globalization;

namespace Mostawsef.Desktop.Services.Api;

/// <summary>The query string of a dashboard period (<c>DashboardRangeQuery</c>): ISO days, culture-independent.</summary>
internal static class DashboardQuery
{
    public static string Of(DateOnly from, DateOnly to) =>
        string.Create(CultureInfo.InvariantCulture, $"from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
}
