using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using System;
using System.Runtime.InteropServices;
using Avalonia.Controls.Primitives;
using Avalonia.Rendering;
using Avalonia.VisualTree;

namespace ShadUI.Utilities;

/// <summary>
/// Provides smooth, animated scrolling behavior for a ScrollViewer in response to pointer wheel input.
/// </summary>
public sealed class SmoothScrollBehavior : StyledElementBehavior<ScrollViewer>
{
    // The base size for a single scroll step: The higher, the faster.
    const double BaseStepSize = 40; 
    
    // The SmoothingFactor factor: Lower = silkier, Higher = snappier.
    const double SmoothingFactor = 50.0; 

    
    TopLevel? topLevel;
    
    double targetX, currentX;
    double targetY, currentY;
    
    bool isLoopRunning;
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
        if (e.Handled || AssociatedObject is null)
            return;
        
        topLevel ??= TopLevel.GetTopLevel(AssociatedObject);
        if (topLevel is null)
            return;
        
        // Prevent scroll direction leaks
        double dx = e.Delta.X;
        double dy = e.Delta.Y;
        
        if (Math.Abs(dy) > Math.Abs(dx))
            dx = 0;
        else if (Math.Abs(dx) > Math.Abs(dy))
            dy = 0;

        // Check if this event is actually for us
        Visual? source = e.Source as Visual;
        
        //  Flyouts
        IRenderRoot? sourceRoot = source?.GetVisualRoot();
        IRenderRoot? myBabyBooRoot = AssociatedObject.GetVisualRoot();

        if (sourceRoot != myBabyBooRoot)
            return; // this event is from a popup/flyout. TRAP IT!!! >:)

        //  Chaining
        bool isShiftPressed = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        while (source is not null && source != AssociatedObject)
        {
            if (source is ScrollViewer inner && inner.IsVisible)
            {
                bool innerHasHorizontal = inner.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
                bool innerHasVertical = inner.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

                double tryingToMoveX = dx + (isShiftPressed ? dy : 0);
                double tryingToMoveY = isShiftPressed ? 0 : dy;
                
                if (innerHasHorizontal && !innerHasVertical && !isShiftPressed)
                {
                    tryingToMoveX += tryingToMoveY;
                    tryingToMoveY = 0;
                }
                
                bool canMoveX = (tryingToMoveX > 0 && inner.Offset.X > 0) || (tryingToMoveX < 0 && inner.Offset.X < (inner.Extent.Width - inner.Viewport.Width));
                bool canMoveY = (tryingToMoveY > 0 && inner.Offset.Y > 0) || (tryingToMoveY < 0 && inner.Offset.Y < (inner.Extent.Height - inner.Viewport.Height));
                
                if (canMoveX || canMoveY)
                    return; // Trap it! The inner child can handle this movement itself
            }

            source = source.GetVisualParent();
        }
        
        // Sync current position if we were idle
        if (!isLoopRunning)
        {
            currentX = AssociatedObject.Offset.X;
            currentY = AssociatedObject.Offset.Y;
            targetX = currentX;
            targetY = currentY;
        }

        // Updating target
        bool hasHorizontal = AssociatedObject.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;
        bool hasVertical = AssociatedObject.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
        
        if (Math.Abs(dx) > 0) 
        {
            targetX -= dx * BaseStepSize;
            targetY -= dy * BaseStepSize;
        }
        else if (isShiftPressed || (hasHorizontal && !hasVertical))
        {
            targetX -= dy * BaseStepSize;
        }
        else
        {
            targetY -= dy * BaseStepSize;
        }
        
        StartAnimationLoop();
        e.Handled = true;
    }

    
    void StartAnimationLoop()
    {
        if (isLoopRunning || topLevel is null)
            return;
        
        isLoopRunning = true;
        topLevel.RequestAnimationFrame(time =>
        {
            lastFrameTime = time;
            OnFrameTick(time);
        });
    }

    void OnFrameTick(
        TimeSpan time)
    {
        if (!isLoopRunning || AssociatedObject is null || topLevel is null)
            return;

        double dt = (time - lastFrameTime).TotalSeconds;
        lastFrameTime = time;

        // Clamp target (doing it in frame tick and not before in case content resizes mid-scroll)
        targetX = Math.Clamp(targetX,
            min: 0,
            max: Math.Max(AssociatedObject.Extent.Width - AssociatedObject.Viewport.Width, 0));
        targetY = Math.Clamp(targetY,
            min: 0,
            max: Math.Max(AssociatedObject.Extent.Height - AssociatedObject.Viewport.Height, 0));
        
        // Calculate positions
        double dx = targetX - currentX;
        double dy = targetY - currentY;
        
        if (Math.Abs(dx) < 0.1 && Math.Abs(dy) < 0.1) // stop if too small
        {
            AssociatedObject.Offset = new(targetX, targetY);
            isLoopRunning = false;

            return;
        }

        double factor = 1.0 - Math.Exp(-SmoothingFactor * dt);
        currentX += dx * factor;
        currentY += dy * factor;
        
        // Update positions
        AssociatedObject.Offset = new(currentX, currentY);
        topLevel.RequestAnimationFrame(OnFrameTick);
    }
}