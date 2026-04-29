using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Serilog;

namespace NineKgTools.Components.Common;

public partial class PhotoWall : ComponentBase, IAsyncDisposable
{
    [Parameter] public string Title { get; set; } = "图片墙";
    [Parameter] public List<PhotoWallImageInfo>? Images { get; set; }

    /// <summary>骨架屏初始渲染的占位数量，也是 "10/20/40" 数量选择器的当前值。</summary>
    [Parameter] public int ImageCount { get; set; } = 20;

    /// <summary>固定列数；0 = 按窗口宽度自动选择（移动端 1 列到大屏 4 列）。</summary>
    [Parameter] public int Columns { get; set; } = 0;

    [Parameter] public bool ShowCountSelector { get; set; } = true;
    [Parameter] public bool ShowRefreshButton { get; set; } = true;
    [Parameter] public bool IsLoading { get; set; } = false;
    [Parameter] public string EmptyMessage { get; set; } = "暂无图片可展示";

    /// <summary>启用横向扫入动画，分批把图片推入视图（<see cref="BatchSize"/> 张一批）。</summary>
    [Parameter] public bool EnableBatchLoading { get; set; } = true;

    [Parameter] public int BatchSize { get; set; } = 3;

    /// <summary>相邻两批之间的延迟，单位毫秒。</summary>
    [Parameter] public int BatchDelay { get; set; } = 150;

