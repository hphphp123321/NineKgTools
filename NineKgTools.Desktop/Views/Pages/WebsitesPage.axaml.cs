using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NineKgTools.Desktop.ViewModels.Pages;
using Serilog;

namespace NineKgTools.Desktop.Views.Pages;

public partial class WebsitesPage : UserControl
{
    /// <summary>
    /// chip 拖拽采用 Pointer 模拟（弃用 OS DragDrop / Pointer.Capture）。Pressed 后在 UserControl 根
    /// 用 AddHandler 接 Moved/Released（绕过 capture 不稳）。EndDrag 时用 FLIP 动画让被影响的所有
    /// chip 同步平滑滑动：
    /// 1. F (First): MoveChip 前记录所有 chip-host 的 layout 位置
    /// 2. L (Last): 调 MoveChip 让 layout 重排
    /// 3. I (Invert): layout pass 后，给每个移位的 chip Border 设 RT=oldPos-newPos（无 transition），
    ///    视觉上 chip 仍停在旧位置
    /// 4. P (Play): 下一帧开启 transitions + RT=Identity，所有 chip 同时平滑滑到新位置
    /// 拖拽源 chip 的 before 视觉位置 = 光标拖到的位置（含 RT），并入同一 FLIP 流程。
    /// </summary>
    private ChipDragState? _drag;

    private const double DragThreshold = 6.0;
    private const double IndicatorHeight = 22.0;
    private const double IndicatorWidth = 3.0;
    private static readonly TimeSpan ReleaseDuration = TimeSpan.FromMilliseconds(220);

