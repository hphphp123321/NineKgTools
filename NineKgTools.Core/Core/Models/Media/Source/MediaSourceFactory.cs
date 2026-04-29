namespace NineKgTools.Core.Models.Media.Source;

public static class MediaSourceFactory
{
    public static int CreateCount = 0;

    // 在./tmp/下创建一个文件
    public static MediaSource Create()
    {
        CreateCount++;
        var fileName = "./tmp/" + CreateCount;
        File.Create(fileName).Close();
        return new MediaSource(fileName);
    }

    public static MediaSource Create(string path)
    {
        return new MediaSource(path);
    }
}
