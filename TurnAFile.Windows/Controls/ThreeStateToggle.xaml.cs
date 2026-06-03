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

    private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as ThreeStateToggle;
        control?.UpdatePosition((ToggleState)e.NewValue);
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

    private double GetPositionForState(ToggleState state)
    {
        return state switch
        {
            ToggleState.High => _leftPosition,
            ToggleState.Medium => _centerPosition,
            ToggleState.Low => _rightPosition,
            _ => _centerPosition
        };
    }

    private ToggleState GetStateForPosition(double position)
    {
        double distLeft = Math.Abs(position - _leftPosition);
        double distCenter = Math.Abs(position - _centerPosition);
        double distRight = Math.Abs(position - _rightPosition);

        if (distLeft <= distCenter && distLeft <= distRight)
            return ToggleState.High;
        else if (distCenter <= distLeft && distCenter <= distRight)
            return ToggleState.Medium;
        else
            return ToggleState.Low;
    }

    private void UpdatePosition(ToggleState state)
    {
        if (!IsLoaded) return;

        UpdateTrackWidth();

        double targetX = GetPositionForState(state);

        Canvas.SetLeft(ThumbButton, targetX);

        UpdateTrackColor(state);

        StateChanged?.Invoke(this, new RoutedEventArgs());
    }

    private void UpdateTrackColor(ToggleState state)
    {
        var textBrush = Application.Current.Resources["TextPrimaryBrush"] as SolidColorBrush;
        if (textBrush != null && textBrush.Color == Colors.White)
        {
            TrackBorder.Background = state switch
            {
                ToggleState.High => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                ToggleState.Medium => new SolidColorBrush(Color.FromRgb(107, 142, 35)),
                ToggleState.Low => new SolidColorBrush(Color.FromRgb(205, 92, 92)),
                _ => TrackBorder.Background
            };
        }
        else
        {
            TrackBorder.Background = Application.Current.Resources["BorderBrush"] as Brush;
        }
    }

    private void OnThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        double currentLeft = Canvas.GetLeft(ThumbButton);
        if (double.IsNaN(currentLeft)) currentLeft = _centerPosition;

        double newX = currentLeft + e.HorizontalChange;
        double maxX = _trackWidth - _thumbWidth - 2;
        newX = Math.Max(2, Math.Min(newX, maxX));

        Canvas.SetLeft(ThumbButton, newX);

        var newState = GetStateForPosition(newX);
        if (newState != State)
        {
            State = newState;
        }
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