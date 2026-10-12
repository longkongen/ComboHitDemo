# ComboHitDemo · 开发路线图

> 微高速动作 ACT Demo（Unity 6 / C#）：**连招 · GP · 极限闪避 · 相杀拼刀 · QTE 处决**
> 最后更新：2026-10-12（阶段 2 完成 —— HUD 显示双方 HP；阶段 3 启动，第 0 步完成 —— 状态计时归位 + 顿帧 + PlayerMotor 帧化）

---

## 0. 我们怎么协作

1. **每阶段开始**：给出「目标 / 要动哪几个文件 / 必须先定的决策 / 验收标准 / 最容易踩的坑」。
2. **实现期**：代码由我自己写；卡住、报错、或冒出新想法就提问，由你来区分这是实现细节问题、设计分歧、还是会破坏后面阶段的想法。
3. **每阶段结束**：我描述现象，你 review 是否达标，再决定进下一阶段或回头补。
4. **每阶段一次 commit**，随时可回退。

## 1. 贯穿全程的纪律（现在就要守，后期改代价极大）

1. **逻辑帧只能有一个时间基准**：✅ 已定 = **自建 60fps 逻辑帧**（`CombatClock`）。全项目只有它读 `Time.deltaTime` 推进逻辑，其他系统一律订阅 `CombatClock.OnTick`，**绝不自带计时器**。
   **执行顺序约定**（靠 `[DefaultExecutionOrder]` 钉死，不靠运气）：
   `输入采样（默认顺序的 Update）→ 逻辑 tick（CombatClock，order 1000）→ 表现（LateUpdate）`
