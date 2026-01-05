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
        IsSmoothScrollEnabledProperty.Changed.AddClassHandler<ScrollViewer>(OnIsSmoothScrollEnabledChanged);
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
    /// Identifies the attached property that enables or disables smooth scroll behavior for a ScrollViewer.
    /// </summary>
    public static readonly AttachedProperty<bool> IsSmoothScrollEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>("IsSmoothScrollEnabled", typeof(ScrollableExt), false);

    /// <summary>
    /// Sets the value indicating whether smooth scroll is enabled for the specified ScrollViewer.
    /// </summary>
    /// <param name="element">The ScrollViewer for which to set the smooth scroll behavior. Cannot be null.</param>
    /// <param name="value">A value indicating whether smooth scroll is enabled. to enable smooth.</param>
    public static void SetIsSmoothScrollEnabled(ScrollViewer element, bool value) =>
        element.SetValue(IsSmoothScrollEnabledProperty, value);

    /// <summary>
    /// Gets a value indicating whether smooth scroll is enabled for the specified ScrollViewer.
    /// </summary>
    /// <param name="element">The ScrollViewer from which to retrieve the smooth scroll setting. Cannot be null.</param>
    /// <returns>true if smooth scroll is enabled for the specified ScrollViewer otherwise, false.</returns>
    public static bool GetIsSmoothScrollEnabled(ScrollViewer element) =>
        element.GetValue(IsSmoothScrollEnabledProperty);

    static void OnIsSmoothScrollEnabledChanged(
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