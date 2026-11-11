using Mostawsef.Contracts.Common;
using Mostawsef.Desktop.ViewModels.Shell;

namespace Mostawsef.Desktop.Tests.Forms;

public class FormErrorsTests
{
    [Fact]
    public void Set_skips_notification_when_the_code_is_unchanged()
    {
        var errors = new FormErrors();
        errors.Set("FullName", ErrorCodes.Required);
        var raises = 0;
        errors.PropertyChanged += (_, _) => raises++;

        errors.Set("FullName", ErrorCodes.Required);

        Assert.Equal(0, raises);
    }

    [Fact]
    public void Batch_raises_once_for_many_sets()
    {
        var errors = new FormErrors();
        var raises = 0;
        errors.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item[]")
            {
                raises++;
            }
        };

        using (errors.Batch())
        {
            errors.Set("A", ErrorCodes.Required);
            errors.Set("B", ErrorCodes.Required);
            errors.Set("C", ErrorCodes.Invalid);
        }

        Assert.Equal(1, raises);
        Assert.True(errors.HasErrors);
        Assert.Equal(ErrorCodes.Required, errors["A"]);
    }
}