2. **判定窗口按「帧」存，表现按「秒」播**。
3. **状态只有一个来源**：由 `CombatActor` 持有；外部只能读 `State`，只能通过 `SetState` 写。
4. **所有机制都通过"改写判定结果"实现**：GP / 极限闪避 / 相杀 / 一闪都必须走同一个 `CombatResolver` 的优先级仲裁，不各自写一套碰撞。
5. **同帧优先级写死**：`QTE处决 > 相杀 > GP > 极限闪避 > 普通命中`。
6. **静态状态必须手动重置**：本工程禁用了 Domain Reload（`EnterPlayModeOptions = 3`），`static` 字段/事件不会在进 Play 时自动重置。任何带静态状态的类都必须写
   `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 重置方法，否则会出现"第二次 Play 动作快一倍 / `MissingReferenceException`"这类极难查的问题。
7. **调试日志要按"单位时间统计"，不要按"每 N 帧打印一次"**：状态一旦停住，后者会每帧刷屏把现场冲掉（阶段 1 已经踩过这个坑，阶段 3 做顿帧时会再遇到）。
8. **表现层（模型 / 动画 / 特效）只能"读"状态与帧数，绝不允许反向驱动逻辑**：动画播放位置由 `elapsedFrames / 该状态总帧数` 算出的归一化时间驱动，改帧数动画自动跟着变；`Animation Event` 只准用于表现（脚步音、特效、拖尾），**永远不准用来开判定窗口** —— 判定窗口的唯一来源永远是 `AttackData` 的帧数。
9. **引用一律在 Prefab Mode 内接线**：component / 子物体的引用（`owner`、`shape`、`actor`、`attackHitbox`…）必须在 Prefab 资产内部连好；场景实例只负责摆位置与朝向。凡是"只在场景实例上 override"的引用都是定时炸弹 —— 新建实例、他人 clone、或把 Prefab 拖进另一个场景时会**静默失效**（`owner 未接线` → 自我 disable，不报打法错误只报一行 LogError）。已经踩过两次：HUD 的 `player` 字段拖了 Prefab 资产、两个 Prefab 的 `AttackHitbox.owner` 只存在于场景 override 里。

## 2. 机制 → 规则 → 落地文件

| 机制 | 触发条件 | 判定窗口 | 结果 | 落点文件 |
|---|---|---|---|---|
| 连招 | 攻击键 + 输入缓冲 | 各段招式的取消窗口 | 链到下一段 `AttackData` | `CombatController` + `AttackData` |
| GP（弹反） | 防御键 | 起手后 `0~0.12s` 主动帧 | 挡住 + 回耐 + `GuardPoint` | `GuardSystem` |
| 闪避 | 闪避键 | 全身 i-frame 中段 | 位移 + `Dodge` | `DodgeSystem` |
| 极限闪避 | 闪避启动后 `~0.15s` 内被命中 | 启动前段 | `PerfectDodge` + 回耐 | `DodgeSystem` |
| 特殊攻击（气刃连段） | 特殊攻击键 + 消耗气刃 | 每段独立判定帧 | 气刃 1→2→3，**唯一可相杀的招式** | `CombatController` + `SpiritSystem` |
| 相杀（拼刀） | 我方气刃判定帧 ∩ 敌方攻击判定帧 | 同一逻辑帧相交 | 双方 `Clash` → 我方开 QTE 窗口 | `ClashSystem` + `CombatResolver` |
| QTE 处决 | 相杀成功 | 限时输入 | `Execute`，成功后回气刃 | `QTESystem` |
| 一闪 | GP / 极限闪避成功后窗口内 | 成功后 `~0.3s` | `Issen` 高伤反击 + 回耐 | `IssenSystem` |
| 耐力 | 闪避/GP/攻击消耗，GP+极限闪避回复 | — | 资源闸门 | `StaminaSystem` |
| 韧性/破防 | 伤害削减 | — | `Stagger` → 处决机会 | `PostureSystem` |
| 气刃 | 命中回气，气刃斩耗气 | 段位门槛 | 特殊攻击的弹药 | `SpiritSystem` |

## 3. 阶段总表

| 阶段 | 目标（做完能看到什么） | 验收标准 | 难度 | 状态 |
|---|---|---|---|---|
| **0. 地基收尾** | 工程干净、可回滚、可反复实验 | 能 Play、WASD 移动、按攻击键有反馈；已提交基线 | S | ✅ 完成 |
| **1. 招式时间轴** | 攻击不再是硬编码秒数，而是「起手/判定/收招」可调 | 在 Inspector 改数值手感立刻变；能看到判定帧 | S–M | ✅ 完成 |
| **2. 判定层 + 木桩敌人** | 第一次真正「打中」并掉血 | 攻击能打掉木桩血；木桩挥刀能打到我 | M | ✅ 完成 |
| **3. 受击反馈与生死** | 挨打有硬直、击退、顿帧、能死 | 能打死木桩，也能被打死 | S–M | 🟡 进行中 |
| **4. 连招系统** | 三段连起来且「乱按不串招」 | 连按顺畅出 1→2→3；停手回 Idle | M | ⬜ |
| **5. 防御三件套** | 闪避 / 极限闪避 / GP 都能用且回耐 | 闪避能穿攻击；极限闪避有提示并回耐；GP 能弹开并回耐 | M–L | ⬜ |
| **6. 一闪** | GP/极限闪避成功 → 反击 | GP 成功后按攻击键出一闪，直接打掉一大截血 | M | ⬜ |
| **7. 气刃与特殊攻击** | 气刃资源 + 3 段气刃连段 + 段位 | 命中攒气、气刃连段打得出来、没气打不出 | M–L | ⬜ |
| **8. 相杀 / 拼刀（核心）** | 气刃判定帧 ∩ 敌方攻击判定帧 → 双方对峙而非互掉血 | 敌人出招瞬间用气刃顶上去 → 触发 `Clash` 且本帧双方不掉血 | **L** | ⬜ |
| **9. QTE 处决** | 相杀成功 → 限时输入 → 处决 → 回气刃 | 相杀后出提示，按对进 `Execute` 并回气，按错/超时双方后撤 | M | ⬜ |
| **10. 韧性 / 破防** | 不依赖相杀的第二条处决路径 | 连续进攻能打出破防并处决 | M | ⬜ |
| **11. 表现与手感打磨** | 从「能玩」到「看起来是微高速动作」（模型 / 动画 / 特效 / 震屏在此进入；此时帧数表已冻结，动画只需对表一次） | 一整套连段流畅、有打击感 | L | ⬜ |
| **12. 垂直切片** | 一个完整可给人试玩的循环 | 外人能上手打出全部机制 | M | ⬜ |

> 顺序不可倒：阶段 2 的木桩敌人必须早做（GP/极限闪避/相杀全是"对敌人攻击做反应"）；阶段 8 是全项目技术难点，1、2、4、7 都是在为它铺地基。

## 4. 当前进度

**阶段 0（已完成）**
- git 仓库初始化 + `.gitignore`；推送到 GitHub 私有仓库 `https://github.com/longkongen/ComboHitDemo`
- Player 存为 Prefab；移除重复的 `CapsuleCollider`；全部脚本统一 UTF-8；战斗状态收敛到 `CombatActor`
- 清理场景 Player 实例上失效的 `m_RemovedComponents` override
- 修复：输入缓冲泄漏、重复状态迁移、`AttackData.damage` / `recovery` 未生效

