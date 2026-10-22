# API 与序列化迁移说明

## 2026-10 背包与战斗接入

`KitchenObjectSO` 保留原预制体、图标和名称字段，新增公开效果及投掷参数；当前耐久保存在 `ItemInstance`，不写入共享 SO。`KitchenObject.Instance` / `Bind` 让台面代理和背包共享同一实例。

交互入口为 `PlayerItemController.Collect/Equip/Stow/TryUse/TryThrow/TryPlace/TryCollect`；库存由 `InventoryModel` 管理，HUD 订阅变化。`RunSession.Ensure` 跨场景复用，`BeginNew` 仅供新局/重试，`End` 结束本局。不要在重建 UI 时调用 `BeginNew`。

`GameInput` 新增使用、投掷、背包、收起及取消事件；输入资源保留 GUID 并重新生成封装。`GameplayPause` 按所有者管理暂停，使用 `Set(owner, true/false)`，不要由单个面板直接恢复 `Time.timeScale`。

正式战斗使用 `IDamageable`、`DamageInfo`、`CombatTeam`、`ItemProjectilePool`、`RockEnemy` 和 `EnemySpawner`。旧原型 API 不变。`EnemySpawner.items` 的短期旧序列化键 `Items` 由 `FormerlySerializedAs` 保留兼容。

`SceneLoader.Load` 在切换前清理场景暂停；进入菜单结束会话。三关的 `GameplayBootstrap`、Catalog 与 Canvas 已显式配置。参数和结构见 [操作说明](背包与战斗操作说明.md) 及 [当前架构](背包与战斗系统架构.md)。以下为此前资源整理的接口迁移记录。

## 类型和文件

完整类型/文件名称映射见 `type-renames.json`。`CarMo → CameraMove` 是文件名修正，原来的类已经叫 `CameraMove`。保留所有原脚本 `.meta` 的 GUID，场景和预制体的 `m_Script` 引用不通过重新挂载来替换。

`ChoosePanel`、`SettingPanel`、`FinalPanel` 分别改为 `LevelSelectPanel`、`SettingsPanel`、`CompletionPanel`；预制体名称同步更改，保持 Resources 加载约定。`GameControl` 是生成类型，类型名保留；输入资源的生成输出路径已显式指向新的脚本位置。

## 调用接口

| 原接口 | 新接口及行为 |
|---|---|
| `Loader.Load(target)` | `SceneLoader.Load(target, Action onLoaded = null)`；返回 bool，加载中返回 false；通过实际场景名称异步加载 |
| `Loader.LoadBacke()` | `SceneLoader.LoadTargetScene()`；仅供加载场景控制器启动目标异步加载，返回 AsyncOperation |
| `UIManager` 构造时查找并保留 Canvas | `RegisterCanvas(Transform)`、`UnregisterCanvas(Transform)`；由场景的 SceneUIRoot 注册、注销 |
| `IsHaveKitchenObject()` | `HasKitchenObject()` |
| `SetKitchenObject` / `AddKitchenObject` | `TryAddKitchenObject(item)`；校验物品、挂点和已有物品，返回 bool |
| `TransformKitchenObject(source, target)` | 静态 `TryTransferKitchenObject(source, target)`；成功后才清空源，失败不丢失物品 |
| `CreatKitchennObject(prefab)` | ContainerCounter 内部生成函数；子类通过 `TryGiveItem(player)` 共用生成和转移 |
| `KitchenObjectSO.prefab/sprite/objectName` | 私有序列化配置，加只读 `Prefab/Sprite/ObjectName` 属性 |
| `KitchenObject.kitchenObjectSO` | `GetKitchenObjectSO()` |
| `SetFather(actor)` | `Projectile.SetOwner(actor)`、`Weapon.SetOwner(actor)` |
| `JsonMgr.Instance` | `JsonDataStore.Instance`；SaveData/LoadData 签名保留 |
| `JsonType.JsonUtlity` | `JsonType.JsonUtility`；枚举顺序和数值保留 |

仓库内调用已同步更新；本次没有为拼写错误的旧接口添加兼容包装。若项目之外有调用这些 API 的代码，需要按此表更新。

## Inspector 配置

已经正确使用 camelCase 的字段保留名称，只收窄可见性，不改变其序列化键。字段名称变化通过 `FormerlySerializedAs` 兼容，例如：

- `k1/k2/k3/FoxSO` → `firstCounter/secondCounter/thirdCounter/foxDefinition`。
- `tomo/chee/tomoSO/cheeSO/breadSO` → `tomatoCounter/cheeseCounter/tomatoDefinition/cheeseDefinition/breadDefinition`。
- 最终目标 `k1/Cheese` → `counter/requiredItem`。
- 相机 `H` → `height`；订单 `RecipeList` → `recipeList`。
- 战斗 `atk/def/tankHead/deadEff` → `attack/defense/head/deathEffect`；子弹及武器 `fatherObj` → `owner`。

OneShotContainerCounter 和 ButterflyCounter 继承 ContainerCounter，共用其私有序列化 `kitchenObjectSO`；旧场景中的同名配置已经通过引用审查和 Play Mode 流程验证。原来没有被使用的静态计数器、空方法及战斗旋转速度字段已清除。

原有场景和预制体中的旧字段键没有被批量重写或强制重序列化；Unity 读取时通过迁移属性识别它们。今后正常保存资源时，Unity 可能写出新的字段键。
