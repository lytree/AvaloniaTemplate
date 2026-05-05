

using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Ursa.Helpers;

namespace Avalonia.UI.Theme.Animations;

public class DefaultSizeAnimations : ResourceDictionary
{
    public const string WidthAnimationGeneratorKey = "WidthAnimationGenerator";
    public const string HeightAnimationGeneratorKey = "HeightAnimationGenerator";
    public const string WidthHeightAnimationGeneratorKey = "WidthHeightAnimationGenerator";

    public DefaultSizeAnimations()
    {
        Add(WidthAnimationGeneratorKey, WidthAnimationGenerator);
        Add(HeightAnimationGeneratorKey, HeightAnimationGenerator);
        Add(WidthHeightAnimationGeneratorKey, WidthHeightAnimationGenerator);
    }

    private readonly SizeAnimationHelperAnimationGeneratorDelegate WidthAnimationGenerator =
        (_, oldDesiredSize, newDesiredSize) => new Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new Animation.Easings.CubicEaseInOut(),
            FillMode = Animation.FillMode.None,
            Children =
            {
                new Animation.KeyFrame
                {
                    Cue = new Animation.Cue(0.0),
                    Setters =
                    {
                        new Setter(Layoutable.WidthProperty, oldDesiredSize.Width)
                    }
                },
                new Animation.KeyFrame
                {
                    Cue = new Animation.Cue(1.0),
                    Setters =
                    {
                        new Setter(Layoutable.WidthProperty, newDesiredSize.Width)
                    }
                }
            }
        };

    private readonly SizeAnimationHelperAnimationGeneratorDelegate HeightAnimationGenerator =
        (_, oldDesiredSize, newDesiredSize) => new Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new Animation.Easings.CubicEaseInOut(),
            FillMode = Animation.FillMode.None,
            Children =
            {
                new Animation.KeyFrame
                {
                    Cue = new Animation.Cue(0.0),
                    Setters =
                    {
                        new Setter(Layoutable.HeightProperty, oldDesiredSize.Height)
                    }
                },
                new Animation.KeyFrame
                {
                    Cue = new Animation.Cue(1.0),
                    Setters =
                    {
                        new Setter(Layoutable.HeightProperty, newDesiredSize.Height)
                    }
                }
            }
        };

    private readonly SizeAnimationHelperAnimationGeneratorDelegate WidthHeightAnimationGenerator =
        (_, oldDesiredSize, newDesiredSize) => new Animation.Animation
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = new Animation.Easings.CubicEaseInOut(),
            FillMode = Animation.FillMode.None,
            Children =
            {
                new Animation.KeyFrame
                {
                    Cue = new Animation.Cue(0.0),
                    Setters =
                    {
                        new Setter(Layoutable.WidthProperty, oldDesiredSize.Width),
                        new Setter(Layoutable.HeightProperty, oldDesiredSize.Height)
                    }
                },
                new Animation.KeyFrame
                {
                    Cue = new Animation.Cue(1.0),
                    Setters =
                    {
                        new Setter(Layoutable.WidthProperty, newDesiredSize.Width),
                        new Setter(Layoutable.HeightProperty, newDesiredSize.Height)
                    }
                }
            }
        };
}
