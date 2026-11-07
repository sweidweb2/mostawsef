using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Metadata;
using CommunityToolkit.Mvvm.Input;

namespace Mostawsef.Desktop.Components;

/// <summary>Multi-step flow: Stepper + current WizardPage + Back / Next / Finish buttons.</summary>
public class Wizard : TemplatedControl
{
    public static readonly StyledProperty<int> CurrentIndexProperty =
        AvaloniaProperty.Register<Wizard, int>(nameof(CurrentIndex));

    public static readonly StyledProperty<ICommand?> FinishCommandProperty =
        AvaloniaProperty.Register<Wizard, ICommand?>(nameof(FinishCommand));

    public static readonly DirectProperty<Wizard, WizardPage?> CurrentPageProperty =
        AvaloniaProperty.RegisterDirect<Wizard, WizardPage?>(nameof(CurrentPage), o => o.CurrentPage);

    public static readonly DirectProperty<Wizard, IReadOnlyList<string>> StepTitlesProperty =
        AvaloniaProperty.RegisterDirect<Wizard, IReadOnlyList<string>>(nameof(StepTitles), o => o.StepTitles);

    public static readonly DirectProperty<Wizard, bool> IsLastPageProperty =
        AvaloniaProperty.RegisterDirect<Wizard, bool>(nameof(IsLastPage), o => o.IsLastPage);

    public static readonly DirectProperty<Wizard, ICommand> BackCommandProperty =
        AvaloniaProperty.RegisterDirect<Wizard, ICommand>(nameof(BackCommand), o => o.BackCommand);

    public static readonly DirectProperty<Wizard, ICommand> NextCommandProperty =
        AvaloniaProperty.RegisterDirect<Wizard, ICommand>(nameof(NextCommand), o => o.NextCommand);

    private WizardPage? _currentPage;
    private IReadOnlyList<string> _stepTitles = [];
    private bool _isLastPage;
    private readonly RelayCommand _backCommand;
    private readonly RelayCommand _nextCommand;

    public Wizard()
    {
        _backCommand = new RelayCommand(() => CurrentIndex--, () => CurrentIndex > 0);
        _nextCommand = new RelayCommand(GoNext);
        Pages.CollectionChanged += (_, _) => Refresh();
    }

    [Content]
    public AvaloniaList<WizardPage> Pages { get; } = [];

    public int CurrentIndex
    {
        get => GetValue(CurrentIndexProperty);
        set => SetValue(CurrentIndexProperty, value);
    }

    public ICommand? FinishCommand
    {
        get => GetValue(FinishCommandProperty);
        set => SetValue(FinishCommandProperty, value);
    }

    public WizardPage? CurrentPage
    {
        get => _currentPage;
        private set => SetAndRaise(CurrentPageProperty, ref _currentPage, value);
    }

    public IReadOnlyList<string> StepTitles
    {
        get => _stepTitles;
        private set => SetAndRaise(StepTitlesProperty, ref _stepTitles, value);
    }

    public bool IsLastPage
    {
        get => _isLastPage;
        private set => SetAndRaise(IsLastPageProperty, ref _isLastPage, value);
    }

    public ICommand BackCommand => _backCommand;

    public ICommand NextCommand => _nextCommand;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == CurrentIndexProperty)
        {
            Refresh();
        }
    }

    private void GoNext()
    {
        if (IsLastPage)
        {
            if (FinishCommand?.CanExecute(null) == true)
            {
                FinishCommand.Execute(null);
            }
        }
        else
        {
            CurrentIndex++;
        }
    }

    private void Refresh()
    {
        StepTitles = Pages.Select(p => p.Title ?? string.Empty).ToList();
        CurrentPage = CurrentIndex >= 0 && CurrentIndex < Pages.Count ? Pages[CurrentIndex] : null;
        IsLastPage = CurrentIndex >= Pages.Count - 1;
        _backCommand.NotifyCanExecuteChanged();
    }
}

public class WizardPage : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<WizardPage, string?>(nameof(Title));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
