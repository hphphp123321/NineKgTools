# 截图 CI 用的脱敏 demo 数据库

`demo.db` 是给 [`.github/workflows/screenshots.yml`](../../../.github/workflows/screenshots.yml) 用的——CI 容器里没有真实的 `Database/database.db`，启动 `NineKgTools.Web` 是空库，截图全是"空状态 UI"。这里准备一份非空、确定性、不含任何真实数据的种子，CI 把它 cp 成 `Database/database.db` 启动，截图就有内容。

## 当前 demo 数据

- **25 条媒体**：5 类各 5 条（音频 / 视频 / 游戏 / 图片 / 文本），标题全部 "演示音频 001" / "演示游戏 011" 这种占位
- **5 个社团**：彩月工房、星海制作所、夜光社团、晨曦工坊、流萤工作室（虚构）
- **10 个创作者**：覆盖 Author / Illustrator / Musician / VoiceActor / ScreenWriter / Director / Actor 七个角色
- **4 个收藏夹**：默认收藏夹 + 正在游玩 / 等更新 / 个人精选
- **8 条 MediaSource**：4 条待识别（identified=false）+ 4 条待入库（identified=true, InDatabase=false）
- **4 条 PendingIdentification**：让"待入库" Tab 有内容
- **5 条 TagMapping**：让标签映射页面有内容
- **375 个 Tag**：从仓库根 `Config/tags.yaml` 读入（跟生产一致）
- **海报**：用 ImageSharp 现场画 600×800 纯色 JPEG，按媒体类别配主题色（音频蓝 / 视频红 / 游戏绿 / 图片黄 / 文本紫）。**不依赖外网**

数据全部用 `new Random(42)` 确定性生成——相同的 commit + 相同的 `tags.yaml` 重复跑，产出逐字节相同的 `demo.db`。

## 什么时候要重新生成

| 触发 | 必须重新生成 |
|---|---|
| `MediaDbContext` schema 变了（加表 / 加列 / 改约束） | ✅ |
| `Config/tags.yaml` 改了，且想让标签变化反映到截图 | ✅ |
| `scripts/seed/SeedDemoDb/Program.cs` 里的造数据逻辑改了 | ✅ |
| 普通 UI / 业务代码改了 | ❌（旧 demo.db 仍兼容） |

## 怎么重新生成

```bash
# 在仓库根
dotnet run --project scripts/seed/SeedDemoDb

# 或指定输出路径
dotnet run --project scripts/seed/SeedDemoDb -- --output /tmp/demo.db
```

输出末尾会打 summary（媒体/社团/创作者/...的条数）。验证条数符合预期后：

```bash
git add docs/assets/seed/demo.db
git diff --cached --stat   # 看体积变化是否合理
git commit -m "Refresh demo.db (schema vN.N.N)"
```

## 怎么本地预览效果

CI 跑之前想本地看看 demo.db 截出来啥样：

```bash
# 1. 备份你自己的库
mv Database/database.db Database/database.db.bak

# 2. 用 demo
cp docs/assets/seed/demo.db Database/database.db

# 3. 启动 + 浏览器看效果
dotnet run --project NineKgTools.Web
# 或者直接跑截图脚本
(cd scripts/screenshots && npm run capture)

# 4. 恢复
mv Database/database.db.bak Database/database.db
```

## 体积

约 750 KB——含 25 张 600×800 JPEG 海报（每张 ~25KB）+ 业务数据 + tag 字典 + 索引。提交时这个数量是可接受的：1 年 5 次 schema 变更 = ~4 MB git history 增长。

## 不该塞什么

- 任何真实的 OPENAI_API_KEY、Bangumi token、个人账号
- 真实媒体的标题、封面、网站链接
- 用户本地的 `MediaSource` 路径（含 `C:\Users\xxx\` 等隐私）

如果哪天 demo.db 里出现了上述内容，**马上撤回 commit**：

```bash
git rm docs/assets/seed/demo.db
git commit -m "Remove leaked demo.db"
git push
# 然后跑 SeedDemoDb 重新生成干净版本再 commit
```