**阶段 1（已完成）**
- ✅ 决策：逻辑帧基准 = 自建 60fps 逻辑帧；招式数据 = 每招式一个 `ScriptableObject` 资产
- ✅ **第 1 步**：`AttackData` → `ScriptableObject`（帧为单位）；`Data/` 下 3 个招式资产（Slash1 `10/6/5/11`、Slash2 `15/8/6/13`、Slash3 `25/12/9/21`）；`PlayerCombat` 改为引用 `AttackData[] combo`；临时桥接 `FramesToSeconds` 让节奏与改动前一致
- ✅ **第 2 步**：`CombatClock`（`Scripts/Common/`）—— 累加器 + 固定步长、补帧上限 5 防 spiral of death、`static event OnTick(int frame)` + `static Frame`、`[DefaultExecutionOrder(1000)]`、静态状态重置。实测显示 483fps 时逻辑帧稳定 60/s；场景新增 `System` 物体挂载它
- ✅ **第 3 步**：`PlayerCombat` 帧化 —— `elapsedFrames` 作单一来源（`ChangeState` 里唯一复位）、阶段由帧数派生、订阅 `CombatClock.OnTick`、输入缓冲改帧；`FramesToSeconds` 与 `attackDuration`/`recoveryDuration`/`attackActive` 全部删除
- ✅ **第 4 步**：`CombatDebugHUD`（`Scripts/Common/`）调试 HUD —— 招式名 / 阶段 / 当前帧 / 窗口边界，全 ASCII（IMGUI 字体无中文字形）；`PlayerCombat` 暴露只读 `State` / `CurrentAttack` / `ElapsedFrames`
- 排查记录：HUD 恒显示 Idle 是因为 `player` 字段引用了 **Project 窗口的 Prefab 资产**（不再运行的模板，字段永远是初始值）；现已在 HUD 内加自诊断（`!scene.IsValid()` → "Prefab ASSET" 提示）

**阶段 2（已完成）**
- ✅ 决策：命中检测 = 每逻辑帧主动 `Physics.OverlapBox` 查询；木桩 = 会规律挥刀的假人（与玩家共用 `AttackData` 与判定帧开关）
- ✅ 第 1 步：User Layer `Player`(8) / `Enemy`(9) / `Hitbox`(10) / `Hurtbox`(11) 建好并归位（两个 Prefab + 场景实例）
- ✅ 第 2 步：`Hurtbox`（`owner` + `Awake` 校验）+ `CombatActor.TakeDamage` / `Hp` / `IsDead`
- ✅ 第 3 步：`CombatResolver`（唯一仲裁口）+ `AttackHitbox` 改为主动查询（`OverlapBoxNonAlloc` + `HashSet` 去重 + `Owner` 自伤过滤 + `SyncTransforms`）；实测一套连招伤害 10/15/25 全对，一次挥击只结算一次
- ✅ 第 4 步：木桩 Prefab（沿用了 `Enemy` 这个名字）+ `EnemyCombat` + `Data/Attack_Enemy.asset`（`Slash_Enemy`：起手 18 / 判定 4 / 收招 26 / 伤害 8，配 `idleFrames = 90` → 一个循环 138 逻辑帧 ≈ 2.3 秒一次挥刀）—— 与玩家共用 `AttackData` / 判定帧 / 仲裁口；实测同屏两个 `AttackHitbox` 各自独立去重、双方各自扣血（木桩 8 点 ×3 打玩家 92→84→76；玩家 10/15/10 打木桩 90→75→65，连招正确复位）
- ✅ 第 5 步：`CombatDebugHUD` 显示双方 HP / 状态 / 招式 / 阶段 / 帧数 / 逻辑 tick 三行；字段为空时 `Awake` 用 `FindAnyObjectByType` 自动查找并打一行提示（不再依赖手拖引用）
- 验收实测（`Logs/Editor.log` 1584 行）：`error CS` 0、`Exception` 0、`[CombatDebugHUD]` 1 行、木桩 13 次挥刀（**没有停摆**）、`进入攻击` 21 次、`进入 Idle` 4 次、`被击倒` 1 次 ⇒ 此前"木桩静默"疑点结案（当时多半是木桩已被打死或玩家停手）
- 结论：**判定层 + 木桩敌人全部跑通，阶段 2 完成**
- ✅ 决策：表现层后置 —— 阶段 2–10 只做判定与规则，当前仅加「零资源反馈」（`AttackHitbox` / `Hurtbox` 的 `OnDrawGizmos` 调试可视化 + 朝向指示物），模型/动画留到阶段 11

