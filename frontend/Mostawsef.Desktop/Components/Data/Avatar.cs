using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>Round picture or initials of DisplayName.</summary>
public class Avatar : TemplatedControl
{
    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<Avatar, IImage?>(nameof(Source));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Avatar, double>(nameof(Size), 40);

    public static readonly DirectProperty<Avatar, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<Avatar, string>(nameof(Initials), o => o.Initials);

    public static readonly DirectProperty<Avatar, CornerRadius> RoundRadiusProperty =
        AvaloniaProperty.RegisterDirect<Avatar, CornerRadius>(nameof(RoundRadius), o => o.RoundRadius);

    public static readonly DirectProperty<Avatar, double> InitialsFontSizeProperty =
        AvaloniaProperty.RegisterDirect<Avatar, double>(nameof(InitialsFontSize), o => o.InitialsFontSize);

    private string _initials = string.Empty;
    private CornerRadius _roundRadius = new(20);
    private double _initialsFontSize = 15;

    public string? DisplayName
    {
        get => GetValue(DisplayNameProperty);
        set => SetValue(DisplayNameProperty, value);
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public string Initials
    {
        get => _initials;
        private set => SetAndRaise(InitialsProperty, ref _initials, value);
    }

    public CornerRadius RoundRadius
    {
        get => _roundRadius;
        private set => SetAndRaise(RoundRadiusProperty, ref _roundRadius, value);
    }

    public double InitialsFontSize
    {
        get => _initialsFontSize;
        private set => SetAndRaise(InitialsFontSizeProperty, ref _initialsFontSize, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DisplayNameProperty)
        {
            var words = (DisplayName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
        }
        else if (change.Property == SizeProperty)
        {
            RoundRadius = new CornerRadius(Size / 2);
            InitialsFontSize = Math.Round(Size * 0.38);
        }
    }
}
