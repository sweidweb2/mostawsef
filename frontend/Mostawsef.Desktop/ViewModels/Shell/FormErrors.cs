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
    private int _batchDepth;
    private bool _batchDirty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string? this[string field] => _errors.GetValueOrDefault(field);

    public bool HasErrors => _errors.Count > 0;

    /// <summary>Coalesces notifications until dispose. Nested batches are supported.</summary>
    public IDisposable Batch()
    {
        _batchDepth++;
        return new BatchScope(this);
    }

    public void Set(string field, string code)
    {
        if (_errors.TryGetValue(field, out var existing)
            && string.Equals(existing, code, StringComparison.Ordinal))
        {
            return;
        }

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
        if (_batchDepth > 0)
        {
            _batchDirty = true;
            return;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }

    private void EndBatch()
    {
        _batchDepth--;
        if (_batchDepth > 0 || !_batchDirty)
        {
            return;
        }

        _batchDirty = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasErrors)));
    }

    private sealed class BatchScope(FormErrors errors) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            errors.EndBatch();
        }
    }
}
