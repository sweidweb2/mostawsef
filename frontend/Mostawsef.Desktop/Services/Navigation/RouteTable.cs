using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Mostawsef.Desktop.Components;
using Mostawsef.Desktop.ViewModels;

namespace Mostawsef.Desktop.Services.Navigation;

/// <summary>A sidebar entry of a portal: the page view model it opens, its icon and title resource keys.</summary>
public sealed record PortalPage(Type PageType, string IconKey, string TitleKey)
{
    public Geometry? Icon => Icons.Get(IconKey);
}

/// <summary>A role portal: the shared shell with this role's title and menu. The first page opens first.</summary>
public sealed record PortalDefinition(string Role, string TitleKey, IReadOnlyList<PortalPage> Pages);

/// <summary>
/// Every screen of the app: the view that shows each view model and who may open it.
/// Public pages are open to anyone, portal pages only to their portal's role(s), anything else is denied.
/// </summary>
public sealed class RouteTable
{
    private readonly Dictionary<Type, Func<Control>> _views = [];
    private readonly HashSet<Type> _publicPages = [];
    private readonly Dictionary<Type, HashSet<string>> _pageRoles = [];
    private readonly List<PortalDefinition> _portals = [];

    public IReadOnlyList<PortalDefinition> Portals => _portals;

    /// <summary>Page view models, created through DI when opened.</summary>
    public IEnumerable<Type> PageTypes => _publicPages.Concat(_pageRoles.Keys);

    public RouteTable Public<TViewModel, TView>()
        where TViewModel : ViewModelBase
        where TView : Control, new()
    {
        View<TViewModel, TView>();
        _publicPages.Add(typeof(TViewModel));
        return this;
    }

    /// <summary>Maps a view model to its view without making it a page (e.g. the portal shell).</summary>
    public RouteTable View<TViewModel, TView>()
        where TViewModel : ViewModelBase
        where TView : Control, new()
    {
        _views[typeof(TViewModel)] = () => new TView();
        return this;
    }

    public RouteTable Portal(string role, string titleKey, Action<PortalBuilder> pages)
    {
        var builder = new PortalBuilder(this, role);
        pages(builder);
        _portals.Add(new PortalDefinition(role, titleKey, builder.Pages));
        return this;
    }

    public PortalDefinition? FindPortal(string? role) => _portals.FirstOrDefault(p => p.Role == role);

    public bool IsAllowed(Type viewModelType, Func<string, bool> isInRole) =>
        _publicPages.Contains(viewModelType)
        || (_pageRoles.TryGetValue(viewModelType, out var roles) && roles.Any(isInRole));

    public Control? CreateView(Type viewModelType) =>
        _views.TryGetValue(viewModelType, out var create) ? create() : null;

    public sealed class PortalBuilder(RouteTable table, string role)
    {
        internal List<PortalPage> Pages { get; } = [];

        public PortalBuilder Page<TViewModel, TView>(string iconKey, string titleKey)
            where TViewModel : ViewModelBase
            where TView : Control, new()
        {
            table.View<TViewModel, TView>();

            if (!table._pageRoles.TryGetValue(typeof(TViewModel), out var roles))
            {
                table._pageRoles[typeof(TViewModel)] = roles = [];
            }

            roles.Add(role);
            Pages.Add(new PortalPage(typeof(TViewModel), iconKey, titleKey));
            return this;
        }
    }
}
