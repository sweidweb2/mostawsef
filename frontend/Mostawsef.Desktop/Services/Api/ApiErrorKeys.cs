namespace Mostawsef.Desktop.Services.Api;

/// <summary>The resource keys of the load-failure texts for an <see cref="ApiError"/> (shown by the ErrorState control).</summary>
public static class ApiErrorKeys
{
    public static string? Title(ApiError? error) => error switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableTitle",
        _ => "Error.Title",
    };

    public static string? Description(ApiError? error) => error switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableDescription",
        _ => "Error.Description",
    };
}
