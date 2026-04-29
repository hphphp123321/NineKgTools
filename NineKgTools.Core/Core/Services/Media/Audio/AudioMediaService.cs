using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Audio;
using NineKgTools.Core.Services.Configs;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.Services.Media.Audio;

public class AudioMediaService(
    Config config,
    MediaDbContext dbContext,
    CreatorService creatorService)
{
    public async Task<AudioMedia> AddOrUpdateAudioAsync(AudioMedia audioMedia)
    {
        // 先从数据库里找有没有存在的媒体
        var dbAudioMedia = await dbContext.Audios
            // .AsNoTracking()
            .FirstOrDefaultAsync(gm => gm.Title == audioMedia.Title);

        if (dbAudioMedia != null)
        {
            // 先删除原有的AudioCreators再添加新的
            dbContext.Audios.Remove(dbAudioMedia);
            await dbContext.SaveChangesAsync();
        }

        if (audioMedia.Authors != null)
        {
            audioMedia.Authors = await creatorService.AddOrUpdateCreators(audioMedia.Authors);
        }
        if (audioMedia.Illustrators != null)
        {
            audioMedia.Illustrators = await creatorService.AddOrUpdateCreators(audioMedia.Illustrators);
        }
        if (audioMedia.Musicians != null)
        {
            audioMedia.Musicians = await creatorService.AddOrUpdateCreators(audioMedia.Musicians);
        }
        if (audioMedia.ScreenWriters != null)
        {
            audioMedia.ScreenWriters = await creatorService.AddOrUpdateCreators(audioMedia.ScreenWriters);
        }
        if (audioMedia.VoiceActors != null)
        {
            audioMedia.VoiceActors = await creatorService.AddOrUpdateCreators(audioMedia.VoiceActors);
        }

        await dbContext.AddAsync(audioMedia);
        await dbContext.SaveChangesAsync();

        return audioMedia;
    }
}