    [Parameter] public EventCallback<int> OnImageCountChanged { get; set; }
    [Parameter] public EventCallback OnRefresh { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    private List<PhotoWallImageInfo> _visibleImages = new();
    private List<PhotoWallImagePosition> _imagePositions = new();
    private bool _isAnimating;
    private double _windowWidth = 1200;
    private DotNetObjectReference<PhotoWall>? _dotNetHelper;
    private IJSObjectReference? _resizeHandler;
    private List<PhotoWallImageInfo>? _lastImagesRef;

    // 骨架屏高度不能每帧重算，否则 StateHasChanged 时视觉会抖动
    private static readonly int[] SkeletonHeightOptions = { 200, 250, 300, 180, 220, 280, 320 };
    private int[] _skeletonHeights = Array.Empty<int>();
    private int _skeletonHeightsForCount = -1;

    private async Task ChangeImageCount(int count)
    {
        ImageCount = count;
        await OnImageCountChanged.InvokeAsync(count);
    }

    private async Task RefreshImages()
    {
        await OnRefresh.InvokeAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();

        EnsureSkeletonHeights();

        // 只有在 Images 引用真正变化时才处理，避免父组件任何重渲染都触发动画重入
        if (ReferenceEquals(Images, _lastImagesRef))
        {
            return;
        }
        _lastImagesRef = Images;

        if (Images != null && Images.Any() && EnableBatchLoading && !_isAnimating)
        {
            await StartBatchLoading();
        }
        else if (Images != null)
        {
            _visibleImages = Images.ToList();
            CalculateImagePositions();
        }
        else
        {
            _visibleImages.Clear();
            _imagePositions.Clear();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            try
            {
                _windowWidth = await JSRuntime.InvokeAsync<double>("photoWallInterop.getWindowWidth");
                _dotNetHelper = DotNetObjectReference.Create(this);
                _resizeHandler = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "photoWallInterop.onResize", _dotNetHelper);

                CalculateImagePositions();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "PhotoWall JS interop 初始化失败");
            }
        }
        else if (_imagePositions.Count != _visibleImages.Count)
        {
            CalculateImagePositions();
            StateHasChanged();
        }
    }

    /// <summary>
    /// 按左到右、上到下的网格顺序分批把图片喂给视图，营造横向扫入的加载动画
    /// </summary>
    private async Task StartBatchLoading()
    {
        if (Images == null || !Images.Any()) return;

        _isAnimating = true;
        _visibleImages.Clear();

        var batches = BuildLoadingBatches(Images);
        for (int i = 0; i < batches.Count; i++)
        {
            _visibleImages.AddRange(batches[i]);
            CalculateImagePositions();
            StateHasChanged();

            if (i < batches.Count - 1)
            {
                await Task.Delay(BatchDelay);
            }
        }

        _isAnimating = false;
    }

    /// <summary>
    /// 把图片按行列重排后切成 <see cref="BatchSize"/> 大小的批次。
    /// Columns = 0 时按 4 列估算，与 <see cref="GetEffectiveColumnCount"/> 的大屏默认值保持一致。
    /// </summary>
    private List<List<PhotoWallImageInfo>> BuildLoadingBatches(List<PhotoWallImageInfo> source)
    {
        int effectiveColumns = Columns > 0 ? Columns : 4;
        var ordered = new List<PhotoWallImageInfo>(source.Count);
        for (int row = 0; row * effectiveColumns < source.Count; row++)
        {
            for (int col = 0; col < effectiveColumns && row * effectiveColumns + col < source.Count; col++)
            {
                ordered.Add(source[row * effectiveColumns + col]);
            }
        }

        var batches = new List<List<PhotoWallImageInfo>>();
        for (int i = 0; i < ordered.Count; i += BatchSize)
        {
            batches.Add(ordered.GetRange(i, Math.Min(BatchSize, ordered.Count - i)));
        }
        return batches;
    }


    /// <summary>
    /// 构造骨架屏高度数组，仅在 ImageCount 变化时重建，避免每次渲染都 new Random() 造成抖动
    /// </summary>
    private void EnsureSkeletonHeights()
    {
        if (_skeletonHeightsForCount == ImageCount && _skeletonHeights.Length == ImageCount)
        {
            return;
        }

        _skeletonHeights = new int[ImageCount];
        for (int i = 0; i < ImageCount; i++)
        {
            _skeletonHeights[i] = SkeletonHeightOptions[Random.Shared.Next(SkeletonHeightOptions.Length)];
        }
        _skeletonHeightsForCount = ImageCount;
    }

    private void CalculateImagePositions()
    {
        if (!_visibleImages.Any())
        {
            _imagePositions.Clear();
            return;
        }

        int columnCount = GetEffectiveColumnCount();
        if (columnCount <= 0) columnCount = 4;

        // 宽度走百分比让 CSS 适配容器，高度走像素便于瀑布流对齐；
        // 因此这里需要一个"基准像素宽度"把百分比换算成高度
        const double containerWidthPercent = 100;
        const double gapPercent = 1;
        double totalGapPercent = gapPercent * (columnCount - 1);
        double columnWidthPercent = (containerWidthPercent - totalGapPercent) / columnCount;

        double assumedContainerWidthPx = GetAssumedContainerWidth(columnCount);
        double columnWidthPx = assumedContainerWidthPx * columnWidthPercent / 100;
        double gapPx = assumedContainerWidthPx * gapPercent / 100;

        double[] columnHeights = new double[columnCount];
        _imagePositions.Clear();

        for (int i = 0; i < _visibleImages.Count; i++)
        {
            var image = _visibleImages[i];

            int targetColumn = 0;
            double minHeight = columnHeights[0];
            for (int j = 1; j < columnCount; j++)
            {
                if (columnHeights[j] < minHeight)
                {
                    minHeight = columnHeights[j];
                    targetColumn = j;
                }
            }

            double aspectRatio = GetImageAspectRatioValue(image);
            double imageHeightPx = columnWidthPx / aspectRatio;

            _imagePositions.Add(new PhotoWallImagePosition
            {
                Image = image,
                LeftPercent = targetColumn * (columnWidthPercent + gapPercent),
                TopPx = columnHeights[targetColumn],
                WidthPercent = columnWidthPercent,
                HeightPx = imageHeightPx,
                Column = targetColumn,
                AnimationDelay = i * 0.05
            });

            columnHeights[targetColumn] += imageHeightPx + gapPx;
        }

        _containerHeight = columnHeights.Max();
    }
    
    /// <summary>
    /// 根据列数获取假设的容器宽度（用于高度计算）
    /// </summary>
    /// <summary>
    /// 根据列数估算容器像素宽度，用于把百分比宽度换算为像素高度。
    /// 优先使用实际窗口宽度（留 5% 给页面边距），否则按列数给经验值兜底。
    /// </summary>
    private double GetAssumedContainerWidth(int columnCount)
    {
        if (_windowWidth > 0)
        {
            return _windowWidth * 0.95;
        }

        return columnCount switch
        {
            1 => 400,
            2 => 600,
            3 => 900,
            _ => 1200
        };
    }

    /// <summary>
    /// 返回响应式列数。断点与 photo-wall.css 中的媒体查询保持一致：
    /// ≤480 单列 / ≤768 双列 / ≤1200 三列 / 其他四列。
    /// </summary>
    private int GetEffectiveColumnCount()
    {
        if (Columns > 0)
        {
            return Columns;
        }

        if (_windowWidth <= 480) return 1;
        if (_windowWidth <= 768) return 2;
        if (_windowWidth <= 1200) return 3;
        return 4;
    }

    /// <summary>
    /// 拿到图片真实宽高比；拿不到时按 ImageId 哈希稳定分配一个预设比例，
    /// 保证同一张图在多次渲染间不跳位
    /// </summary>
    private double GetImageAspectRatioValue(PhotoWallImageInfo image)
    {
        if (image.Width > 0 && image.Height > 0)
        {
            return (double)image.Width / image.Height;
        }

        var ratios = new[] { 1.0, 4.0 / 3.0, 3.0 / 4.0, 16.0 / 9.0, 3.0 / 2.0, 2.0 / 3.0 };
        var index = Math.Abs(image.ImageId.GetHashCode()) % ratios.Length;
        return ratios[index];
    }

    private double _containerHeight;

    [JSInvokable]
    public async Task OnWindowResize(double width)
    {
        _windowWidth = width;
        CalculateImagePositions();
        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_resizeHandler != null)
            {
                await _resizeHandler.InvokeVoidAsync("dispose");
                await _resizeHandler.DisposeAsync();
            }

            _dotNetHelper?.Dispose();
        }
        catch (JSDisconnectedException)
        {
            // Blazor Server circuit 已断开（用户关闭标签 / 刷新页面等）：属于正常生命周期事件，不需要告警
        }
        catch (TaskCanceledException)
        {
            // 同上：JS 调用因为 circuit 断开被取消
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "PhotoWall 释放资源时发生异常");
        }
    }
}

/// <summary>
/// 图片墙图片信息类
/// </summary>
public class PhotoWallImageInfo
{
    public int ImageId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImageName { get; set; } = string.Empty;
    public int? MediaId { get; set; }
    public string MediaTitle { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public Color CategoryColor { get; set; } = Color.Default;
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// 图片位置信息类
/// </summary>
public class PhotoWallImagePosition
{
    public PhotoWallImageInfo Image { get; set; } = new();
    public double LeftPercent { get; set; }  // 左边距（百分比）
    public double TopPx { get; set; }   // 上边距（像素）
    public double WidthPercent { get; set; }  // 宽度（百分比）
    public double HeightPx { get; set; } // 高度（像素）
    public int Column { get; set; }    // 所在列
    public double AnimationDelay { get; set; } // 动画延迟
}
