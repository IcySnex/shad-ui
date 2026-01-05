using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactivity;
using System;

namespace ShadUI.Utilities;

/// <summary>
/// Provides smooth, animated scrolling behavior for a ScrollViewer in response to pointer wheel input.
/// </summary>
public sealed class SmoothScrollBehavior : StyledElementBehavior<ScrollViewer>
{
    const double BaseStepSize = 75;
    const double SpeedMultiplier = 1.2;
    const double Friction = 0.000005;


    TopLevel? topLevel;

    double targetX, currentX;
    double targetY, currentY;

    bool isLoopRunning;
    DateTime lastWheelEvent = DateTime.MinValue;
    TimeSpan lastFrameTime = TimeSpan.FromSeconds(1.0 / 60.0); // 60 FPS as fallback for first frame


    /// <summary>
    /// Invoked when the behavior is attached to its associated object.
    /// </summary>
    protected override void OnAttached()
    {
        base.OnAttached();

        AssociatedObject?.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Called when the behavior is being detached from its associated object.
    /// </summary>
    protected override void OnDetaching()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
        isLoopRunning = false;

        base.OnDetaching();
    }


    void OnPointerWheelChanged(
        object? sender,
        PointerWheelEventArgs e)
    {
        if (e.Handled || // already handled
            AssociatedObject is null || TopLevel.GetTopLevel(AssociatedObject) is not TopLevel topLevel || // ??
            Math.Abs(e.Delta.Y) < 1.0 && e.Delta.Y != 0 || Math.Abs(e.Delta.X) < 1.0 && e.Delta.X != 0) // input is high precision scroll (e.g. trackpad)
        {
            isLoopRunning = false;
            return;
        }
        
        this.topLevel = topLevel;
        Visual? source = e.Source as Visual;

        // Flyouts
        IRenderRoot? sourceRoot = source?.GetVisualRoot();
        IRenderRoot? myBabyBooRoot = AssociatedObject.GetVisualRoot();

        if (sourceRoot != myBabyBooRoot)
            return; // this event is from a popup/flyout. TRAP IT!!! >:)

        // Nested ScrollViewers
        bool isShiftPressed = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        while (source is not null && source != AssociatedObject)
        {
            if (source is ScrollViewer { IsVisible: true } inner)
            {
                bool innerHasHorizontal = inner.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
                bool innerHasVertical = inner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

                double tryingToMoveX = e.Delta.X + (isShiftPressed ? e.Delta.Y : 0);
                double tryingToMoveY = isShiftPressed ? 0 : e.Delta.Y;
                
                if (innerHasHorizontal && !innerHasVertical && !isShiftPressed)
                {
                    tryingToMoveX += tryingToMoveY;
                    tryingToMoveY = 0;
                }
                
                bool canMoveX = (tryingToMoveX > 0 && inner.Offset.X > 0) || 
                    (tryingToMoveX < 0 && inner.Offset.X < (inner.Extent.Width - inner.Viewport.Width));
                bool canMoveY = (tryingToMoveY > 0 && inner.Offset.Y > 0) || 
                    (tryingToMoveY < 0 && inner.Offset.Y < (inner.Extent.Height - inner.Viewport.Height));
                
                if (canMoveX || canMoveY)
                    return; // Trap it! The inner child can handle this movement.
            }

            source = source.GetVisualParent();
        }

        // Sync current position if we were idle
        if (!isLoopRunning) 
        {
            currentX = AssociatedObject.Offset.X;
            targetX = currentX;

            currentY = AssociatedObject.Offset.Y;
            targetY = currentY;
        }

        // Acceleration
        DateTime now = DateTime.UtcNow;
        double elapsed = (now - lastWheelEvent).TotalMilliseconds;
        lastWheelEvent = now;

        double acceleration = elapsed < 100 ? SpeedMultiplier : 1.0;

        // Update targets
        bool parentHasHorizontal = AssociatedObject.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        bool parentHasVertical = AssociatedObject.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        bool parentIsHorizontalOnly = parentHasHorizontal && !parentHasVertical;

        if (isShiftPressed || parentIsHorizontalOnly)
        {
            targetX -= (e.Delta.Y * BaseStepSize * acceleration);
        }
        else
        {
            targetY -= (e.Delta.Y * BaseStepSize * acceleration);

            // Support for tilt-wheels or touchpads that send actual Delta.X ??? (idk i cant really test)
            if (e.Delta.X != 0)
                targetX -= (e.Delta.X * BaseStepSize * acceleration);
        }

        StartAnimationLoop();
        e.Handled = true;
    }


    void StartAnimationLoop()
    {
        if (isLoopRunning || topLevel is null)
            return;

        isLoopRunning = true;
        lastFrameTime = TimeSpan.FromSeconds(1.0 / 60.0);

        topLevel.RequestAnimationFrame(OnFrameTick);
    }

    void OnFrameTick(
        TimeSpan time)
    {
        if (!isLoopRunning ||
            topLevel is null ||
            AssociatedObject is null)
            return;

        // Calculate delta time
        double dt = (time - lastFrameTime).TotalSeconds;
        lastFrameTime = time;
        dt = Math.Min(dt, 0.1);

        // Clamp target (doing it in frame tick and not before in case content resizes mid-scroll)
        double maxX = Math.Max(AssociatedObject.Extent.Width - AssociatedObject.Viewport.Width, 0);
        double maxY = Math.Max(AssociatedObject.Extent.Height - AssociatedObject.Viewport.Height, 0);

        targetX = Math.Clamp(targetX, 0, maxX);
        targetY = Math.Clamp(targetY, 0, maxY);

        // Calculate new positions
        double distY = targetY - currentY;
        double distX = targetX - currentX;

        currentY += distY * (1.0 - Math.Pow(Friction, dt));
        currentX += distX * (1.0 - Math.Pow(Friction, dt));

        // Stop condition (Check if BOTH have arrived)
        if (Math.Abs(distY) < 0.1 && Math.Abs(distX) < 0.1)
        {
            currentY = targetY;
            currentX = targetX;

            AssociatedObject.Offset = new(currentX, currentY);
            isLoopRunning = false;
            return;
        }

        // Movy movy groovy groovy!
        AssociatedObject.Offset = new(currentX, currentY);

        // Queue next frame
        topLevel.RequestAnimationFrame(OnFrameTick);
    }
}