using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>
/// One error code per form field (ErrorCodes values such as "required" or "max_length|50"), keyed by the
/// field's property name. Views show them translated with Error="{l:FieldError FullName}".
/// </summary>
public sealed class FormErrors : INotifyPropertyChanged
{
    private readonly Dictionary<string, string> _errors = new(StringComparer.OrdinalIgnoreCase);

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? this[string field] => _errors.GetValueOrDefault(field);

    public bool HasErrors => _errors.Count > 0;

    public void Set(string field, string code)
    {
        _errors[field] = code;
        Changed();
    }

    public void Clear(string field)
    {
        if (_errors.Remove(field))
        {
            Changed();
        }
    }

    public void Clear()
    {
        if (_errors.Count > 0)
        {
            _errors.Clear();
            Changed();
        }
    }

    private void Changed()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }
}
