using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;

namespace AvaGithubDesktop.Controls;

public sealed class ImageDiffViewer : TemplatedControl
{
    public static readonly StyledProperty<string?> PreviousImagePathProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string?>(nameof(PreviousImagePath));

    public static readonly StyledProperty<string?> CurrentImagePathProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string?>(nameof(CurrentImagePath));

    public static readonly StyledProperty<string> PreviousLabelProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(PreviousLabel), "Deleted");

    public static readonly StyledProperty<string> CurrentLabelProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(CurrentLabel), "Added");

    public static readonly StyledProperty<string> TwoUpTextProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(TwoUpText), "2-up");

    public static readonly StyledProperty<string> SwipeTextProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(SwipeText), "Swipe");

    public static readonly StyledProperty<string> OnionSkinTextProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(OnionSkinText), "Onion Skin");

    public static readonly StyledProperty<string> DifferenceTextProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(DifferenceText), "Difference");

    public static readonly StyledProperty<string> WidthLabelProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(WidthLabel), "W");

    public static readonly StyledProperty<string> HeightLabelProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(HeightLabel), "H");

    public static readonly StyledProperty<string> SizeLabelProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(SizeLabel), "Size");

    public static readonly StyledProperty<string> DiffLabelProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(DiffLabel), "Diff");

    public static readonly StyledProperty<string> NoSizeDifferenceTextProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(NoSizeDifferenceText), "No size difference");

    public static readonly StyledProperty<string> EmptyImageTextProperty =
        AvaloniaProperty.Register<ImageDiffViewer, string>(nameof(EmptyImageText), "No image content");

    public static readonly StyledProperty<ImageDiffMode> SelectedModeProperty =
        AvaloniaProperty.Register<ImageDiffViewer, ImageDiffMode>(nameof(SelectedMode));

    public static readonly DirectProperty<ImageDiffViewer, Bitmap?> PreviousImageProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, Bitmap?>(nameof(PreviousImage), viewer => viewer.PreviousImage);

    public static readonly DirectProperty<ImageDiffViewer, Bitmap?> CurrentImageProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, Bitmap?>(nameof(CurrentImage), viewer => viewer.CurrentImage);

    public static readonly DirectProperty<ImageDiffViewer, string> PreviousMetadataProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, string>(nameof(PreviousMetadata), viewer => viewer.PreviousMetadata);

    public static readonly DirectProperty<ImageDiffViewer, string> CurrentMetadataProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, string>(nameof(CurrentMetadata), viewer => viewer.CurrentMetadata);

    public static readonly DirectProperty<ImageDiffViewer, string> SizeDiffTextProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, string>(nameof(SizeDiffText), viewer => viewer.SizeDiffText);

    public static readonly DirectProperty<ImageDiffViewer, bool> HasPreviousImageProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(HasPreviousImage), viewer => viewer.HasPreviousImage);

    public static readonly DirectProperty<ImageDiffViewer, bool> HasCurrentImageProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(HasCurrentImage), viewer => viewer.HasCurrentImage);

    public static readonly DirectProperty<ImageDiffViewer, bool> HasNoPreviousImageProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(HasNoPreviousImage), viewer => viewer.HasNoPreviousImage);

    public static readonly DirectProperty<ImageDiffViewer, bool> HasNoCurrentImageProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(HasNoCurrentImage), viewer => viewer.HasNoCurrentImage);

    public static readonly DirectProperty<ImageDiffViewer, bool> IsTwoUpSelectedProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(IsTwoUpSelected), viewer => viewer.IsTwoUpSelected);

    public static readonly DirectProperty<ImageDiffViewer, bool> IsSwipeSelectedProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(IsSwipeSelected), viewer => viewer.IsSwipeSelected);

    public static readonly DirectProperty<ImageDiffViewer, bool> IsOnionSkinSelectedProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(IsOnionSkinSelected), viewer => viewer.IsOnionSkinSelected);

    public static readonly DirectProperty<ImageDiffViewer, bool> IsDifferenceSelectedProperty =
        AvaloniaProperty.RegisterDirect<ImageDiffViewer, bool>(nameof(IsDifferenceSelected), viewer => viewer.IsDifferenceSelected);

    private Bitmap? _previousImage;
    private Bitmap? _currentImage;
    private string _previousMetadata = string.Empty;
    private string _currentMetadata = string.Empty;
    private string _sizeDiffText = string.Empty;
    private bool _hasPreviousImage;
    private bool _hasCurrentImage;
    private bool _hasNoPreviousImage = true;
    private bool _hasNoCurrentImage = true;
    private bool _isTwoUpSelected = true;
    private bool _isSwipeSelected;
    private bool _isOnionSkinSelected;
    private bool _isDifferenceSelected;

    public ImageDiffViewer()
    {
        SelectTwoUpCommand = new RelayCommand(() => SelectModeAsync(ImageDiffMode.TwoUp));
        SelectSwipeCommand = new RelayCommand(() => SelectModeAsync(ImageDiffMode.Swipe));
        SelectOnionSkinCommand = new RelayCommand(() => SelectModeAsync(ImageDiffMode.OnionSkin));
        SelectDifferenceCommand = new RelayCommand(() => SelectModeAsync(ImageDiffMode.Difference));
    }

    public string? PreviousImagePath
    {
        get => GetValue(PreviousImagePathProperty);
        set => SetValue(PreviousImagePathProperty, value);
    }

    public string? CurrentImagePath
    {
        get => GetValue(CurrentImagePathProperty);
        set => SetValue(CurrentImagePathProperty, value);
    }

    public string PreviousLabel
    {
        get => GetValue(PreviousLabelProperty);
        set => SetValue(PreviousLabelProperty, value);
    }

    public string CurrentLabel
    {
        get => GetValue(CurrentLabelProperty);
        set => SetValue(CurrentLabelProperty, value);
    }

    public string TwoUpText
    {
        get => GetValue(TwoUpTextProperty);
        set => SetValue(TwoUpTextProperty, value);
    }

    public string SwipeText
    {
        get => GetValue(SwipeTextProperty);
        set => SetValue(SwipeTextProperty, value);
    }

    public string OnionSkinText
    {
        get => GetValue(OnionSkinTextProperty);
        set => SetValue(OnionSkinTextProperty, value);
    }

    public string DifferenceText
    {
        get => GetValue(DifferenceTextProperty);
        set => SetValue(DifferenceTextProperty, value);
    }

    public string WidthLabel
    {
        get => GetValue(WidthLabelProperty);
        set => SetValue(WidthLabelProperty, value);
    }

    public string HeightLabel
    {
        get => GetValue(HeightLabelProperty);
        set => SetValue(HeightLabelProperty, value);
    }

    public string SizeLabel
    {
        get => GetValue(SizeLabelProperty);
        set => SetValue(SizeLabelProperty, value);
    }

    public string DiffLabel
    {
        get => GetValue(DiffLabelProperty);
        set => SetValue(DiffLabelProperty, value);
    }

    public string NoSizeDifferenceText
    {
        get => GetValue(NoSizeDifferenceTextProperty);
        set => SetValue(NoSizeDifferenceTextProperty, value);
    }

    public string EmptyImageText
    {
        get => GetValue(EmptyImageTextProperty);
        set => SetValue(EmptyImageTextProperty, value);
    }

    public ImageDiffMode SelectedMode
    {
        get => GetValue(SelectedModeProperty);
        set => SetValue(SelectedModeProperty, value);
    }

    public Bitmap? PreviousImage
    {
        get => _previousImage;
        private set => SetAndRaise(PreviousImageProperty, ref _previousImage, value);
    }

    public Bitmap? CurrentImage
    {
        get => _currentImage;
        private set => SetAndRaise(CurrentImageProperty, ref _currentImage, value);
    }

    public string PreviousMetadata
    {
        get => _previousMetadata;
        private set => SetAndRaise(PreviousMetadataProperty, ref _previousMetadata, value);
    }

    public string CurrentMetadata
    {
        get => _currentMetadata;
        private set => SetAndRaise(CurrentMetadataProperty, ref _currentMetadata, value);
    }

    public string SizeDiffText
    {
        get => _sizeDiffText;
        private set => SetAndRaise(SizeDiffTextProperty, ref _sizeDiffText, value);
    }

    public bool HasPreviousImage
    {
        get => _hasPreviousImage;
        private set => SetAndRaise(HasPreviousImageProperty, ref _hasPreviousImage, value);
    }

    public bool HasCurrentImage
    {
        get => _hasCurrentImage;
        private set => SetAndRaise(HasCurrentImageProperty, ref _hasCurrentImage, value);
    }

    public bool HasNoPreviousImage
    {
        get => _hasNoPreviousImage;
        private set => SetAndRaise(HasNoPreviousImageProperty, ref _hasNoPreviousImage, value);
    }

    public bool HasNoCurrentImage
    {
        get => _hasNoCurrentImage;
        private set => SetAndRaise(HasNoCurrentImageProperty, ref _hasNoCurrentImage, value);
    }

    public bool IsTwoUpSelected
    {
        get => _isTwoUpSelected;
        private set => SetAndRaise(IsTwoUpSelectedProperty, ref _isTwoUpSelected, value);
    }

    public bool IsSwipeSelected
    {
        get => _isSwipeSelected;
        private set => SetAndRaise(IsSwipeSelectedProperty, ref _isSwipeSelected, value);
    }

    public bool IsOnionSkinSelected
    {
        get => _isOnionSkinSelected;
        private set => SetAndRaise(IsOnionSkinSelectedProperty, ref _isOnionSkinSelected, value);
    }

    public bool IsDifferenceSelected
    {
        get => _isDifferenceSelected;
        private set => SetAndRaise(IsDifferenceSelectedProperty, ref _isDifferenceSelected, value);
    }

    public ICommand SelectTwoUpCommand { get; }

    public ICommand SelectSwipeCommand { get; }

    public ICommand SelectOnionSkinCommand { get; }

    public ICommand SelectDifferenceCommand { get; }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PreviousImagePathProperty)
        {
            PreviousImage = LoadBitmap(PreviousImagePath);
            HasPreviousImage = PreviousImage is not null;
            HasNoPreviousImage = !HasPreviousImage;
            RefreshMetadata();
        }

        if (change.Property == CurrentImagePathProperty)
        {
            CurrentImage = LoadBitmap(CurrentImagePath);
            HasCurrentImage = CurrentImage is not null;
            HasNoCurrentImage = !HasCurrentImage;
            RefreshMetadata();
        }

        if (change.Property == SelectedModeProperty)
        {
            RefreshModeState();
        }

        if (change.Property == WidthLabelProperty
            || change.Property == HeightLabelProperty
            || change.Property == SizeLabelProperty
            || change.Property == DiffLabelProperty
            || change.Property == NoSizeDifferenceTextProperty)
        {
            RefreshMetadata();
        }
    }

    private Task SelectModeAsync(ImageDiffMode mode)
    {
        SelectedMode = mode;
        return Task.CompletedTask;
    }

    private void RefreshModeState()
    {
        IsTwoUpSelected = SelectedMode == ImageDiffMode.TwoUp;
        IsSwipeSelected = SelectedMode == ImageDiffMode.Swipe;
        IsOnionSkinSelected = SelectedMode == ImageDiffMode.OnionSkin;
        IsDifferenceSelected = SelectedMode == ImageDiffMode.Difference;
    }

    private void RefreshMetadata()
    {
        PreviousMetadata = FormatMetadata(PreviousImage, PreviousImagePath);
        CurrentMetadata = FormatMetadata(CurrentImage, CurrentImagePath);
        SizeDiffText = FormatSizeDifference(PreviousImagePath, CurrentImagePath);
    }

    private string FormatMetadata(Bitmap? image, string? filePath)
    {
        if (image is null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return EmptyImageText;
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            "{0}: {1}px | {2}: {3}px | {4}: {5}",
            WidthLabel,
            image.PixelSize.Width,
            HeightLabel,
            image.PixelSize.Height,
            SizeLabel,
            FormatBytes(new FileInfo(filePath).Length));
    }

    private string FormatSizeDifference(string? previousPath, string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(previousPath)
            || string.IsNullOrWhiteSpace(currentPath)
            || !File.Exists(previousPath)
            || !File.Exists(currentPath))
        {
            return string.Empty;
        }

        var previousSize = new FileInfo(previousPath).Length;
        var currentSize = new FileInfo(currentPath).Length;
        var delta = currentSize - previousSize;
        if (delta == 0)
        {
            return $"{DiffLabel}: {NoSizeDifferenceText}";
        }

        var percent = previousSize == 0
            ? 100
            : Math.Abs((int)Math.Round((double)currentSize / previousSize * 100));
        var sign = delta > 0 ? "+" : string.Empty;
        return $"{DiffLabel}: {sign}{FormatBytes(delta)} ({percent}%)";
    }

    private static Bitmap? LoadBitmap(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        var sign = bytes < 0 ? "-" : string.Empty;
        var value = Math.Abs((double)bytes);
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        var format = unitIndex == 0 ? "0" : "0.##";
        return $"{sign}{value.ToString(format, CultureInfo.CurrentCulture)} {units[unitIndex]}";
    }
}
