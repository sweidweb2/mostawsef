using System;

namespace Mostawsef.Desktop.Services.Navigation;

/// <summary>
/// Protects a page/shell view model: NavigationService only opens it for a signed-in user in one of these roles.
/// Use the role names from Mostawsef.Contracts.Auth.Roles.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RequiresRoleAttribute(params string[] roles) : Attribute
{
    public string[] Roles { get; } = roles;
}

/// <summary>Any signed-in user may open the page.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class RequiresAuthenticationAttribute : Attribute
{
}
