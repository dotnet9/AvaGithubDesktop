using System.Diagnostics;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using CodeWF.Log.Core;

namespace AvaGithubDesktop.Controls;

public sealed class OperationLogView : TemplatedControl
{
    private readonly List<LogInfo> _logs = [];
    private readonly RelayCommand _clearCommand;
    private readonly RelayCommand _copyCommand;
    private readonly RelayCommand _openLogFolderCommand;
    private CancellationTokenSource? _readCancellationTokenSource;
    private bool _isReading;
    private ScrollViewer? _scrollViewer;
    private SelectableTextBlock? _textView;

    public static readonly StyledProperty<IBrush?> TimeForegroundProperty =
        AvaloniaProperty.Register<OperationLogView, IBrush?>(nameof(TimeForeground));

    public static readonly StyledProperty<IBrush?> TextForegroundProperty =
        AvaloniaProperty.Register<OperationLogView, IBrush?>(nameof(TextForeground));

    public static readonly StyledProperty<IBrush?> DebugForegroundProperty =
        AvaloniaProperty.Register<OperationLogView, IBrush?>(nameof(DebugForeground));

    public static readonly StyledProperty<IBrush?> InfoForegroundProperty =
        AvaloniaProperty.Register<OperationLogView, IBrush?>(nameof(InfoForeground));

    public static readonly StyledProperty<IBrush?> WarnForegroundProperty =
        AvaloniaProperty.Register<OperationLogView, IBrush?>(nameof(WarnForeground));

    public static readonly StyledProperty<IBrush?> ErrorForegroundProperty =
        AvaloniaProperty.Register<OperationLogView, IBrush?>(nameof(ErrorForeground));

