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
                top.Close();
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }
}
