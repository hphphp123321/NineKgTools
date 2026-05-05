namespace NineKgTools.Desktop.Views.Dialogs;

/// <summary>
/// 共享对话框的 4 种 intent 分类：决定 accent 色 / 默认图标 / 是否显示"不可撤销"警告 / 是否显示 Hero count。
/// 与 Web 端 NineKgConfirmDialog 的 Intent 语义保持一致（详见 CLAUDE.md "共享弹窗体系" 章节）。
/// </summary>
public enum DialogIntent
{
    /// <summary>蓝色 accent / 一般信息确认（如取消任务、停止后台、向量同步）</summary>
    Info,

    /// <summary>绿色 accent / 肯定动作（如批量入库、加入识别队列）</summary>
    Affirmative,

    /// <summary>红色 accent + "不可撤销"警告 / 不可逆破坏（删媒体/标签/创作者/收藏夹）</summary>
    Destructive,

    /// <summary>红色 accent + Hero count 大数字 + "不可撤销" / 批量破坏（批量删除、批量丢弃）</summary>
    DestructiveBatch,
}
