using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>
/// Round picture or initials of DisplayName. MaxInitials limits the letters (1 = first letter only).
/// IsColorful picks one of five tones from the name, so the same name always gets the same color.
/// </summary>
[PseudoClasses(":tone0", ":tone1", ":tone2", ":tone3", ":tone4")]
public class Avatar : TemplatedControl
{
    public const int ToneCount = 5;

    public static readonly StyledProperty<string?> DisplayNameProperty =
        AvaloniaProperty.Register<Avatar, string?>(nameof(DisplayName));

    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<Avatar, IImage?>(nameof(Source));

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Avatar, double>(nameof(Size), 40);

    public static readonly StyledProperty<int> MaxInitialsProperty =
        AvaloniaProperty.Register<Avatar, int>(nameof(MaxInitials), 2);

    public static readonly StyledProperty<bool> IsColorfulProperty =
        AvaloniaProperty.Register<Avatar, bool>(nameof(IsColorful));

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

    public int MaxInitials
    {
        get => GetValue(MaxInitialsProperty);
        set => SetValue(MaxInitialsProperty, value);
    }

    public bool IsColorful
    {
        get => GetValue(IsColorfulProperty);
        set => SetValue(IsColorfulProperty, value);
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

    /// <summary>0..ToneCount-1 from the characters of the name; stable across runs (unlike string.GetHashCode).</summary>
    public static int ToneOf(string? name)
    {
        var sum = 0;
        foreach (var c in (name ?? string.Empty).Trim())
        {
            sum = unchecked(sum * 31 + char.ToUpperInvariant(c));
        }

        return (int)((uint)sum % ToneCount);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DisplayNameProperty || change.Property == MaxInitialsProperty)
        {
            var words = (DisplayName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = string.Concat(words.Take(Math.Max(1, MaxInitials)).Select(w => char.ToUpperInvariant(w[0])));
        }
        else if (change.Property == SizeProperty)
        {
            RoundRadius = new CornerRadius(Size / 2);
            InitialsFontSize = Math.Round(Size * 0.38);
        }

        if (change.Property == DisplayNameProperty || change.Property == IsColorfulProperty)
        {
            var tone = IsColorful ? ToneOf(DisplayName) : -1;
            for (var i = 0; i < ToneCount; i++)
            {
                PseudoClasses.Set($":tone{i}", i == tone);
            }
        }
    }
}
