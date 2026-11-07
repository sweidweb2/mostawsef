using Mostawsef.Domain.Common;

namespace Mostawsef.Domain.Entities;

/// <summary>A meaningful user or system action (sign-in, record created, settings changed...). Append-only.</summary>
public class ActivityLog : IChronological
{
    public const int ActionMaxLength = 100;
    public const int ModuleMaxLength = 50;
    public const int DescriptionMaxLength = 500;
    public const int UsernameMaxLength = 50;
    public const int EntityMaxLength = 100;
    public const int IpAddressMaxLength = 45;
    public const int UserAgentMaxLength = 300;
    public const int DetailsMaxLength = 8000;
    public const int SearchTextMaxLength = UsernameMaxLength + ActionMaxLength + ModuleMaxLength + DescriptionMaxLength + 3;

    private ActivityLog()
    {
    }

    public ActivityLog(
        string action,
        string module,
        string description,
        Guid? userId,
        string? username,
        string? entityType,
        string? entityId,
        string? ipAddress,
        string? userAgent,
        string? details)
    {
        Action = Text.TruncateRequired(action, ActionMaxLength);
        Module = Text.TruncateRequired(module, ModuleMaxLength);
        Description = Text.TruncateRequired(description, DescriptionMaxLength);
        UserId = userId;
        Username = Text.Truncate(username, UsernameMaxLength);
        EntityType = Text.Truncate(entityType, EntityMaxLength);
        EntityId = Text.Truncate(entityId, EntityMaxLength);
        IpAddress = Text.Truncate(ipAddress, IpAddressMaxLength);
        UserAgent = Text.Truncate(userAgent, UserAgentMaxLength);
        Details = Text.Truncate(details, DetailsMaxLength);
        RebuildSearchText();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;

    /// <summary>Stable code, e.g. "auth.login". See ActivityActions.</summary>
    public string Action { get; private set; } = string.Empty;

    public string Module { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid? UserId { get; private set; }

    public string? Username { get; private set; }

    public string? EntityType { get; private set; }

    public string? EntityId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    /// <summary>Extra context as JSON.</summary>
    public string? Details { get; private set; }

    /// <summary>Username, action, module and description, lowercased (Text.SearchKey). What the list search matches.</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>Sets SearchText from the searchable fields; also used to fill rows written before the column existed.</summary>
    public void RebuildSearchText() => SearchText = Text.SearchKey(Username, Action, Module, Description);
}
