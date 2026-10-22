# Fox Unity 项目

Unity 版本：**2022.3.62f3c1**。入口场景：`Assets/_Project/Scenes/StartScene.unity`。

自有资源和代码集中在 `Assets/_Project`；第三方素材位于 `Assets/ThirdParty`。TextMesh Pro 和 TutorialInfo 保留原路径。

- [森林派对操作说明、美术编辑与公开参数](Docs/森林派对操作与参数.md)
- [森林派对架构与审查](Docs/森林派对架构与审查.md)
- [森林派对实际验证结果](Docs/森林派对验证结果.md)

- [背包与战斗操作说明、公开参数](Docs/背包与战斗操作说明.md)
- [当前背包与战斗架构、审查建议](Docs/背包与战斗系统架构.md)
- [项目架构与代码审查报告](Docs/项目架构与代码审查报告.md)
- [工程规范](Docs/工程规范.md)
- [资源迁移映射](Docs/asset-migration.csv)：旧路径、新路径、GUID、原文件 SHA-256。
- [类型名称映射](Docs/type-renames.json)与 [API 变更说明](Docs/API变更说明.md)。
- [实际验证记录](Docs/验证结果.md)。

使用对应 Unity 版本打开项目。静态标题页主按钮直接进入第一关故事，另有选关、设置和退出。三关均使用 Blender 制作的幽灵玩家。第一关寻找三只狐狸，E 道歉后自由组队护送，倒地时长按 E 两秒救援；演出中长按 Esc 一秒跳过。第二、三关保留摆放目标。

WASD 移动，E 拾取/摆放，Tab 背包，点击格子装备，R 收起，左键使用，右键投掷。初始四类道具各三件；第一关最多同时三只、累计十只岩石怪物。跨关保留本局状态，死亡可重试或返回菜单。Blender 源文件位于 `SourceArt/Ghost`，Unity 使用烘焙 FBX；Timeline 位于 `Assets/_Project/Timelines`。

验证入口：Unity 菜单 `Tools/Fox` 和 Test Runner 中的 `FoxProject.Tests`。Windows 开发版位于本地 `Builds/WindowsDevelopment/Fox.exe`，不纳入源码提交。
