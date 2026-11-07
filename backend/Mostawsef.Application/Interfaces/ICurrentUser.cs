namespace Mostawsef.Application.Interfaces;

/// <summary>Who is making the current request, and from where. Empty values outside HTTP requests.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Username { get; }

    string? Role { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    string? RequestPath { get; }

    string? RequestMethod { get; }

    string? TraceId { get; }
}
