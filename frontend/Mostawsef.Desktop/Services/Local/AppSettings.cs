namespace Mostawsef.Desktop.Services.Local;

public enum AppTheme
{
    Light,
    Dark,
}

public enum AppLanguage
{
    English,
    Arabic,
}

public class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Light;

    public AppLanguage Language { get; set; } = AppLanguage.Arabic;

    /// <summary>Address of the local Mostawsef API.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5270";
}
