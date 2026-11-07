using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Translated text that refreshes when the language changes.
/// Usage: Text="{l:Tr Common.Save}" with xmlns:l="using:Mostawsef.Desktop.Services.Local".
/// With a bound {0} argument: Text="{l:Tr Dashboard.Welcome, Arg=UserName}".
/// </summary>
public class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    /// <summary>Path of a DataContext property passed as {0} to the translated text.</summary>
    public string? Arg { get; set; }

    // App.Services is null only in the XAML previewer, which then shows the raw key.
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        App.Services is null ? Key
        : Arg is null ? CreateBinding(Key)
        : Translation.Bind(new ReflectionBinding(Arg), new Translation.FormatConverter(App.Localization), Key);

    /// <summary>Binding for code-behind: control.Bind(TextBlock.TextProperty, TrExtension.CreateBinding("Common.Save")).</summary>
    public static BindingBase CreateBinding(string key)
    {
        var localization = App.Localization;
        return new ReflectionBinding(nameof(LocalizationService.Version))
        {
            Source = localization,
            Mode = BindingMode.OneWay,
            Converter = new TranslateConverter(localization),
            ConverterParameter = key,
        };
    }

    private sealed class TranslateConverter(LocalizationService localization) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            parameter is string key ? localization.Get(key) : null;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}

/// <summary>
/// Translates the resource key held by a DataContext property, so view models expose keys
/// and never listen for language changes. Usage: Text="{l:TrKey ErrorKey}".
/// </summary>
public class TrKeyExtension(string path) : MarkupExtension
{
    public string Path { get; set; } = path;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        App.Services is null
            ? Path
            : Translation.Bind(new ReflectionBinding(Path), new Translation.KeyConverter(App.Localization), parameter: null);
}

internal static class Translation
{
    /// <summary>Combines a DataContext binding with LocalizationService.Version so the text refreshes on language changes.</summary>
    public static MultiBinding Bind(BindingBase value, Converter converter, string? parameter) =>
        new()
        {
            Bindings =
            {
                value,
                new ReflectionBinding(nameof(LocalizationService.Version)) { Source = converter.Localization },
            },
            Mode = BindingMode.OneWay,
            Converter = converter,
            ConverterParameter = parameter,
        };

    public abstract class Converter(LocalizationService localization) : IMultiValueConverter
    {
        public LocalizationService Localization { get; } = localization;

        public abstract object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture);
    }

    public sealed class KeyConverter(LocalizationService localization) : Converter(localization)
    {
        public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            values.Count > 0 && values[0] is string { Length: > 0 } key ? Localization.Get(key) : null;
    }

    public sealed class FormatConverter(LocalizationService localization) : Converter(localization)
    {
        public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is not string key)
            {
                return null;
            }

            var arg = values.Count > 0 && values[0] != AvaloniaProperty.UnsetValue ? values[0] : null;
            return Localization.Format(key, arg);
        }
    }
}
