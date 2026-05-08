using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace NineKgTools.Desktop.Animations;

/// <summary>
/// 主窗 ContentControl 切 Page 时的过渡：旧页面 fade out（原地不动），
/// 新页面 fade in 同时从下方 <see cref="SlideOffset"/>px 上浮到 0。
/// 时长走 desktop-design.md 既定的"页面切换 160ms" Win11 风格规范。
///
/// **实现照抄 Avalonia 12 内置 PageSlide 的模式**（src/Avalonia.Base/Animation/PageSlide.cs）：
/// - KeyFrame setter 走 <see cref="TranslateTransform.YProperty"/>（不是
///   <see cref="Visual.RenderTransformProperty"/>）——animation 引擎自动在 visual 的
///   RenderTransform 槽位 attach 一个 TranslateTransform 实例并 animate Y。
/// - 收尾把 <c>RenderTransform = null</c> 清掉，避免 FillMode.Forward 把 transform 永久
///   stick 在动画值上。
///
/// **不要直接写** <c>to.Opacity = 0</c> 或 <c>to.RenderTransform = ...</c> 做 priming —
/// 那会用 LocalValue 优先级写入属性系统，FillMode.Forward 的动画值清掉后 LocalValue 仍在，
/// 表现就是"页面 invisible 但仍可点击"（hit-test 不查 Opacity）。
///
/// 与 FluentAvalonia 内部动画系统解耦——FAUISettings.SetAnimationsEnabledAtAppLevel(false)
/// 关掉的是 NavigationView indicator + ContentDialog 等，本 transition 走 Avalonia 原生
/// Animation API 不受影响。
/// </summary>
public sealed class FadeSlideUpTransition : IPageTransition
{
    /// <summary>过渡时长（默认 160ms 与项目 Win11 风格规范一致）。</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(160);

    /// <summary>新页面进入时的下方起始偏移（px）。8 是 subtle 不抢眼的"抬起"距离。</summary>
    public double SlideOffset { get; set; } = 8.0;

    /// <summary>动画 FillMode，默认 Forward 让结束帧值持续到收尾清理。</summary>
    public FillMode FillMode { get; set; } = FillMode.Forward;

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
                FillMode = FillMode,
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
            // 唯一允许的 priming：保证 IsVisible = true（IPageTransition 调用方可能 set false 了）
            to.IsVisible = true;

            var fadeIn = new Animation
            {
                Duration = Duration,
                Easing = easing,
                FillMode = FillMode,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0d),
                            new Setter(TranslateTransform.YProperty, SlideOffset),
                        },
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1d),
                            new Setter(TranslateTransform.YProperty, 0d),
                        },
                    },
                },
            };
            tasks.Add(fadeIn.RunAsync(to, ct));
        }

        await Task.WhenAll(tasks);

        if (ct.IsCancellationRequested) return;

        // 收尾：照抄 PageSlide.Start 末尾的清理路径
        if (from is not null)
        {
            from.IsVisible = false;
            if (FillMode != FillMode.None)
                from.RenderTransform = null;
        }

        if (to is not null && FillMode != FillMode.None)
        {
            to.RenderTransform = null;
        }
    }
}
