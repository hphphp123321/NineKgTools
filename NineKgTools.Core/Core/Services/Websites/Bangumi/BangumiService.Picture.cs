using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Picture;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

public partial class BangumiService
{
    /// <summary>
    /// 把Bangumi的条目信息转为图片媒体
    /// </summary>
    private async Task<PictureMedia> ConvertSubjectInfoToPictureMedia(MediaBase mediaBase, SubjectInfo subjectInfo, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi/Picture] 转换漫画: {subjectInfo.Name}");
        }

        var pictureMedia = new PictureMedia(mediaBase);

        // 默认全部为漫画
        pictureMedia.Category = StaticCategories.Manga;

        if (subjectInfo.Infobox.Remove("页数", out var pageNum))
        {
            if (pageNum is string pageNumString)
            {
                pictureMedia.PageNum = int.Parse(pageNumString);
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync($"[Bangumi/Picture] 页数: {pictureMedia.PageNum}");
                }
            }
        }

        // 获取开发人员
        var persons = await GetSubjectPersonsById(subjectInfo.Id, progressReporter, cancellationToken);
        foreach (var person in persons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var personDetail = await GetPersonDetailById(person.Id, progressReporter, cancellationToken);
            switch (person.Relation)
            {
                case "出版社":
                    pictureMedia.Circle = personDetail?.ToCircle();
                    if (progressReporter != null && personDetail != null)
                    {
                        await progressReporter.DebugAsync($"[Bangumi/Picture] 出版社: {personDetail.Name}");
                    }
                    break;
                case "作者":
                    if (personDetail != null)
                    {
                        pictureMedia.Authors?.Add(personDetail.ToCreator());
                        if (progressReporter != null)
                        {
                            await progressReporter.DebugAsync($"[Bangumi/Picture] 作者: {personDetail.Name}");
                        }
                    }
                    break;
                default:
                    Log.Warning("未知的人员关系{Relation}", person.Relation);
                    break;
            }
        }

        // 填充Infobox
        AddMediaInfos(pictureMedia, subjectInfo.Infobox);

        return pictureMedia;
    }

}