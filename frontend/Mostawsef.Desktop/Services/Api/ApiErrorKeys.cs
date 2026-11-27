namespace Mostawsef.Desktop.Services.Api;

/// <summary>The resource keys of the load-failure texts for an <see cref="ApiError"/> (shown by the ErrorState control).</summary>
public static class ApiErrorKeys
{
    public static string? Title(ApiError? error) => error switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableTitle",
        ApiError.Timeout => "Error.TimeoutTitle",
        _ => "Error.Title",
    };

    public static string? Description(ApiError? error) => error switch
    {
        null => null,
        ApiError.Unreachable => "Error.UnreachableDescription",
        ApiError.Timeout => "Error.TimeoutDescription",
        _ => "Error.Description",
    };

    /// <summary>Toast / form-level text when a write or action fails on the wire.</summary>
    public static string Action(ApiError error) => error switch
    {
        ApiError.Unreachable => "Login.ServerUnreachable",
        ApiError.Timeout => "Error.Timeout",
        _ => "Error.RequestFailed",
    };
}
