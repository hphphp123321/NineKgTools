using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Services.Configs;
using NineKgTools.Core.Services.Http;
using NineKgTools.Utils;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Cryptography;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace NineKgTools.Core.Services.Images;

public class ImageService(MediaDbContext dbContext, Config config, HttpService http)
{
    private static string ImgCacheName = "Images";

    /// <summary>
    /// 根据图片ID获取图片对象并返回相应的文件流
    /// </summary>
    /// <param name="imageId">图片ID</param>
    /// <returns>返回图片的文件流或 URL</returns>
    public async Task<Stream?> GetImageByNameAsync(string imageName)
    {
        // 从数据库查找图片
        var image = await dbContext.Images.FirstOrDefaultAsync(i => i.Name == imageName);

        // 如果找不到图片，返回 null
        if (image == null)
        {
            return null;
        }

        // 如果图片的本地文件路径不为空，表示图片存储在本地
        if (image.File != null && File.Exists(image.File.FullName))
        {
            return new FileStream(image.File.FullName, FileMode.Open, FileAccess.Read);
        }


        if (image.Url == null) return null;

        // 使用 HttpClient 下载远程图片并返回流
        var imageBytes = await http.GetBytes(image.Url.ToString());
        return new MemoryStream(imageBytes);
    }

    /// <summary>
    /// 寻找或者插入一个图片，在插入时会根据是否缓存图片来保存图片
    /// </summary>
    /// <param name="image">要插入的图片</param>
    /// <param name="parentDirName">图片所在父文件夹名字，通常为媒体名、社团名、人名</param>
    /// <returns>返回数据库中存在的图片实体</returns>
    public async Task<Image?> AddOrFindImageAsync(Image? image, string parentDirName)
    {
        if (image == null)
            return null;

        // 确保图片有hash值，无论它是通过哪种构造方法创建的
        await EnsureImageHashAsync(image);

        parentDirName = parentDirName.ReplaceInvalidChars();
        var imageName = GenerateImageFileName(image, parentDirName);

        // 使用单独的方法查找现有图片
        var dbImage = await FindExistingImageAsync(image, imageName);
        if (dbImage != null)
            return dbImage;

        image.Name = imageName;
        await dbContext.Images.AddAsync(image);
        await dbContext.SaveChangesAsync(); // 更新image的ID

        // 缓存图片
        var file = await StoreImage(image, parentDirName);
        image.File = file;
        await dbContext.SaveChangesAsync();

        return image;
    }

    public async Task RemoveImageAsync(Image image)
    {
        if (image == null)
            return;
        
        // 先在数据库中查看是否存在该图片
        var dbImage = await dbContext.Images.FindAsync(image.Id);
        

        if (dbImage != null)
        {
            dbContext.Images.Remove(image);
            await dbContext.SaveChangesAsync();
        }

        // 删除图片文件
        if (image.File != null && File.Exists(image.File.FullName))
        {
            try
            {
                File.Delete(image.File.FullName);
                Log.Information("删除图片文件: {FileFullName}", image.File.FullName);
                
                // 如果图片所在的文件夹为空，则删除文件夹
                var parentDir = image.File.Directory;
                if (parentDir != null && parentDir.GetFiles().Length == 0)
                {
                    Directory.Delete(parentDir.FullName);
                    Log.Information("删除空文件夹: {Directory}", parentDir.FullName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "删除图片文件失败: {FileFullName}", image.File.FullName);
            }
        }
    }
    
    public async Task RemoveImagesAsync(List<Image> images)
    {
        foreach (var image in images)
        {
            await RemoveImageAsync(image);
        }
    }
    
    /// <summary>
    /// 确保图片对象有哈希值和尺寸信息，无论它是通过什么方式创建的
    /// </summary>
    /// <param name="image">要处理的图片对象</param>
    private async Task EnsureImageHashAsync(Image image)
    {
        // 如果已经有哈希值和尺寸信息，不需要再计算
        var needsHash = string.IsNullOrEmpty(image.Hash);
        var needsDimensions = image.Width <= 0 || image.Height <= 0;
        
        if (!needsHash && !needsDimensions)
            return;

        byte[] content = null;

        // 情况1: 图片已经有Content数据
        if (image.Content != null && image.Content.Length > 0)
        {
            content = image.Content;
        }
        // 情况2: 从文件加载内容
        else if (image.File != null && image.File.Exists)
        {
            content = await File.ReadAllBytesAsync(image.File.FullName);
            // 可以选择是否保存内容到Image.Content
            // image.Content = content;
        }
        // 情况3: 从URL下载内容
        else if (image.Url != null)
        {
            try
            {
                content = await http.GetBytes(image.Url.ToString());
                // 可以选择是否保存内容到Image.Content
                // image.Content = content;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "从URL {Url} 下载图片内容以计算哈希值时出错", image.Url);
            }
        }

        // 如果有内容，计算哈希值和尺寸信息
        if (content != null && content.Length > 0)
        {
            // 计算哈希值
            if (needsHash)
            {
                using var sha = SHA256.Create();
                var hashBytes = sha.ComputeHash(content);
                image.Hash = Convert.ToBase64String(hashBytes);
            }
            
            // 获取图片尺寸
            if (needsDimensions)
            {
                try
                {
                    using var imageSharp = ImageSharpImage.Load(content);
                    image.Width = imageSharp.Width;
                    image.Height = imageSharp.Height;
                    
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "无法获取图片尺寸，使用默认值");
                    image.Width = 0;
                    image.Height = 0;
                }
            }
        }
    }