**阶段 3（进行中）**
- ✅ 决策：**A1** 硬直帧数进 `AttackData.hitStunFrames`（每招式不同）；**B1** 顿帧冻结整个逻辑时钟（不广播 tick + 丢累加器）；**C1** 现在就帧化 `PlayerMotor`，木桩配极简 `EnemyMotor`；**D1** `elapsedFrames` 迁入 `CombatActor` 改名 `StateFrames`
- ✅ 第 0 步：计时归位 + 顿帧开关 + `PlayerMotor` 帧化
  - `CombatActor.stateEnteredFrame` + `public int StateFrames => CombatClock.Frame - stateEnteredFrame;`（**差值式**，`SetState` 内唯一复位）。刻意**不用**"订阅 `OnTick` 自增"：`CombatActor` 与控制器订阅同一个事件，谁先被通知只由组件顺序决定，先后颠倒就会让整条帧数表错一帧；差值式没有这个隐患，而且顿帧期间 `Frame` 不增 ⇒ 所有状态计时**自动冻结**
  - `CombatClock.hitStopFrames` + `public static void RequestHitStop(int frames)`（取 max 不叠加，叠加会导致连击越打越卡）+ `Update` 顶部冻结分支（**必须放在 `accumulator += Time.deltaTime` 之前**，分支内 `hitStopFrames--; accumulator = 0f; return;`）
  - `PlayerMotor` 改「`Update` 只采样输入 / `OnCombatTick` 才位移」，步长一律 `CombatClock.TickDelta`；`PlayerCombat` / `EnemyCombat` 删除各自的计时字段，改读 `actor.StateFrames`
- ⬜ 第 1 步：受击硬直（`AttackData.hitStunFrames` + `CombatActor.ReceiveHit` + 控制器 `Hit` 分支）
- ⬜ 第 2 步：顿帧接命中 · ⬜ 第 3 步：击退（含 `EnemyMotor`）· ⬜ 第 4 步：生死（`Dead` 状态 + 死后关判定）

## 5. 待定决策（到对应阶段再定，届时附上每种选法的代价）

