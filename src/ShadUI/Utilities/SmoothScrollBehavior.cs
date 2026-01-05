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
    const double BaseStepSize = 40;
    const double SpeedMultiplier = 1.2;
    const double Friction = 0.000005;


    TopLevel? topLevel;

    double targetX, currentX;
    double targetY, currentY;

    bool isLoopRunning;
    DateTime lastWheelEvent = DateTime.MinValue;
    TimeSpan lastFrameTime = TimeSpan.Zero;


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
        double dx = e.Delta.X;
        double dy = e.Delta.Y;
        
        if (e.Handled || AssociatedObject is null || TopLevel.GetTopLevel(AssociatedObject) is not TopLevel topLevel)
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
            if (source is ScrollViewer inner && inner.IsVisible)
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
        
        double accelartion = (elapsed < 80) ? Math.Min(1.0, 1.0 + (80 - elapsed) / 40.0) : 0.75;
        
        double stepX = e.Delta.X * BaseStepSize * accelartion;
        double stepY = e.Delta.Y * BaseStepSize * accelartion;

        if (Math.Sign(stepY) != Math.Sign(targetY - currentY) && Math.Abs(stepY) > 0.1) // Direction Snap
            currentY = targetY;
        if (Math.Sign(stepX) != Math.Sign(targetX - currentX) && Math.Abs(stepX) > 0.1)
            currentX = targetX;

        // 6. Update Targets
        bool isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        bool canScrollHorizontally = AssociatedObject.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        bool canScrollVertically = AssociatedObject.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

        // Logic for Shift-scroll or Horizontal-only viewers
        if (isShift || (canScrollHorizontally && !canScrollVertically))
        {
            targetX -= (stepY + stepX); 
        }
        else
        {
            targetX -= stepX;
            targetY -= stepY;
        }

        StartAnimationLoop();
        e.Handled = true;
    }


    void StartAnimationLoop()
    {
        if (isLoopRunning || topLevel is null)
            return;

        isLoopRunning = true;
        lastFrameTime = TimeSpan.Zero;
        
        topLevel.RequestAnimationFrame(time =>
        {
            lastFrameTime = time;
            topLevel.RequestAnimationFrame(OnFrameTick);
        });
    }

    const double Smoothing = 10.0; 

    void OnFrameTick(TimeSpan time)
    {
        if (!isLoopRunning || AssociatedObject is null)
            return;

        double dt = (time - lastFrameTime).TotalSeconds;
        lastFrameTime = time;
        dt = Math.Min(dt, 0.1);

        // Clamp target (doing it in frame tick and not before in case content resizes mid-scroll)
        double maxX = Math.Max(AssociatedObject.Extent.Width - AssociatedObject.Viewport.Width, 0);
        double maxY = Math.Max(AssociatedObject.Extent.Height - AssociatedObject.Viewport.Height, 0);

        targetX = Math.Clamp(targetX, 0, maxX);
        targetY = Math.Clamp(targetY, 0, maxY);
        
        // Calculate new positions
        double dx = targetX - currentX;
        double dy = targetY - currentY;
        
        if (Math.Abs(dx) < 0.01 && Math.Abs(dy) < 0.01) // Stop if we are close enough to the target
        {
            AssociatedObject.Offset = new(targetX, targetY);
            isLoopRunning = false;
            return;
        }

        double lerpFactor = 1.0 - Math.Exp(-Smoothing * dt);
        currentX += dx * lerpFactor;
        currentY += dy * lerpFactor;

        AssociatedObject.Offset = new(currentX, currentY);
        topLevel?.RequestAnimationFrame(OnFrameTick);
    }
}