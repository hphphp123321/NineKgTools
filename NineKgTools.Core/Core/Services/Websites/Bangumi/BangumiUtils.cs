using System.Text.Json;
using System.Text.RegularExpressions;
using NineKgTools.Core.Services.Tasks.Interfaces;
using NineKgTools.Core.Services.Websites.Bangumi.Model;
using Serilog;

namespace NineKgTools.Core.Services.Websites.Bangumi;

public partial class BangumiService
{
    /// <summary>
    /// 获取Bangumi条目信息
    /// </summary>
    /// <param name="subjectId">条目ID</param>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    private async Task<SubjectInfo?> GetSubjectInfoById(int subjectId, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取条目信息: ID={subjectId}");
        }

        var url = $"{_bangumiApiUrl}/v0/subjects/{subjectId}";
        var response = await http.Get(url, authorization: authorization, cancellationToken: cancellationToken);
        if (response == null)
        {
            Log.Error("获取Bangumi条目{SubjectId}信息失败", subjectId);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Bangumi] 获取条目信息失败: ID={subjectId}");
            }
            return null;
        }

        var subjectInfo = JsonSerializer.Deserialize<SubjectInfo>(response);

        Log.Debug("成功获取到Bangumi条目信息：{SubjectId} -> {SubjectName}", subjectInfo.Id, subjectInfo.Name);
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 条目: {subjectInfo.Name}");
        }
        return subjectInfo;
    }

    /// <summary>
    /// 获取Bangumi条目中的人物信息
    /// </summary>
    /// <param name="subjectId">条目ID</param>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>人物列表</returns>
    private async Task<List<PersonInfo>?> GetSubjectPersonsById(int subjectId, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取人物信息: ID={subjectId}");
        }

        var url = $"{_bangumiApiUrl}/v0/subjects/{subjectId}/persons";
        var response = await http.Get(url, authorization: authorization, cancellationToken: cancellationToken);
        if (response == null)
        {
            Log.Error("获取Bangumi条目{SubjectId}人物信息失败", subjectId);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Bangumi] 获取人物信息失败: ID={subjectId}");
            }
            return null;
        }

        var personInfos = JsonSerializer.Deserialize<List<PersonInfo>>(response);
        Log.Debug("成功获取到Bangumi条目人物信息：{SubjectId} -> {PersonCount}人", subjectId, personInfos.Count);
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取到 {personInfos.Count} 位人物");
        }
        return personInfos;
    }

    /// <summary>
    /// 获取Bangumi人物具体信息
    /// </summary>
    /// <param name="personId">人物ID</param>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>人物的具体信息，包含血型、生日等信息</returns>
    private async Task<PersonDetail?> GetPersonDetailById(int personId, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var url = $"{_bangumiApiUrl}/v0/persons/{personId}";
        var response = await http.Get(url, authorization: authorization, cancellationToken: cancellationToken);
        if (response == null)
        {
            Log.Error("获取Bangumi人物信息失败");
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Bangumi] 获取人物详情失败: ID={personId}");
            }
            return null;
        }

        var personDetail = JsonSerializer.Deserialize<PersonDetail>(response);
        return personDetail;
    }

    /// <summary>
    /// 获取Bangumi条目中的角色信息
    /// </summary>
    /// <param name="subjectId">条目ID</param>
    /// <param name="progressReporter">进度报告器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>角色列表</returns>
    private async Task<List<CharacterInfo>?> GetSubjectCharactersById(int subjectId, IProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取角色信息: ID={subjectId}");
        }

        var url = $"{_bangumiApiUrl}/v0/subjects/{subjectId}/characters";
        var response = await http.Get(url, authorization: authorization, cancellationToken: cancellationToken);
        if (response == null)
        {
            Log.Error("获取Bangumi条目{SubjectId}角色信息失败", subjectId);
            if (progressReporter != null)
            {
                await progressReporter.WarningAsync($"[Bangumi] 获取角色信息失败: ID={subjectId}");
            }
            return null;
        }

        var characterInfos = JsonSerializer.Deserialize<List<CharacterInfo>>(response);

        Log.Debug("成功获取到Bangumi条目角色信息：{SubjectId} -> {CharacterCount}个角色", subjectId, characterInfos.Count);
        if (progressReporter != null)
        {
            await progressReporter.DebugAsync($"[Bangumi] 获取到 {characterInfos.Count} 个角色");
        }
        return characterInfos;
    }


    private static int? GetSubjectIdFromUrl(string url)
    {
        var match = SubjectIdRegex().Match(url);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    /// <summary>
    /// 判断是否为漫画，因为漫画和书籍的类型都是SubjectType.Book
    /// </summary>
    /// <param name="subjectInfo"></param>
    /// <returns></returns>
    private bool IsManga(SubjectInfo subjectInfo)
    {
        // 通过tags来判断
        var tags = subjectInfo.Tags;
        return tags.Any(tag => tag.Name == "漫画");
    }

    [GeneratedRegex(@"subject/(\d+)")]
    private static partial Regex SubjectIdRegex();
}