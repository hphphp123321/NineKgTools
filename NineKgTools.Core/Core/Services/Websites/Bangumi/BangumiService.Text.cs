using NineKgTools.Core.Models.Categories;
using NineKgTools.Core.Models.Media;
using NineKgTools.Core.Models.Media.Text;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

public partial class BangumiService
{
    /// <summary>
    /// 把Bangumi的条目信息转为书籍媒体
    /// </summary>
    private async Task<TextMedia> ConvertSubjectInfoToTextMedia(MediaBase mediaBase, SubjectInfo subjectInfo, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi/Text] 转换书籍: {subjectInfo.Name}");
        }

        var bookMedia = new TextMedia(mediaBase);

        // 默认全部为小说
        bookMedia.Category = StaticCategories.Novel;

        if (subjectInfo.Infobox.Remove("册数", out var bookNum))
        {
            if (bookNum is string bookNumString)
            {
                bookMedia.BookNum = int.Parse(bookNumString);
                if (progressReporter != null)
                {
                    await progressReporter.DebugAsync($"[Bangumi/Text] 册数: {bookMedia.BookNum}");
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
                case "作者":
                    bookMedia.Author = personDetail?.ToCreator();
                    if (progressReporter != null && personDetail != null)
                    {
                        await progressReporter.DebugAsync($"[Bangumi/Text] 作者: {personDetail.Name}");
                    }
                    break;
                case "插画":
                    if (personDetail != null)
                    {
                        bookMedia.Illustrators?.Add(personDetail.ToCreator());
                    }
                    break;
                case "出版社":
                    bookMedia.Circle = personDetail?.ToCircle();
                    if (progressReporter != null && personDetail != null)
                    {
                        await progressReporter.DebugAsync($"[Bangumi/Text] 出版社: {personDetail.Name}");
                    }
                    break;
                default:
                    Log.Warning("未知的人员关系{Relation}", person.Relation);
                    break;
            }
        }

        // 填充Infobox
        AddMediaInfos(bookMedia, subjectInfo.Infobox);

        return bookMedia;
    }

}
