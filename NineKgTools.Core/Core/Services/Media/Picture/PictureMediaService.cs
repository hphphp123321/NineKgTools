using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Services.Configs;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.Services.Media.Picture;

public class PictureMediaService(Config config, MediaDbContext dbContext, CreatorService creatorService)
{
    public async Task<PictureMedia> AddOrUpdatePictureAsync(PictureMedia pictureMedia)
    {
        // 先从数据库里找有没有存在的媒体
        var dbPictureMedia = await dbContext.Pictures
            // .AsNoTracking()
            .FirstOrDefaultAsync(vm => vm.Title == pictureMedia.Title);

        if (dbPictureMedia != null)
        {
            // 先删除原有的PictureCreators再添加新的
            dbContext.Pictures.Remove(dbPictureMedia);
            await dbContext.SaveChangesAsync();
        }

        pictureMedia.Actors = pictureMedia.Actors == null
            ? null
            : await creatorService.AddOrUpdateCreators(pictureMedia.Actors);
        pictureMedia.Illustrators = pictureMedia.Illustrators == null
            ? null
            : await creatorService.AddOrUpdateCreators(pictureMedia.Illustrators);
        pictureMedia.Authors = pictureMedia.Authors == null
            ? null
            : await creatorService.AddOrUpdateCreators(pictureMedia.Authors);

        await dbContext.AddAsync(pictureMedia);
        await dbContext.SaveChangesAsync();

        return pictureMedia;
    }
}