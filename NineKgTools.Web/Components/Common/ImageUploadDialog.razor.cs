using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Images;
using Serilog;

namespace NineKgTools.Components.Common;

public partial class ImageUploadDialog : ComponentBase
{
    const int MaxFileSize = 10 * 1024 * 1024; // 10MB
    const int PreviewMaxDimension = 600;      // 预览缩略图最大边
    const string PreviewFormat = "image/jpeg";

    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = null!;
    [Parameter] public string ParentDirName { get; set; } = "UserUploads";

    [Inject] private ImageService ImageService { get; set; } = null!;

    private string? _imagePreviewUrl;
    private string? _errorMessage;
    private IBrowserFile? _selectedFile;
    private bool _isLoading;

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        try
        {
            _errorMessage = null;
            var file = e.File;

            if (file == null)
                return;

            // 验证文件大小：在错误信息里带上实际大小，用户无需自己换算
            if (file.Size > MaxFileSize)
            {
                var actualMb = file.Size / (1024.0 * 1024.0);
                _errorMessage = $"图片太大了（{actualMb:F1} MB），请选择 10 MB 以内的图片。";
                return;
            }

            // 验证文件类型：带上实际格式，方便用户理解
            var validTypes = new[] { "image/jpeg", "image/png", "image/jpg" };
            if (!validTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                var displayType = string.IsNullOrEmpty(file.ContentType) ? "未知" : file.ContentType;
                _errorMessage = $"暂不支持 {displayType} 格式，请换用 JPG 或 PNG 图片。";
                return;
            }

            _selectedFile = file;

            // 只读取浏览器降采样后的缩略图用于预览：
            // - 10MB 原图 → ~50KB JPEG 缩略图
            // - base64 串从 ~13MB 降到 ~70KB，显著减轻 SignalR/DOM 压力
            // - 原始文件在 Submit 时才读取，避免重复读取
            var thumbnail = await file.RequestImageFileAsync(PreviewFormat, PreviewMaxDimension, PreviewMaxDimension);
            var previewBuffer = new byte[thumbnail.Size];
            await using (var previewStream = thumbnail.OpenReadStream(MaxFileSize))
            {
                await previewStream.ReadExactlyAsync(previewBuffer);
            }
            _imagePreviewUrl = $"data:{PreviewFormat};base64,{Convert.ToBase64String(previewBuffer)}";
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "生成图片预览失败 File={FileName} Size={Size}", e.File?.Name, e.File?.Size);
            _errorMessage = "无法读取这张图片，换一张试试？";
        }
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    private async Task Submit()
    {
        if (_selectedFile == null || string.IsNullOrEmpty(_imagePreviewUrl))
            return;

        try
        {
            _isLoading = true;

            // 读取原始文件内容到内存（整个流程中只读这一次）
            var buffer = new byte[_selectedFile.Size];
            await using (var stream = _selectedFile.OpenReadStream(MaxFileSize))
            {
                await stream.ReadExactlyAsync(buffer);
            }

            var image = new Image(buffer, _selectedFile.Name);
            var dbImage = await ImageService.AddOrFindImageAsync(image, ParentDirName);

            MudDialog.Close(DialogResult.Ok(dbImage));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "上传图片失败 File={FileName} ParentDir={ParentDir}", _selectedFile?.Name, ParentDirName);
            _errorMessage = "上传失败，请检查网络后重试。";
            _isLoading = false;
        }
    }
}