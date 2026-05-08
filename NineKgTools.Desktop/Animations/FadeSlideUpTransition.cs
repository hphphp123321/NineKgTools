using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media.Transformation;
using Avalonia.Styling;

namespace NineKgTools.Desktop.Animations;

/// <summary>
/// 主窗 ContentControl 切 Page 时的过渡动画：旧页面 fade out（原地不动），
/// 新页面 fade in 同时从下方 <see cref="SlideOffset"/>px 上浮到 0。
///
/// 时长走 desktop-design.md 既定的"页面切换 160ms" Win11 风格规范。
/// 与 FluentAvalonia 内部动画系统解耦——FAUISettings.SetAnimationsEnabledAtAppLevel(false)
/// 关掉的是 NavigationView indicator + ContentDialog 等，本 transition 不受影响。
///
/// 没复用 Avalonia 内置 PageSlide：它默认按视区 100% 横向滑入（适合手机 stack
/// navigation），无法配置 subtle 8px 偏移。这里直接走 Animation API 写细节。
/// </summary>
public sealed class FadeSlideUpTransition : IPageTransition
{
    /// <summary>过渡时长（默认 160ms 与项目 Win11 风格规范一致）。</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(160);

    /// <summary>新页面进入时的下方起始偏移（px）。8 是 subtle 不抢眼的"抬起"距离。</summary>
    public double SlideOffset { get; set; } = 8.0;

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken ct)
    {
        var easing = new CubicEaseOut();
        var tasks = new List<Task>(2);

        if (from is not null)
        {
            // 老页面：原地 fade out，不动位置——避免与新页面 slide-up 形成双向位移混乱
            var fadeOut = new Animation
            {
                Duration = Duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(Visual.OpacityProperty, 1d) },
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(Visual.OpacityProperty, 0d) },
                    },
                },
            };
            tasks.Add(fadeOut.RunAsync(from, ct));
        }

        if (to is not null)
        {
            // 新页面：从下方 SlideOffset px 上浮 + fade in
            var slideUpFromOffset = TransformOperations.Parse($"translateY({SlideOffset}px)");
            var slideAtRest = TransformOperations.Parse("translateY(0px)");

            var fadeIn = new Animation
            {
                Duration = Duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0d),
                            new Setter(Visual.RenderTransformProperty, slideUpFromOffset),
                        },
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1d),
                            new Setter(Visual.RenderTransformProperty, slideAtRest),
                        },
                    },
                },
            };
            tasks.Add(fadeIn.RunAsync(to, ct));
        }

        await Task.WhenAll(tasks);
    }
}
