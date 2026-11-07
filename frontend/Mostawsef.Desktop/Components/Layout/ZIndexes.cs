namespace Mostawsef.Desktop.Components;

/// <summary>Layer order for in-window overlays. Mirrors Styles/Tokens/ZIndex.axaml.</summary>
public static class ZIndexes
{
    public const int Base = 0;
    public const int Raised = 1;
    public const int Sticky = 10;
    public const int Dropdown = 100;
    public const int Overlay = 1000;
    public const int Modal = 1100;
    public const int Toast = 1200;
    public const int Tooltip = 1300;
}
