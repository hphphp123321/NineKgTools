using NineKgTools.Core.DbContexts;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Services.Configs;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.Services.Media.Text;

public class TextMediaService(Config config, MediaDbContext dbContext, CreatorService creatorService)
{
    public async Task<TextMedia> AddOrUpdateTextAsync(TextMedia textMedia)
    {
        // 先从数据库里找有没有存在的媒体
        var dbTextMedia = await dbContext.Texts
            // .AsNoTracking()
            .FirstOrDefaultAsync(vm => vm.Title == textMedia.Title);

        if (dbTextMedia != null)
        {
            // 先删除原有的TextCreators再添加新的
            dbContext.Texts.Remove(dbTextMedia);
            await dbContext.SaveChangesAsync();
        }

        textMedia.Illustrators = textMedia.Illustrators == null
            ? null
            : await creatorService.AddOrUpdateCreators(textMedia.Illustrators);
        textMedia.Author = textMedia.Author == null
            ? null
            : await creatorService.AddOrUpdateCreator(textMedia.Author);

        await dbContext.AddAsync(textMedia);
        await dbContext.SaveChangesAsync();

        return textMedia;
    }
}