    public static readonly StyledProperty<string> DebugLevelTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(DebugLevelText), "Debug");

    public static readonly StyledProperty<string> InfoLevelTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(InfoLevelText), "Info");

    public static readonly StyledProperty<string> WarnLevelTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(WarnLevelText), "Warn");

    public static readonly StyledProperty<string> ErrorLevelTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(ErrorLevelText), "Error");

    public static readonly StyledProperty<string> FatalLevelTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(FatalLevelText), "Fatal");

    public static readonly StyledProperty<string> CopyTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(CopyText), "Copy");

    public static readonly StyledProperty<string> ClearTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(ClearText), "Clear");

    public static readonly StyledProperty<string> OpenLogFolderTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(OpenLogFolderText), "Open log folder");

    public static readonly StyledProperty<string> FilterPlaceholderTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(FilterPlaceholderText), "Filter logs");

    public static readonly StyledProperty<string> FilterTextProperty =
        AvaloniaProperty.Register<OperationLogView, string>(nameof(FilterText), string.Empty);

    public static readonly DirectProperty<OperationLogView, ICommand> CopyCommandProperty =
        AvaloniaProperty.RegisterDirect<OperationLogView, ICommand>(nameof(CopyCommand), view => view.CopyCommand);

    public static readonly DirectProperty<OperationLogView, ICommand> ClearCommandProperty =
        AvaloniaProperty.RegisterDirect<OperationLogView, ICommand>(nameof(ClearCommand), view => view.ClearCommand);

    public static readonly DirectProperty<OperationLogView, ICommand> OpenLogFolderCommandProperty =
        AvaloniaProperty.RegisterDirect<OperationLogView, ICommand>(nameof(OpenLogFolderCommand), view => view.OpenLogFolderCommand);

    public OperationLogView()
    {
        _copyCommand = new RelayCommand(CopyAsync, () => _textView?.Inlines?.Count > 0);
        _clearCommand = new RelayCommand(ClearAsync, () => _textView?.Inlines?.Count > 0);
        _openLogFolderCommand = new RelayCommand(OpenLogFolderAsync);
    }

    public IBrush? TimeForeground
    {
        get => GetValue(TimeForegroundProperty);
        set => SetValue(TimeForegroundProperty, value);
    }

    public IBrush? TextForeground
    {
        get => GetValue(TextForegroundProperty);
        set => SetValue(TextForegroundProperty, value);
    }

    public IBrush? DebugForeground
    {
        get => GetValue(DebugForegroundProperty);
        set => SetValue(DebugForegroundProperty, value);
    }

    public IBrush? InfoForeground
    {
        get => GetValue(InfoForegroundProperty);
        set => SetValue(InfoForegroundProperty, value);
    }

    public IBrush? WarnForeground
    {
        get => GetValue(WarnForegroundProperty);
        set => SetValue(WarnForegroundProperty, value);
    }

    public IBrush? ErrorForeground
    {
        get => GetValue(ErrorForegroundProperty);
        set => SetValue(ErrorForegroundProperty, value);
    }

    public string DebugLevelText
    {
        get => GetValue(DebugLevelTextProperty);
        set => SetValue(DebugLevelTextProperty, value);
    }

    public string InfoLevelText
    {
        get => GetValue(InfoLevelTextProperty);
        set => SetValue(InfoLevelTextProperty, value);
    }

    public string WarnLevelText
    {
        get => GetValue(WarnLevelTextProperty);
        set => SetValue(WarnLevelTextProperty, value);
    }

    public string ErrorLevelText
    {
        get => GetValue(ErrorLevelTextProperty);
        set => SetValue(ErrorLevelTextProperty, value);
    }

    public string FatalLevelText
    {
        get => GetValue(FatalLevelTextProperty);
        set => SetValue(FatalLevelTextProperty, value);
    }

    public string CopyText
    {
        get => GetValue(CopyTextProperty);
        set => SetValue(CopyTextProperty, value);
    }

    public string ClearText
    {
        get => GetValue(ClearTextProperty);
        set => SetValue(ClearTextProperty, value);
    }

    public string OpenLogFolderText
    {
        get => GetValue(OpenLogFolderTextProperty);
        set => SetValue(OpenLogFolderTextProperty, value);
    }

    public string FilterPlaceholderText
    {
        get => GetValue(FilterPlaceholderTextProperty);
        set => SetValue(FilterPlaceholderTextProperty, value);
    }

    public string FilterText
    {
        get => GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    public ICommand CopyCommand => _copyCommand;

    public ICommand ClearCommand => _clearCommand;

    public ICommand OpenLogFolderCommand => _openLogFolderCommand;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _textView = e.NameScope.Find<SelectableTextBlock>("PART_TextView");
        RenderAllLogs();
        StartReadingLogs();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _readCancellationTokenSource?.Cancel();
        _readCancellationTokenSource?.Dispose();
        _readCancellationTokenSource = null;
        _isReading = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DebugLevelTextProperty
            || change.Property == InfoLevelTextProperty
            || change.Property == WarnLevelTextProperty
            || change.Property == ErrorLevelTextProperty
            || change.Property == FatalLevelTextProperty
            || change.Property == FilterTextProperty)
        {
            RenderAllLogs();
        }
    }

    private void StartReadingLogs()
    {
        if (_isReading)
        {
            return;
        }

        _isReading = true;
        _readCancellationTokenSource = new CancellationTokenSource();
        var token = _readCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var log in Logger.ReadAllUiLogsAsync(token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() => AddLog(log), DispatcherPriority.Background, token);
                }
            }
            catch (OperationCanceledException)
            {
                // 控件卸载时结束读取循环，这是正常生命周期。
            }
        }, token);
    }

    private void AddLog(LogInfo log)
    {
        _logs.Add(log);
        var maxCount = Math.Max(100, (int)Logger.MaxUIDisplayCount);
        var shouldRenderAll = false;
        while (_logs.Count > maxCount)
        {
            _logs.RemoveAt(0);
            shouldRenderAll = true;
        }

        if (shouldRenderAll)
        {
            RenderAllLogs();
        }
        else
        {
            AppendLog(log);
        }

        UpdateCommandState();
    }

    private void RenderAllLogs()
    {
        if (_textView?.Inlines is null)
        {
            return;
        }

        _textView.Inlines.Clear();
        foreach (var log in _logs.Where(MatchesFilter))
        {
            AddRuns(_textView.Inlines, log);
        }

        _scrollViewer?.ScrollToEnd();
        UpdateCommandState();
    }

    private void AppendLog(LogInfo log)
    {
        if (_textView?.Inlines is null)
        {
            return;
        }

        if (!MatchesFilter(log))
        {
            return;
        }

        var isAtBottom = _scrollViewer is null
            || _scrollViewer.Offset.Y >= (_scrollViewer.Extent.Height - _scrollViewer.Viewport.Height - 4);
        AddRuns(_textView.Inlines, log);
        if (isAtBottom)
        {
            _scrollViewer?.ScrollToEnd();
        }
    }

    private void AddRuns(InlineCollection inlines, LogInfo log)
    {
        inlines.Add(new Run(log.RecordTime.ToString(Logger.TimeFormat))
        {
            Foreground = TimeForeground
        });
        inlines.Add(new Run($" [{GetLevelText(log.Level)}] ")
        {
            Foreground = GetLevelForeground(log.Level),
            FontWeight = log.Level == LogType.Fatal ? FontWeight.Bold : FontWeight.Normal
        });
        inlines.Add(new Run(GetLogMessage(log))
        {
            Foreground = TextForeground
        });
        inlines.Add(new Run(Environment.NewLine));
    }

    private bool MatchesFilter(LogInfo log)
    {
        var filter = FilterText.Trim();
        if (filter.Length == 0)
        {
            return true;
        }

        return GetLevelText(log.Level).Contains(filter, StringComparison.OrdinalIgnoreCase)
               || GetLogMessage(log).Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLogMessage(LogInfo log) =>
        string.IsNullOrWhiteSpace(log.FriendlyDescription) ? log.Description : log.FriendlyDescription;

    private string GetLevelText(LogType level)
    {
        return level switch
        {
            LogType.Debug => DebugLevelText,
            LogType.Info => InfoLevelText,
            LogType.Warn => WarnLevelText,
            LogType.Error => ErrorLevelText,
            LogType.Fatal => FatalLevelText,
            _ => level.ToString()
        };
    }

    private IBrush? GetLevelForeground(LogType level)
    {
        return level switch
        {
            LogType.Debug => DebugForeground,
            LogType.Info => InfoForeground,
            LogType.Warn => WarnForeground,
            LogType.Error or LogType.Fatal => ErrorForeground,
            _ => TextForeground
        };
    }

    private async Task CopyAsync()
    {
        if (_textView?.Text is not { Length: > 0 } text)
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private Task ClearAsync()
    {
        _logs.Clear();
        _textView?.Inlines?.Clear();
        UpdateCommandState();
        return Task.CompletedTask;
    }

    private Task OpenLogFolderAsync()
    {
        var directory = Logger.LogDir;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    private void UpdateCommandState()
    {
        _copyCommand.RaiseCanExecuteChanged();
        _clearCommand.RaiseCanExecuteChanged();
    }
}
