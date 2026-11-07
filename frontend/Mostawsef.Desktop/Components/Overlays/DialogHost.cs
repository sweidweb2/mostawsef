using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Root of a window's content that can stack modal layers above it.
/// Place once in MainWindow and attach it to DialogService.
/// </summary>
[TemplatePart("PART_OverlayLayer", typeof(Panel))]
public class DialogHost : ContentControl
{
    private readonly Dictionary<Modal, Control> _layers = [];
    private Panel? _overlayLayer;

    public bool HasOpenDialogs => _layers.Count > 0;

    internal void Show(Modal modal)
    {
        if (_overlayLayer is null)
        {
            return;
        }

        modal.HorizontalAlignment = HorizontalAlignment.Center;
        modal.VerticalAlignment = VerticalAlignment.Center;

        var scrim = new Border { Classes = { "dialog-scrim" } };
        var layer = new Panel { Children = { scrim, modal } };

        // A click outside acts like the X button, but only when both press and release land outside the modal,
        // so selecting text in a field and releasing past its edge does not close it.
        var pressedOutside = false;
        scrim.PointerPressed += (_, e) => pressedOutside = e.GetCurrentPoint(scrim).Properties.IsLeftButtonPressed;
        scrim.PointerReleased += (_, e) =>
        {
            var wasPressedOutside = pressedOutside;
            pressedOutside = false;
            if (wasPressedOutside
                && e.InitialPressMouseButton == MouseButton.Left
                && !modal.Bounds.Contains(e.GetPosition(layer))
                && modal.ShowCloseButton)
            {
                e.Handled = true;
                _ = modal.DismissAsync();
            }
        };

        _layers[modal] = layer;
        _overlayLayer.Children.Add(layer);
        _overlayLayer.IsVisible = true;
        modal.Focus();
    }

    internal void Hide(Modal modal)
    {
        if (_overlayLayer is null || !_layers.Remove(modal, out var layer))
        {
            return;
        }

        _overlayLayer.Children.Remove(layer);
        _overlayLayer.IsVisible = _layers.Count > 0;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _overlayLayer = e.NameScope.Find<Panel>("PART_OverlayLayer");
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _layers.Count > 0)
        {
            var top = _layers.Keys.Last();
            if (top.ShowCloseButton)
            {
                _ = top.DismissAsync();
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }
}
