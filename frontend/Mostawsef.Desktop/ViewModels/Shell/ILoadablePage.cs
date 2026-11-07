using System.Threading.Tasks;

namespace Mostawsef.Desktop.ViewModels.Shell;

/// <summary>A page that loads data when the shell opens it.</summary>
public interface ILoadablePage
{
    Task InitializeAsync();
}
