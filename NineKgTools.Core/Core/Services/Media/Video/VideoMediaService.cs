using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Video;
using NineKgTools.Core.Services.Configs;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.Services.Media.Video;

public class VideoMediaService(Config config, MediaDbContext dbContext, CreatorService creatorService)
{
    public async Task<VideoMedia> AddOrUpdateVideoAsync(VideoMedia videoMedia)
    {
        // 先从数据库里找有没有存在的媒体
        var dbVideoMedia = await dbContext.Videos
            // .AsNoTracking()
            .FirstOrDefaultAsync(vm => vm.Title == videoMedia.Title);

        if (dbVideoMedia != null)
        {
            // 先删除原有的VideoCreators再添加新的
            dbContext.Videos.Remove(dbVideoMedia);
            await dbContext.SaveChangesAsync();
        }


        videoMedia.Makers = videoMedia.Makers == null
            ? null
            : await creatorService.AddOrUpdateCircles(videoMedia.Makers);
        videoMedia.Actors = videoMedia.Actors == null
            ? null
            : await creatorService.AddOrUpdateCreators(videoMedia.Actors);
        videoMedia.Illustrators = videoMedia.Illustrators == null
            ? null
            : await creatorService.AddOrUpdateCreators(videoMedia.Illustrators);
        videoMedia.Musicians = videoMedia.Musicians == null
            ? null
            : await creatorService.AddOrUpdateCreators(videoMedia.Musicians);
        videoMedia.ScreenWriters = videoMedia.ScreenWriters == null
            ? null
            : await creatorService.AddOrUpdateCreators(videoMedia.ScreenWriters);
        videoMedia.Directors = videoMedia.Directors == null
            ? null
            : await creatorService.AddOrUpdateCreators(videoMedia.Directors);

        await dbContext.AddAsync(videoMedia);
        await dbContext.SaveChangesAsync();

        return videoMedia;
    }
}