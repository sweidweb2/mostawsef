using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Mostawsef.Desktop.Components;

/// <summary>Shared display properties of PatientCard and PatientHeader. Values are preformatted text.</summary>
public abstract class PatientInfo : TemplatedControl
{
    public static readonly StyledProperty<string?> PatientNameProperty =
        AvaloniaProperty.Register<PatientInfo, string?>(nameof(PatientName));

    public static readonly StyledProperty<string?> FileNumberProperty =
        AvaloniaProperty.Register<PatientInfo, string?>(nameof(FileNumber));

    public static readonly StyledProperty<string?> AgeProperty =
        AvaloniaProperty.Register<PatientInfo, string?>(nameof(Age));

    public static readonly StyledProperty<string?> GenderProperty =
        AvaloniaProperty.Register<PatientInfo, string?>(nameof(Gender));

    public static readonly StyledProperty<string?> PhoneProperty =
        AvaloniaProperty.Register<PatientInfo, string?>(nameof(Phone));

    public static readonly StyledProperty<IImage?> PhotoProperty =
        AvaloniaProperty.Register<PatientInfo, IImage?>(nameof(Photo));

    public static readonly StyledProperty<string?> StatusProperty =
        AvaloniaProperty.Register<PatientInfo, string?>(nameof(Status));

    public static readonly StyledProperty<BadgeKind> StatusKindProperty =
        AvaloniaProperty.Register<PatientInfo, BadgeKind>(nameof(StatusKind));

    public string? PatientName
    {
        get => GetValue(PatientNameProperty);
        set => SetValue(PatientNameProperty, value);
    }

    public string? FileNumber
    {
        get => GetValue(FileNumberProperty);
        set => SetValue(FileNumberProperty, value);
    }

    public string? Age
    {
        get => GetValue(AgeProperty);
        set => SetValue(AgeProperty, value);
    }

    public string? Gender
    {
        get => GetValue(GenderProperty);
        set => SetValue(GenderProperty, value);
    }

    public string? Phone
    {
        get => GetValue(PhoneProperty);
        set => SetValue(PhoneProperty, value);
    }

    public IImage? Photo
    {
        get => GetValue(PhotoProperty);
        set => SetValue(PhotoProperty, value);
    }

    public string? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public BadgeKind StatusKind
    {
        get => GetValue(StatusKindProperty);
        set => SetValue(StatusKindProperty, value);
    }
}

/// <summary>Compact patient summary for lists and search results.</summary>
public class PatientCard : PatientInfo
{
}

/// <summary>Patient banner at the top of patient screens: details, allergies and actions.</summary>
public class PatientHeader : PatientInfo
{
    public static readonly StyledProperty<string?> AllergiesProperty =
        AvaloniaProperty.Register<PatientHeader, string?>(nameof(Allergies));

    public static readonly StyledProperty<object?> ActionsProperty =
        AvaloniaProperty.Register<PatientHeader, object?>(nameof(Actions));

    public string? Allergies
    {
        get => GetValue(AllergiesProperty);
        set => SetValue(AllergiesProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }
}
