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

    /// <summary>
    /// Builds the main views ahead of time (while the login screen is idle and after sign-in) so the first visit to each is quick.
    /// Costs some CPU and memory right after start. Set to false in settings.json on a PC where that makes the app feel slower.
    /// </summary>
    public bool PreloadViews { get; set; } = true;
}
