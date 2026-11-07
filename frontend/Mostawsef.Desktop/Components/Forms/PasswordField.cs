using System;
using Avalonia.Controls;

namespace Mostawsef.Desktop.Components;

/// <summary>Masked TextBox with the Fluent show/hide (reveal) button.</summary>
public class PasswordField : TextBox
{
    public PasswordField()
    {
        PasswordChar = '•';
        Classes.Add("revealPasswordButton");
    }

    protected override Type StyleKeyOverride => typeof(TextBox);
}