    /// <summary>
    /// 查找数据库中是否已存在相同的图片
    /// </summary>
    /// <param name="image">要查找的图片</param>
    /// <param name="imageName">生成的图片名称</param>
    /// <returns>如果存在则返回数据库中的图片，否则返回null</returns>
    private async Task<Image?> FindExistingImageAsync(Image image, string imageName)
    {
        // 条件1: 按名称查找 (基本条件)
        var dbImage = await dbContext.Images.FirstOrDefaultAsync(i => i.Name == imageName);
        if (dbImage != null)
            return dbImage;

        // 如果有URL，也可以按URL查找
        if (image.Url != null)
        {
            dbImage = await dbContext.Images.FirstOrDefaultAsync(i =>
                i.Url != null && i.Url == image.Url);
            if (dbImage != null)
                return dbImage;
        }

        // 如果有文件，也可以按文件路径查找
        if (image.File != null && image.File.Exists)
        {
            dbImage = await dbContext.Images.FirstOrDefaultAsync(i =>
                i.File != null && i.File == image.File);
            if (dbImage != null)
                return dbImage;
        }

        // 如果有内容哈希值，可以按哈希值查找
        if (!string.IsNullOrEmpty(image.Hash))
        {
            dbImage = await dbContext.Images.FirstOrDefaultAsync(i => i.Hash == image.Hash);
            if (dbImage != null)
                return dbImage;
        }

        return null;
    }

    /// <summary>
    /// 寻找或者插入一组Picture
    /// </summary>
    public async Task<List<Image>> AddOrFindImagesAsync(List<Image> images, string mediaTitle)
    {
        var dbImages = new List<Image>();
        foreach (var image in images)
        {
            var dbImage = await AddOrFindImageAsync(image, mediaTitle);
            if (dbImage != null)
                dbImages.Add(dbImage);
        }

        return dbImages;
    }


    /// <summary>
    /// 储存图片到本地缓存
    /// </summary>
    /// <param name="image">要储存的图片</param>
    /// <param name="parentDirName">图片所在父文件夹名字，通常为媒体名、社团名、人名</param>
    private async Task<FileInfo> StoreImage(Image image, string parentDirName)
    {
        if (image.File != null)
            return image.File;

        var imageCachePath = Path.Combine(config.Cache.Path, ImgCacheName, parentDirName);
        if (!Directory.Exists(imageCachePath))
            Directory.CreateDirectory(imageCachePath);

        var filePath = Path.Combine(imageCachePath, image.Name);
        if (File.Exists(filePath))
            return new FileInfo(filePath);

        var fileStream = File.Create(filePath);

        if (image.Content != null)
        {
            // 如果有图片内容，直接写入文件
            await fileStream.WriteAsync(image.Content);
        }
        else if (image.Url != null)
        {
            // 如果有URL，下载并写入文件
            var imageBytes = await http.GetBytes(image.Url.ToString());
            await fileStream.WriteAsync(imageBytes);
        }

        fileStream.Close();

        // 图片内容已经写入文件，可以清空Content避免数据库存储过大的二进制数据
        image.Content = null;

        return new FileInfo(filePath);
    }

    private static string GenerateImageFileName(Image image, string parentDirName)
    {
        var originalName = image.GetOriginalName();
        var extension = Path.GetExtension(originalName);
        var hashString = HashEncoder.EncodeToShortHash(parentDirName + originalName);
        return $"{hashString}{extension}";
    }


    /// <summary>
    /// 更新数据库中所有没有Hash值和尺寸信息的图片
    /// </summary>
    public async Task UpdateMissingImageInfoAsync()
    {
        var imagesNeedingUpdate = await dbContext.Images
            .Where(i => i.Hash == null || i.Hash == string.Empty || i.Width <= 0 || i.Height <= 0)
            .ToListAsync();

        if (imagesNeedingUpdate.Count == 0)
        {
            Log.Information("没有找到需要更新信息的图片");
            return;
        }

        Log.Information("找到 {Count} 张需要更新信息的图片，正在更新...", imagesNeedingUpdate.Count);

        int successCount = 0;
        foreach (var image in imagesNeedingUpdate)
        {
            try
            {
                await EnsureImageHashAsync(image);

                if (!string.IsNullOrEmpty(image.Hash) && image.Width > 0 && image.Height > 0)
                {
                    successCount++;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "更新图片 {ImageId} ({ImageName}) 的信息时出错", image.Id, image.Name);
            }
        }

        await dbContext.SaveChangesAsync();
        Log.Information("成功更新了 {SuccessCount} 张图片的信息", successCount);
    }

