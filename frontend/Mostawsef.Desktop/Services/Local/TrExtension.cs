using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Translated text that refreshes when the language changes.
/// Usage: Text="{l:Tr Common.Save}" with xmlns:l="using:Mostawsef.Desktop.Services.Local".
/// </summary>
public class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => CreateBinding(Key);

    /// <summary>Binding for code-behind: control.Bind(TextBlock.TextProperty, TrExtension.CreateBinding("Common.Save")).</summary>
    public static BindingBase CreateBinding(string key) =>
        new ReflectionBinding(nameof(LocalizationService.Version))
        {
            Source = LocalizationService.Instance,
            Mode = BindingMode.OneWay,
            Converter = TranslateConverter.Instance,
            ConverterParameter = key,
        };

    private sealed class TranslateConverter : IValueConverter
    {
        public static readonly TranslateConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            parameter is string key ? LocalizationService.Instance.Get(key) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
