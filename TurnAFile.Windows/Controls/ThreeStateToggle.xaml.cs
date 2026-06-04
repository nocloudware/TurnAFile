using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TurnAFile.Core.Services;

namespace TurnAFile.Windows.Controls;

public partial class ThreeStateToggle : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register("State", typeof(ToggleState), typeof(ThreeStateToggle),
            new PropertyMetadata(ToggleState.Medium, OnStateChanged));

    public ToggleState State
    {
        get => (ToggleState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public event RoutedEventHandler? StateChanged;

    private double _thumbWidth = 24;
    private double _trackWidth = 120;
    private double _leftPosition = 2;
    private double _centerPosition = 0;
    private double _rightPosition = 0;

    private readonly Color _colorHigh = Color.FromRgb(132, 112, 104);
    private readonly Color _colorMedium = Color.FromRgb(93, 111, 69);
    private readonly Color _colorLow = Color.FromRgb(83, 108, 122);

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ThreeStateToggle control)
            control.UpdatePosition((ToggleState)e.NewValue);
    }

    public ThreeStateToggle()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateTrackWidth();
        UpdatePosition(State);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTrackWidth();
        UpdatePosition(State);
    }

    private void UpdateTrackWidth()
    {
        _trackWidth = ActualWidth;
        if (_trackWidth <= 0) _trackWidth = 120;
        _leftPosition = 2;
        _rightPosition = _trackWidth - _thumbWidth - 2;
        _centerPosition = (_trackWidth - _thumbWidth) / 2;
    }

    private double GetPositionForState(ToggleState state) => state switch
    {
        ToggleState.High => _leftPosition,
        ToggleState.Medium => _centerPosition,
        ToggleState.Low => _rightPosition,
        _ => _centerPosition
    };

    private ToggleState GetStateForPosition(double position)
    {
        double distLeft = Math.Abs(position - _leftPosition);
        double distCenter = Math.Abs(position - _centerPosition);
        double distRight = Math.Abs(position - _rightPosition);

        if (distLeft <= distCenter && distLeft <= distRight) return ToggleState.High;
        if (distCenter <= distLeft && distCenter <= distRight) return ToggleState.Medium;
        return ToggleState.Low;
    }

    private void UpdatePosition(ToggleState state)
    {
        if (!IsLoaded) return;

        UpdateTrackWidth();
        double targetX = GetPositionForState(state);
        Canvas.SetLeft(ThumbButton, targetX);

        UpdateVisuals(state, targetX);
        StateChanged?.Invoke(this, new RoutedEventArgs());
    }

    private void UpdateVisuals(ToggleState state, double thumbX)
    {
        Color color;
        double progressWidth;

        switch (state)
        {
            case ToggleState.High:
                color = _colorHigh;
                progressWidth = thumbX + _thumbWidth;
                break;
            case ToggleState.Medium:
                color = _colorMedium;
                progressWidth = thumbX + _thumbWidth;
                break;
            case ToggleState.Low:
                color = _colorLow;
                progressWidth = _trackWidth;
                break;
            default:
                color = _colorMedium;
                progressWidth = thumbX + _thumbWidth;
                break;
        }

        ProgressFill.Background = new SolidColorBrush(color);
        ProgressFill.Width = progressWidth;

        if (ThumbButton.Template.FindName("ThumbBorder", ThumbButton) is Border thumbBorder)
            thumbBorder.Background = new SolidColorBrush(color);
    }

    private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentLeft = Canvas.GetLeft(ThumbButton);
        if (double.IsNaN(currentLeft)) currentLeft = _centerPosition;

        double newX = currentLeft + e.HorizontalChange;
        newX = Math.Max(_leftPosition, Math.Min(newX, _rightPosition));

        var newState = GetStateForPosition(newX);
        if (newState != State)
        {
            State = newState;
        }
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var position = e.GetPosition(this).X;
        double step = (_trackWidth - _thumbWidth) / 2;

        if (position <= step)
            State = ToggleState.High;
        else if (position >= step + _thumbWidth)
            State = ToggleState.Low;
        else
            State = ToggleState.Medium;
    }
}
