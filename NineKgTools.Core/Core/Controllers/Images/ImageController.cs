using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Images;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NineKgTools.Core.Controllers.Images;

[Route("api/[controller]")]
[ApiController]
public class ImageController : ControllerBase
{
    private readonly ImageService _imageService;

    // 构造函数注入 ImageService
    public ImageController(ImageService imageService)
    {
        _imageService = imageService;
    }
    
    [HttpGet("{imageName}")]
    public async Task<IActionResult> GetImage(string imageName)
    {
        var imageStream = await _imageService.GetImageByNameAsync(imageName);

        if (imageStream == null)
        {
            return NotFound(); // 如果没有找到图片，返回 404
        }

        // 设置 MIME 类型
        var mimeType = GetMimeType(imageName);
        return File(imageStream, mimeType);
    }

    // 通过文件扩展名推断 MIME 类型
    private static string GetMimeType(string filename)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
    }

    // 添加一个上传图片的API端点
    [HttpPost]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("没有上传文件或文件为空");
            
        try
        {
            // 读取上传的文件内容
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();
            
            // 创建Image对象
            var image = new Image(fileBytes, file.FileName);
            
            // 保存到数据库和文件系统
            var savedImage = await _imageService.AddOrFindImageAsync(image, "UserUploads");
            
            if (savedImage == null)
                return StatusCode(500, "保存图片失败");
                
            return Ok(new { ImageUrl = savedImage.GetImageUrl() });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"上传图片错误: {ex.Message}");
        }
    }
}