    /// <summary>
    /// 更新数据库中所有没有Hash值的图片（保持向后兼容）
    /// </summary>
    public async Task UpdateMissingImageHashesAsync()
    {
        await UpdateMissingImageInfoAsync();
    }

    /// <summary>
    /// 清理未使用的图片缓存和数据库中未被引用的图片记录
    /// </summary>
    public async Task RemoveUnusedImgCache()
    {
        Log.Information("开始清理图片缓存和未引用图片");

        // 第一部分：删除数据库中未被任何实体引用的图片记录
        await RemoveUnusedDatabaseImages();

        // 第二部分：删除文件系统中存在但数据库中没有记录的图片文件
        await RemoveOrphanedImageFiles();

        Log.Information("图片缓存和未引用图片清理完成");
    }

    /// <summary>
    /// 删除数据库中未被任何实体引用的图片记录
    /// </summary>
    private async Task RemoveUnusedDatabaseImages()
    {
        Log.Information("开始清理数据库中未被引用的图片记录");

        // 获取所有被引用的图片ID
        var referencedImageIds = new HashSet<int>();

        // 1. 查找作为媒体海报的图片
        var posterIds = await dbContext.Medias
            .Where(m => m.Poster != null)
            .Select(m => m.Poster.Id)
            .ToListAsync();
        posterIds.ForEach(id => referencedImageIds.Add(id));

        // 2. 查找作为社团头像的图片
        var circleAvatarIds = await dbContext.Circles
            .Where(c => c.Avatar != null)
            .Select(c => c.Avatar.Id)
            .ToListAsync();
        circleAvatarIds.ForEach(id => referencedImageIds.Add(id));

        // 3. 查找作为创作者头像的图片
        var creatorAvatarIds = await dbContext.Creators
            .Where(c => c.Avatar != null)
            .Select(c => c.Avatar.Id)
            .ToListAsync();
        creatorAvatarIds.ForEach(id => referencedImageIds.Add(id));

        // 4. 查找作为媒体附加图片的图片
        var mediaPictureIds = await dbContext.Medias
            .SelectMany(m => m.Pictures)
            .Select(p => p.Id)
            .ToListAsync();
        mediaPictureIds.ForEach(id => referencedImageIds.Add(id));

        // 找出所有未被引用的图片
        var unusedImages = await dbContext.Images
            .Where(i => !referencedImageIds.Contains(i.Id))
            .ToListAsync();

        Log.Information("找到 {UnusedImagesCount} 张未被引用的图片记录", unusedImages.Count);

        if (unusedImages.Count > 0)
        {
            // 删除图片记录对应的文件
            foreach (var image in unusedImages)
            {
                if (image.File != null && File.Exists(image.File.FullName))
                {
                    try
                    {
                        File.Delete(image.File.FullName);
                        Log.Information("删除未引用图片文件: {FileFullName}", image.File.FullName);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "删除图片文件失败: {FileFullName}", image.File.FullName);
                    }
                }
            }

            // 从数据库中删除图片记录
            dbContext.Images.RemoveRange(unusedImages);
            await dbContext.SaveChangesAsync();
            Log.Information("已从数据库中删除 {UnusedImagesCount} 张未引用的图片记录", unusedImages.Count);
        }
    }

    /// <summary>
    /// 删除文件系统中存在但数据库中没有记录的图片文件
    /// </summary>
    private async Task RemoveOrphanedImageFiles()
    {
        Log.Information("开始清理文件系统中的孤立图片文件");

        var imageDirectory = Path.Combine(config.Cache.Path, ImgCacheName);
        // 如果不存在图片缓存文件夹，直接返回
        if (!Directory.Exists(imageDirectory))
        {
            Log.Information("图片缓存文件夹不存在，跳过清理");
            return;
        }

        int removedCount = 0;
        var mediaDirectories = Directory.GetDirectories(imageDirectory);
        foreach (var mediaDirectory in mediaDirectories)
        {
            var imageFiles = Directory.GetFiles(mediaDirectory);
            foreach (var imageFile in imageFiles)
            {
                var imageName = Path.GetFileName(imageFile);
                var image = await dbContext.Images.FirstOrDefaultAsync(i => i.Name == imageName);
                if (image == null)
                {
                    try
                    {
                        File.Delete(imageFile);
                        removedCount++;
                        Log.Information("删除孤立图片文件: {ImageFile}", imageFile);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "删除孤立图片文件失败: {ImageFile}", imageFile);
                    }
                }
            }
        }

        Log.Information("已删除 {RemovedCount} 个孤立图片文件", removedCount);
    }
}