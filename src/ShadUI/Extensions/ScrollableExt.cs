using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using Avalonia.Xaml.Interactivity;
using ShadUI.Utilities;
using System;

// ReSharper disable once CheckNamespace
namespace ShadUI;

/// <summary>
///     Usable extension methods for making an element scrollable.
/// </summary>
internal static class ScrollableExt
{
    static ScrollableExt()
    {
        IsSmoothScrollingEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsSmoothScrollingEnabledChanged);
    }


    /// <summary>
    ///     Makes the visual scrollable.
    /// </summary>
    /// <param name="compositionVisual"></param>
    public static void MakeScrollable(this CompositionVisual? compositionVisual)
    {
        if (compositionVisual == null) return;

        var compositor = compositionVisual.Compositor;

        var animationGroup = compositor.CreateAnimationGroup();
        var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
        offsetAnimation.Target = "Offset";

        offsetAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue");
        offsetAnimation.Duration = TimeSpan.FromMilliseconds(250);

        var implicitAnimationCollection = compositor.CreateImplicitAnimationCollection();
        animationGroup.Add(offsetAnimation);
        implicitAnimationCollection["Offset"] = animationGroup;
        compositionVisual.ImplicitAnimations = implicitAnimationCollection;
    }


    /// <summary>
    /// Identifies the attached property that enables or disables smooth scrolling behavior for a ScrollViewer.
    /// </summary>
    public static readonly AttachedProperty<bool> IsSmoothScrollingEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsSmoothScrollingEnabled", typeof(ScrollableExt), false);

    /// <summary>
    /// Sets the value indicating whether smooth scrolling is enabled for the specified ScrollViewer.
    /// </summary>
    /// <param name="element">The ScrollViewer for which to set the smooth scrolling behavior. Cannot be null.</param>
    /// <param name="value">A value indicating whether smooth scrolling is enabled. to enable smooth.</param>
    public static void SetIsSmoothScrollingEnabled(ScrollViewer element, bool value) =>
        element.SetValue(IsSmoothScrollingEnabledProperty, value);

    /// <summary>
    /// Gets a value indicating whether smooth scrolling is enabled for the specified ScrollViewer.
    /// </summary>
    /// <param name="element">The ScrollViewer from which to retrieve the smooth scrolling setting. Cannot be null.</param>
    /// <returns>true if smooth scrolling is enabled for the specified ScrollViewer otherwise, false.</returns>
    public static bool GetIsSmoothScrollingEnabled(ScrollViewer element) =>
        element.GetValue(IsSmoothScrollingEnabledProperty);

    static void OnIsSmoothScrollingEnabledChanged(
        ScrollViewer scrollViewer,
        AvaloniaPropertyChangedEventArgs e)
    {
        // for some reason, when allowing to dynamically swap flyouts (e.g. ComboBox) crashes. Idk why? so only enabling at start is supported...
        if (e.NewValue is not true)
            return;

        BehaviorCollection behaviors = Interaction.GetBehaviors(scrollViewer);
        foreach (AvaloniaObject b in behaviors)
        {
            if (b is SmoothScrollBehavior)
                return;
        }

        behaviors.Add(new SmoothScrollBehavior());
    }
}