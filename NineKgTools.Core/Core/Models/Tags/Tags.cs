using NineKgTools.Core.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace NineKgTools.Core.Models.Tags;

public static class StaticTags{

    public static List<Tag> Copy(this List<Tag> tags){
        return tags.Select(tag => tag.Copy()).ToList();
    }
    public static List<TopTag> Copy(this List<TopTag> topTags)
    {
        return topTags.Select(topTag => topTag.Copy()).ToList();
    }
}