| 决策 | 状态 / 归属阶段 |
|---|---|
| 逻辑帧基准：自建 60fps 累加器 vs `FixedUpdate` 50Hz | ✅ **已定：自建 60fps 逻辑帧**（阶段 1） |
| 招式数据形态：`ScriptableObject` 资产 vs 代码内 `new` | ✅ **已定：每招式一个 SO 资产**（阶段 1） |
| 命中检测方式：物理 trigger 事件 vs 每逻辑帧主动 `OverlapBox` 查询 | ✅ **已定：每逻辑帧主动 `OverlapBox` 查询**（阶段 2）—— 判定必须发生在自己的时钟里，否则阶段 8「同一逻辑帧相交」无从谈起 |
| 木桩敌人形态：纯静止靶子 vs 会规律挥刀的假人 | ✅ **已定：会规律挥刀的假人**（阶段 2）—— 与玩家共用 `AttackData` 与判定帧开关，阶段 5/6/8 直接在它身上试 |
| 硬直时长来源：写进 `AttackData` vs 全局常量 | ✅ **已定：写进 `AttackData.hitStunFrames`**（阶段 3）—— 每招式的硬直本来就该不同（重招打退更久） |
| 顿帧的作用范围：冻结整个逻辑时钟 vs 只暂停参与双方 | ✅ **已定：冻结整个逻辑时钟**（阶段 3）—— 实现最简、语义最直白（拳拳到肉时全场入定），代价是顿帧期间 `OnTick` 不广播，所有系统一起冻结 |
| `PlayerMotor` 的帧化时机：现在 vs 等击退需求出现再改 | ✅ **已定：现在就帧化**（阶段 3）—— 击退必须走逻辑帧；两套时基混用一定会出偏移，而且越晚改越贵 |
| 状态计时的归属：各控制器各自计数 vs 由 `CombatActor` 持有 | ✅ **已定：`CombatActor.StateFrames`**（阶段 3）—— 纪律 #3「状态唯一来源」的必然延伸，且顿帧自动冻结 |
| 表现层引入时机：边做机制边接模型/动画 vs 逻辑优先、表现后置 | ✅ **已定：逻辑优先，表现后置到阶段 11**（阶段 2）—— 帧数表在阶段 5/6/8 还会变动，现在做动画对表等于白干；当前只加「零资源反馈」（Gizmos 判定可视化 + 朝向指示物） |
| GP / 极限闪避成功后「回耐」与「一闪」的关系 | 阶段 6 |
| 一闪的定位与消耗（是否消耗气刃） | 阶段 6–7 |
| 相杀的成立条件（是否要求双方都处于攻击判定帧） | 阶段 8 |
| QTE 处决的输入形式（单键 / 多段 / 方向+按键） | 阶段 9 |

## 6. 已知技术债 / 待办清单

| 项 | 归属阶段 |
|---|---|
| `PlayerCombat.attackDuration` / `recoveryDuration` 已成死字段（时间改由 `AttackData` 驱动） | ✅ 已解决（阶段 1 第 3 步） |
| `attackActive` 一个字段承担两个语义（startup 结束 / 处于判定帧） | ✅ 已解决（阶段 1 第 3 步，改由帧数派生） |
| `FramesToSeconds` 临时桥接（第 3 步删除，即为帧化完成的标志） | ✅ 已解决（阶段 1 第 3 步） |
| `AttackHitbox.damage` 仍是 `public` 字段（应改 `private` + 只读属性） | ✅ 已解决（阶段 2，已改为 `private float damage`） |
| `PlayerMotor` 未走逻辑帧（`Update` 里直接位移，与自建逻辑帧两套时基混用） | ✅ 已解决（阶段 3 第 0 步） |
| `AttackHitbox` 缺 Layer/Tag 过滤（会打到自己与地面） | ✅ 已解决（阶段 2 第 3 步：`LayerMask.GetMask("Hurtbox")` + `Owner` 身份过滤） |
| `AttackHitbox` 缺"同一次挥击只结算一次"的去重 | ✅ 已解决（阶段 2 第 3 步：实例级 `HashSet<Hurtbox>`，`Activate` 时清空） |
| 命中未扣血（`AttackHitbox` 只有日志，还没有 `Hurtbox` 与结算） | ✅ 已解决（阶段 2 第 3 步：经 `CombatResolver` → `CombatActor.TakeDamage`） |
| 无 Tags/Layers 定义（Player / Enemy / Hitbox / Hurtbox） | ✅ 已解决（阶段 2 第 1 步） |
| 「攻击」输入仍直读 `Mouse.current`，未使用 `InputSystem_Actions` 的 `Attack` Action | 4 |
| `AttackData.cs` 的 `menuName` 拼写为 `ComboHit/AttackDate` | 任意 |
| 木桩 Prefab 沿用 `Enemy` 命名，与规划里的 `TrainingDummy` 不一致 | 4 |
| 无 `.asmdef` 分层；`Scripts/Input` 仍为空目录 | 1–4 |
| 项目内行尾混用（`PlayerMotor.cs` 全 LF、`CombatActor.cs` 前 17 行 LF 之后 CRLF、其余 CRLF），建议加 `.gitattributes` 统一 | 任意（2026-10-12 踩坑：按"统一 CRLF"重写产生 51 行 / 126 行**假 diff**；改文件前先 `git ls-files --eol`，按各文件自身行尾写，禁止全局归一化） |
| `TutorialInfo/`、`Readme.asset` 为模板残留 | 任意 |