    /// <summary>拖拽源 chip 的复位 transitions——RT + Opacity + BoxShadow 一起过渡。</summary>
    private static readonly Transitions DraggedReleaseTransitions = new()
    {
        new DoubleTransition
        {
            Property = Visual.OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(180),
        },
        new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = ReleaseDuration,
            Easing = new CubicEaseOut(),
        },
        new BoxShadowsTransition
        {
            Property = Border.BoxShadowProperty,
            Duration = TimeSpan.FromMilliseconds(180),
        },
    };

    /// <summary>让位 chip 的滑动 transitions——只动 RT。</summary>
    private static readonly Transitions SiblingSlideTransitions = new()
    {
        new TransformOperationsTransition
        {
            Property = Visual.RenderTransformProperty,
            Duration = ReleaseDuration,
            Easing = new CubicEaseOut(),
        },
    };

    public WebsitesPage()
    {
        InitializeComponent();
    }

    private void OnChipPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border chipBorder) return;
        if (chipBorder.DataContext is not PriorityChipViewModel chip) return;
        if (!e.GetCurrentPoint(chipBorder).Properties.IsLeftButtonPressed) return;

        if (_drag is not null) EndDrag(commit: false);

        var container = chipBorder.GetVisualAncestors().OfType<Grid>()
            .FirstOrDefault(g => g.Tag is string s && s == "ChipsContainer");
        if (container is null) return;
        var itemsControl = container.GetVisualDescendants().OfType<ItemsControl>()
            .FirstOrDefault(c => c.Classes.Contains("chips-list"));
        if (itemsControl is null) return;
        var indicator = container.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("drop-indicator"));
        if (indicator is null) return;

        _drag = new ChipDragState
        {
            ChipBorder = chipBorder,
            ChipVm = chip,
            Row = chip.Row,
            ChipsList = itemsControl,
            Container = container,
            Indicator = indicator,
            PressOrigin = e.GetPosition(container),
        };

        AddHandler(PointerMovedEvent, OnRootPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnRootPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_drag is null) return;

        var pos = e.GetPosition(_drag.Container);
        var dx = pos.X - _drag.PressOrigin.X;
        var dy = pos.Y - _drag.PressOrigin.Y;

        if (!_drag.IsDragging)
        {
            if (Math.Sqrt(dx * dx + dy * dy) < DragThreshold) return;
            StartDrag();
        }

        _drag.CurrentDelta = new Point(dx, dy);
        var inv = CultureInfo.InvariantCulture;
        _drag.ChipBorder.RenderTransform = TransformOperations.Parse($"translate({dx.ToString(inv)}px, {dy.ToString(inv)}px)");

        UpdateDropIndicator(pos);
    }

    private void OnRootPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        EndDrag(commit: true);
    }

    private void StartDrag()
    {
        if (_drag is null) return;
        _drag.IsDragging = true;
        var border = _drag.ChipBorder;
        border.Transitions = null;
        border.Opacity = 0.6;
        border.ZIndex = 999;
        try
        {
            border.BoxShadow = BoxShadows.Parse("0 8 24 0 #44000000");
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "chip 拖拽设阴影失败（不影响逻辑）");
        }
    }

    private void UpdateDropIndicator(Point pos)
    {
        if (_drag is null) return;

        var hosts = new List<(int Index, Rect Bounds)>(_drag.ChipsList.ItemCount);
        for (int i = 0; i < _drag.ChipsList.ItemCount; i++)
        {
            var ic = _drag.ChipsList.ContainerFromIndex(i);
            if (ic is null) continue;
            var topLeft = ic.TranslatePoint(new Point(0, 0), _drag.Container);
            if (topLeft is null) continue;
            hosts.Add((i, new Rect(topLeft.Value.X, topLeft.Value.Y, ic.Bounds.Width, ic.Bounds.Height)));
        }
        if (hosts.Count == 0) { HideIndicator(); return; }

        var sameRow = hosts.Where(h => pos.Y >= h.Bounds.Y && pos.Y <= h.Bounds.Bottom)
                           .OrderBy(h => h.Bounds.X)
                           .ToList();
        if (sameRow.Count == 0) { HideIndicator(); return; }

        int hoverIndex = sameRow[^1].Index + 1;
        foreach (var h in sameRow)
        {
            var mid = h.Bounds.X + h.Bounds.Width / 2;
            if (pos.X < mid)
            {
                hoverIndex = h.Index;
                break;
            }
        }

        double indicatorX;
        if (hoverIndex <= sameRow[0].Index)
        {
            indicatorX = sameRow[0].Bounds.X - IndicatorWidth - 1;
        }
        else if (hoverIndex > sameRow[^1].Index)
        {
            indicatorX = sameRow[^1].Bounds.Right + 1;
        }
        else
        {
            var prev = sameRow.LastOrDefault(h => h.Index == hoverIndex - 1);
            var next = sameRow.FirstOrDefault(h => h.Index == hoverIndex);
            indicatorX = (prev.Bounds.Right + next.Bounds.X) / 2 - IndicatorWidth / 2;
        }

        var rowY = sameRow[0].Bounds.Y;
        var rowHeight = sameRow[0].Bounds.Height;
        var indicatorY = rowY + (rowHeight - IndicatorHeight) / 2;

        _drag.HoverIndex = hoverIndex;
        var inv = CultureInfo.InvariantCulture;
        _drag.Indicator.RenderTransform = TransformOperations.Parse(
            $"translate({indicatorX.ToString(inv)}px, {indicatorY.ToString(inv)}px)");
        if (!_drag.Indicator.Classes.Contains("active"))
            _drag.Indicator.Classes.Add("active");
    }

    private void HideIndicator()
    {
        if (_drag is null) return;
        _drag.HoverIndex = -1;
        _drag.Indicator.Classes.Remove("active");
    }

    private void EndDrag(bool commit)
    {
        if (_drag is null) return;
        var state = _drag;
        _drag = null;

        RemoveHandler(PointerMovedEvent, OnRootPointerMoved);
        RemoveHandler(PointerReleasedEvent, OnRootPointerReleased);

        state.Indicator.Classes.Remove("active");

        if (!state.IsDragging) return;

        var draggedBorder = state.ChipBorder;
        var draggedDelta = state.CurrentDelta;

        int from = state.Row.Chips.IndexOf(state.ChipVm);
        int? finalTo = null;
        if (commit && state.HoverIndex >= 0 && from >= 0)
        {
            var to = state.HoverIndex > from ? state.HoverIndex - 1 : state.HoverIndex;
            if (to >= 0 && to < state.Row.Chips.Count && to != from) finalTo = to;
        }

        // F (First): MoveChip 前 snapshot 所有 chip-host 在 container 中的 layout 位置
        var beforePositions = SnapshotChipPositions(state.ChipsList, state.Container);

        // L (Last): 立即 MoveChip 让 layout 重排
        if (finalTo.HasValue)
        {
            try
            {
                state.Row.MoveChip(from, finalTo.Value);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "chip 拖拽 MoveChip 失败");
                finalTo = null;
            }
        }

        // I + P: layout pass 后 invert 所有移位 chip 的 RT，下一帧再设 Identity 触发动画
        Dispatcher.UIThread.Post(() =>
        {
            var afterPositions = SnapshotChipPositions(state.ChipsList, state.Container);

            // 收集需要做 FLIP 的 chip Border + 它们的 delta
            var flipTargets = new List<(Border Border, double Dx, double Dy, bool IsDragged)>();

            foreach (var (container, oldPos) in beforePositions)
            {
                if (!afterPositions.TryGetValue(container, out var newPos)) continue;
                var chipBorder = container.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(b => b.Classes.Contains("priority-chip"));
                if (chipBorder is null) continue;

                bool isDragged = ReferenceEquals(chipBorder, draggedBorder);
                double dx, dy;
                if (isDragged)
                {
                    // 拖拽源：before 视觉位置 = oldLayoutPos + currentRT，after layout = newPos
                    // delta = (oldPos + currentRT) - newPos
                    dx = (oldPos.X + draggedDelta.X) - newPos.X;
                    dy = (oldPos.Y + draggedDelta.Y) - newPos.Y;
                }
                else
                {
                    dx = oldPos.X - newPos.X;
                    dy = oldPos.Y - newPos.Y;
                    if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5) continue; // 未移位 chip 跳过
                }
                flipTargets.Add((chipBorder, dx, dy, isDragged));
            }

            // Invert：关 transitions 设 RT = delta（瞬间，无动画）
            var inv = CultureInfo.InvariantCulture;
            foreach (var (b, dx, dy, _) in flipTargets)
            {
                b.Transitions = null;
                b.RenderTransform = TransformOperations.Parse($"translate({dx.ToString(inv)}px, {dy.ToString(inv)}px)");
            }

            // Play：再下一帧开 transitions 设 RT = Identity → 所有 chip 同步滑到 layout 新位置
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var (b, _, _, isDragged) in flipTargets)
                {
                    if (isDragged)
                    {
                        b.Transitions = DraggedReleaseTransitions;
                        b.RenderTransform = TransformOperations.Identity;
                        b.Opacity = 1.0;
                        b.BoxShadow = default;
                    }
                    else
                    {
                        b.Transitions = SiblingSlideTransitions;
                        b.RenderTransform = TransformOperations.Identity;
                    }
                }

                // 动画结束后清拖拽源 ZIndex + 清 sibling 的 transitions（避免 hover 等后续状态变化时插值）
                DispatcherTimer.RunOnce(() =>
                {
                    foreach (var (b, _, _, isDragged) in flipTargets)
                    {
                        if (isDragged)
                        {
                            b.ZIndex = 0;
                        }
                        else
                        {
                            b.Transitions = null;
                        }
                    }
                }, ReleaseDuration);
            }, DispatcherPriority.Render);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>遍历 chip ItemsControl 的所有 container，返回每个在 owner 坐标系下的 TopLeft 位置。</summary>
    private static Dictionary<Control, Point> SnapshotChipPositions(ItemsControl chipsList, Visual owner)
    {
        var dict = new Dictionary<Control, Point>(chipsList.ItemCount);
        for (int i = 0; i < chipsList.ItemCount; i++)
        {
            var c = chipsList.ContainerFromIndex(i);
            if (c is null) continue;
            var p = c.TranslatePoint(new Point(0, 0), owner);
            if (!p.HasValue) continue;
            dict[c] = p.Value;
        }
        return dict;
    }

    private sealed class ChipDragState
    {
        public required Border ChipBorder { get; init; }
        public required PriorityChipViewModel ChipVm { get; init; }
        public required PriorityRowViewModel Row { get; init; }
        public required ItemsControl ChipsList { get; init; }
        public required Grid Container { get; init; }
        public required Border Indicator { get; init; }
        public required Point PressOrigin { get; init; }
        public bool IsDragging { get; set; }
        public int HoverIndex { get; set; } = -1;
        public Point CurrentDelta { get; set; }
    }
}
