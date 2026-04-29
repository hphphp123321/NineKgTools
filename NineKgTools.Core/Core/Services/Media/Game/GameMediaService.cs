using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Game;
using NineKgTools.Core.Services.Configs;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.Services.Media.Game;

public class GameMediaService(
    Config config,
    MediaDbContext dbContext,
    CreatorService creatorService)
{
    public async Task<GameMedia> AddOrUpdateGameAsync(GameMedia gameMedia)
    {
        // 先从数据库里找有没有存在的媒体
        var dbGameMedia = await dbContext.Games
            .FirstOrDefaultAsync(gm => gm.Title == gameMedia.Title);

        if (dbGameMedia != null)
        {
            // 先删除原有的GameCreators再添加新的
            dbContext.Games.Remove(dbGameMedia);
            await dbContext.SaveChangesAsync();
        }

        gameMedia.Authors = gameMedia.Authors == null
            ? null
            : await creatorService.AddOrUpdateCreators(gameMedia.Authors);
        gameMedia.Illustrators = gameMedia.Illustrators == null
            ? null
            : await creatorService.AddOrUpdateCreators(gameMedia.Illustrators);
        gameMedia.Musicians = gameMedia.Musicians == null
            ? null
            : await creatorService.AddOrUpdateCreators(gameMedia.Musicians);
        gameMedia.ScreenWriters = gameMedia.ScreenWriters == null
            ? null
            : await creatorService.AddOrUpdateCreators(gameMedia.ScreenWriters);
        gameMedia.VoiceActors = gameMedia.VoiceActors == null
            ? null
            : await creatorService.AddOrUpdateCreators(gameMedia.VoiceActors);

        await dbContext.AddAsync(gameMedia);
        await dbContext.SaveChangesAsync();

        return gameMedia;
    }
}