## 7. 变更记录

- **2026-10-12 · 阶段 2 完成 + 阶段 3 第 0 步完成**
  - 阶段 2 第 5 步：`CombatDebugHUD` 扩成三行（双方 HP / 状态 / 招式 / 阶段 / 帧数 + 逻辑 tick 计数）；`PlayerCombat` / `EnemyCombat` 各暴露只读 `Actor` / `CurrentAttack` / `ElapsedFrames`；HUD 的 `player` / `enemy` 未接线时 `Awake` 自动 `FindAnyObjectByType` 查找并打一行提示
  - 修掉编译警告 `CS0618`：`FindFirstObjectByType` → `FindAnyObjectByType`（"First" 按 instanceID 排序，本工程禁用 Domain Reload 时 instanceID 跨 Play 持续增长 ⇒ 同一次编辑会话里第二次 Play 可能挑中不同对象）
  - 阶段 3 第 0 步：`CombatActor.StateFrames`（差值式计时，`SetState` 内唯一复位）、`CombatClock.RequestHitStop(int)` + `Update` 顶部冻结分支（丢累加器 + 不广播 tick）、`PlayerMotor` 帧化（`Update` 只采样输入 / `OnCombatTick` 位移）、`PlayerCombat` / `EnemyCombat` 删除自有计时字段改读 `actor.StateFrames`
  - **踩坑（行尾）**：仓库内 `PlayerMotor.cs` = LF、`CombatActor.cs` = mixed（1–17 行 LF、18 行起 CRLF）、其余 CRLF；按"统一 CRLF"重写造成 `CombatActor.cs` 51 行 / `PlayerMotor.cs` 126 行假 diff，`git checkout --` 还原后按各文件自身行尾分别写入才收敛（9/0 与 39/39）。**教训：改文件前先 `git ls-files --eol`**
  - 另一课：`Select-String` 默认忽略大小写，查 `elapsedFrames` 会把属性 `ElapsedFrames` 算成残留（假阳性）
  - 验收取证：`error CS` 0、`Exception` 0；DLL 二进制含 `RequestHitStop` / `StateFrames` / `stateEnteredFrame` / `hitStopFrames`；木桩 13 次挥刀无停摆 ⇒ 阶段 2「木桩静默」疑点结案
- **2026-10-10 · 阶段 2 第 4 步完成（木桩会挥刀）**
  - 新增 `Assets/_Project/Scripts/Combat/EnemyCombat.cs`：木桩节拍状态机（`Idle` 90 帧 → `Attack` → `Recovery`，`ChangeState` 为唯一计时复位点），复用 `CombatClock` / `AttackData` / `AttackHitbox` / `CombatResolver`，是 `PlayerCombat` 的简化镜像
  - 新增 `Assets/_Project/Data/Attack_Enemy.asset`；`Assets/_Project/Prefabs/Enemy.prefab` 根物体挂 `EnemyCombat` 并接线 `actor` / `attackHitbox` / `attack` / `idleFrames`
  - 编码：`EnemyCombat.cs` 由 GBK 转 UTF-8 无 BOM（本项目第 3 个 GBK 文件，前两个是 `PlayerCombat.cs` / `AttackHitbox.cs`）；日志前缀 `[DummyCombat]` → `[EnemyCombat]`
  - **踩坑（Prefab 漂移）**：`AttackHitbox.owner` / `shape` 一度只在**场景实例**上存了 override，两个 Prefab 资产里是空的 —— 运行能打中，但新建实例 / 他人 clone 会静默失效（`owner 未接线` → 自我 disable）。已在 Prefab Mode 内补接线，并据此新增纪律 #9
  - 结论：至此"两个角色共用同一条判定链路"成立，阶段 2 只剩第 5 步（HUD 显示双方 HP + 整体验收）
