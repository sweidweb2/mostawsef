using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Mostawsef.Contracts.Common;

namespace Mostawsef.Desktop.Services.Local;

/// <summary>
/// Translated text that refreshes when the language changes.
/// Usage: Text="{l:Tr Common.Save}" with xmlns:l="using:Mostawsef.Desktop.Services.Local".
/// With a bound {0} argument: Text="{l:Tr Dashboard.Welcome, Arg={Binding UserName}}" (compiled under x:DataType).
/// </summary>
public class TrExtension(string key) : MarkupExtension
{
    public string Key { get; set; } = key;

    /// <summary>Binding of the value passed as {0} to the translated text.</summary>
    public BindingBase? Arg { get; set; }

    // App.Services is null only in the XAML previewer, which then shows the raw key.
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        App.Services is null ? Key
        : Arg is null ? CreateBinding(Key)
        : Translation.Bind(Arg, new Translation.FormatConverter(App.Localization), Key);

    /// <summary>Binding for code-behind: control.Bind(TextBlock.TextProperty, TrExtension.CreateBinding("Common.Save")).</summary>
    public static BindingBase CreateBinding(string key)
    {
        var localization = App.Localization;
        return new CompiledBinding(Translation.VersionPath)
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
/// and never listen for language changes. Usage: Text="{l:TrKey {Binding ErrorKey}}".
/// With Prefix, the property holds a code: Text="{l:TrKey {Binding Action}, Prefix=Action.}" translates "Action.&lt;code&gt;"
/// and shows the code itself when no translation exists.
/// </summary>
public class TrKeyExtension(BindingBase value) : MarkupExtension
{
    /// <summary>Binding of the key (or code); compiled by the XAML compiler under x:DataType.</summary>
    public BindingBase Value { get; set; } = value;

    public string? Prefix { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        App.Services is null
            ? Prefix ?? string.Empty
            : Translation.Bind(Value, new Translation.KeyConverter(App.Localization), Prefix);
}

/// <summary>
/// The translated error of a form field, for a FormViewModel DataContext: Error="{l:FieldError Username}".
/// Shows "ErrorCode.&lt;code&gt;" with the code's arguments as {0}, {1} (e.g. "max_length|50"); unknown codes show "ErrorCode.invalid".
/// </summary>
public class FieldErrorExtension(string field) : MarkupExtension
{
    public string Field { get; set; } = field;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        App.Services is null
            ? Field
            : Translation.Bind(new ReflectionBinding($"Errors[{Field}]"), new Translation.ErrorCodeConverter(App.Localization), null);
}

/// <summary>
/// An amount in minor units shown with the clinic currency: Text="{l:Money {Binding Total}}" -> "1,250.50 EGP".
/// Refreshes when the language or the clinic currency changes.
/// </summary>
public class MoneyExtension(BindingBase value) : MarkupExtension
{
    /// <summary>Binding of the amount; compiled by the XAML compiler under x:DataType.</summary>
    public BindingBase Value { get; set; } = value;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (App.Services is null)
        {
            return string.Empty;
        }

        var clinic = App.Services.GetRequiredService<ClinicContext>();
        return new MultiBinding
        {
            Bindings =
            {
                Value,
                new CompiledBinding(Translation.VersionPath) { Source = App.Localization },
                new CompiledBinding(Translation.ClinicSettingsPath) { Source = clinic },
            },
            Mode = BindingMode.OneWay,
            Converter = new Translation.MoneyConverter(App.Localization, clinic),
        };
    }
}

internal static class Translation
{
    /// <summary>
    /// LocalizationService.Version as a compiled path, built once: on a language switch every translated text
    /// re-reads it, so it must not go through reflection.
    /// </summary>
    public static readonly CompiledBindingPath VersionPath = CompiledBinding.Create<LocalizationService, int>(l => l.Version).Path!;

    /// <summary>ClinicContext.Settings as a compiled path (every money text listens to it for currency changes).</summary>
    public static readonly CompiledBindingPath ClinicSettingsPath =
        CompiledBinding.Create<ClinicContext, Contracts.Settings.ClinicSettingsDto?>(c => c.Settings).Path!;


    /// <summary>Combines a DataContext binding with LocalizationService.Version so the text refreshes on language changes.</summary>
    public static MultiBinding Bind(BindingBase value, Converter converter, string? parameter) =>
        new()
        {
            Bindings =
            {
                value,
                new CompiledBinding(VersionPath) { Source = converter.Localization },
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
        /// <summary>parameter = optional prefix; with a prefix, missing translations fall back to the raw value.</summary>
        public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 0 || values[0] is not string { Length: > 0 } value)
            {
                return null;
            }

            if (parameter is not string { Length: > 0 } prefix)
            {
                return Localization.Get(value);
            }

            var key = prefix + value;
            var text = Localization.Get(key);
            return text == key ? value : text;
        }
    }

    public sealed class ErrorCodeConverter(LocalizationService localization) : Converter(localization)
    {
        public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 0 || values[0] is not string { Length: > 0 } code)
            {
                return null;
            }

            var parts = code.Split(ErrorCodes.ArgumentSeparator);
            var key = "ErrorCode." + parts[0];
            if (Localization.Get(key) == key)
            {
                return Localization.Get("ErrorCode." + ErrorCodes.Invalid);
            }

            return Localization.Format(key, parts[1..]);
        }
    }

    public sealed class MoneyConverter(LocalizationService localization, ClinicContext clinic) : Converter(localization)
    {
        public override object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            values.Count > 0 && values[0] is long minorUnits
                ? Converters.MoneyFormat.Format(minorUnits, clinic.CurrencyCode, Localization)
                : null;
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