- **2026-10-10 · 阶段 2 第 1–3 步完成 + 表现层决策**
  - 第 1 步：建立 User Layer `Player`(8) / `Enemy`(9) / `Hitbox`(10) / `Hurtbox`(11)（代码里一律 `LayerMask.GetMask(...)`，不硬编码层号）
  - 第 2 步：`Hurtbox`（`owner` + `Awake` 校验，未接线时自我 disable）；`CombatActor` 增加 `hp` / `Hp` / `IsDead` / `TakeDamage`
  - 第 3 步：`CombatResolver.ResolveHit` 成为唯一仲裁口（阶段 5/6/8 的拦截都插在这里）；`AttackHitbox` 从 `OnTriggerEnter` 改为每逻辑帧主动 `Physics.OverlapBoxNonAlloc` 查询，含 `Physics.SyncTransforms()`、`HashSet` 去重、`Owner` 自伤过滤、`Activate()` 内立即查一次（规避"委托调用顺序 = 订阅顺序"导致的判定帧第 0 帧漏查）
  - 踩坑：`LayerMask.GetMask` 写在字段初始化器/静态构造里 → `UnityException: NameToLayer is not allowed...` + `TypeInitializationException`（整个类型废掉，需重编译恢复）。教训：凡"要查引擎内部表"的 API 只能放在 `Awake`/`Start`
  - **决策：表现层后置** —— 当前无模型无动画（Player 胶囊 / Enemy 圆柱），判定链路只依赖 Collider + Layer + 帧数，与美术零耦合；模型/动画/特效统一留到阶段 11。当前只加零资源调试可视化：`AttackHitbox` / `Hurtbox` 的 `OnDrawGizmos`（黄=待命、红=判定帧开着；owner 未接线时画洋红）
  - 纪律 #8 追加：表现层只能读状态与帧数，`Animation Event` 不准开判定窗口
- **2026-10-10 · 阶段 1 完成，阶段 2 启动**
  - 第 3 步：`PlayerCombat` 帧化 —— `elapsedFrames` 单一计时来源、阶段由 `AttackData` 帧数派生、订阅 `CombatClock.OnTick`（`Update` 只做输入采样）、输入缓冲改帧；删除 `FramesToSeconds` 桥接
  - 第 4 步：新增 `CombatDebugHUD` 并挂到场景 `System` 物体；`PlayerCombat` 暴露只读调试属性
  - 排查：HUD 恒显示 `Idle` 的根因是 `player` 字段引用了 Project 窗口的 **Prefab 资产**（不运行的模板）；教训：引用必须拖 Hierarchy 里的场景实例；已在 HUD 内加 `scene.IsValid()` 自诊断
  - 纪律 #7 追加：调试日志按"单位时间统计"而非"每 N 帧打印"
- **2026-10-07 · 阶段 1 第 1、2 步完成**
  - 第 1 步：`AttackData` 改为 `ScriptableObject`（帧为单位、`OnValidate` 夹紧、派生属性）；`Data/` 下建 3 个招式资产并接入 Prefab 的 `combo` 数组；`PlayerCombat` 移除硬编码招式与 `new AttackData(...)`
  - 第 2 步：新增 `CombatClock` 全局唯一时基并挂到场景 `System` 物体；确立「输入采样 → 逻辑 tick → 表现」三层执行顺序；记录"静态状态手动重置"与"日志按单位时间统计"两条纪律（均因本工程禁用 Domain Reload / 探针刷屏事件而来）
  - 修复：`CombatClock.cs` 由 GBK 转 UTF-8；临时探针移除；`MaxTickPerFarame` 拼写修正
- **2026-10-03 · 阶段 1 启动**
  - 决策：逻辑帧基准 = 自建 60fps 逻辑帧；招式数据 = 每招式一个 `ScriptableObject` 资产
  - 原则：本阶段只加马上会被消费的字段
- **2026-10-03 · 阶段 0**
  - git 初始化 + `.gitignore` + 基线提交；推送到 GitHub 私有仓库
  - `PlayerCombat.cs` / `AttackHitbox.cs` 由 GBK 转为 UTF-8（无 BOM）
  - 修复：`CombatActor.state` 写权未收敛；输入缓冲无过期机制；`UpdateRecovery` 重复状态迁移；`AttackData.damage` / `recovery` 未生效
  - 补强：`Awake` 校验关键引用；`Mouse.current` 空引用保护；`ChangeState` 增加 `default` 兜底
  - 清理场景 Player 实例上失效的 `m_RemovedComponents` override
