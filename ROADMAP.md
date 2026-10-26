# ComboHitDemo · 开发路线图

> 微高速动作 ACT Demo（Unity 6 / C#）：**连招 · GP · 极限闪避 · 相杀拼刀 · QTE 处决**
> 最后更新：2026-10-13（阶段 4 连招系统完成；**阶段 5 防御三件套进行中** —— 第 0 步耐力地基、第 1 步普通防御 + 前方格挡盒、第 2 步 GP 弹开已落地，下一步闪避位移 + 无敌帧）

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
| **3. 受击反馈与生死** | 挨打有硬直、击退、顿帧、能死 | 能打死木桩，也能被打死 | S–M | ✅ 完成 |
| **4. 连招系统** | 三段连起来且「乱按不串招」 | 连按顺畅出 1→2→3；停手回 Idle | M | ✅ 完成 |
| **5. 防御三件套** | 闪避 / 极限闪避 / GP 都能用且回耐 | 闪避能穿攻击；极限闪避有提示并回耐；GP 能弹开并回耐 | M–L | 🟡 进行中 |
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

**阶段 3（已完成）**
- ✅ 决策：**A1** 硬直帧数进 `AttackData.hitStunFrames`（每招式不同）；**B1** 顿帧冻结整个逻辑时钟（不广播 tick + 丢累加器）；**C1** 现在就帧化 `PlayerMotor`，木桩配极简 `EnemyMotor`；**D1** `elapsedFrames` 迁入 `CombatActor` 改名 `StateFrames`
- ✅ 第 0 步：计时归位 + 顿帧开关 + `PlayerMotor` 帧化
  - `CombatActor.stateEnteredFrame` + `public int StateFrames => CombatClock.Frame - stateEnteredFrame;`（**差值式**，`SetState` 内唯一复位）。刻意**不用**"订阅 `OnTick` 自增"：`CombatActor` 与控制器订阅同一个事件，谁先被通知只由组件顺序决定，先后颠倒就会让整条帧数表错一帧；差值式没有这个隐患，而且顿帧期间 `Frame` 不增 ⇒ 所有状态计时**自动冻结**
  - `CombatClock.hitStopFrames` + `public static void RequestHitStop(int frames)`（取 max 不叠加，叠加会导致连击越打越卡）+ `Update` 顶部冻结分支（**必须放在 `accumulator += Time.deltaTime` 之前**，分支内 `hitStopFrames--; accumulator = 0f; return;`）
  - `PlayerMotor` 改「`Update` 只采样输入 / `OnCombatTick` 才位移」，步长一律 `CombatClock.TickDelta`；`PlayerCombat` / `EnemyCombat` 删除各自的计时字段，改读 `actor.StateFrames`
- ✅ 第 1 步：受击硬直（`AttackData.hitStunFrames` + `CombatActor.ReceiveHit` + 控制器 `Hit` 分支）
  - `AttackData.hitStunFrames`（每招式不同，`OnValidate` 夹 ≥1）；4 个资产实填 **10 / 14 / 20 / 12**（Slash1 / Slash2 / Slash3 / Enemy）
  - `CombatActor.lastHit` + `public AttackData LastHit` + `public void ReceiveHit(AttackData attack)`（`TakeDamage` → `if (IsDead) return;` → 记 `lastHit` → `SetState(Hit)`）；`CombatResolver.ResolveHit(attacker, defender, attack)` 改传整个 `AttackData`（阶段 8 相杀需要"招式身份"而非数值）；`AttackHitbox.Activate(AttackData)`（原 `float damage` 每加一个效果就得改签名）
  - 两个控制器的 `Hit` 分支 `UpdateHit()`：**每帧幂等**地收刀 + 清输入缓冲与 `comboStep`，`actor.StateFrames >= LastHit.hitStunFrames` 时回 `Idle`（刻意不写"进入 `Hit` 的第 0 帧触发一次" —— `StateFrames == 0` 不可靠）
  - `PlayerMotor` 读 `CombatActor.State`：`Hit` / `Dead` 期间跳过水平位移，重力照常
  - 死亡兜底：`CombatActor.ReceiveHit` 在致命伤时提前 `return`（不进 `Hit`），因此 `Dead` 状态由控制器每 tick 兜底切换（否则真人会原地继续挥刀）
  - 验收取证（`Logs/Editor.log`）：`[CombatActor] Player 进入硬直：Slash_Enemy（12 帧）` → `PlayerCombat:UpdateHit` → `进入 Idle`；`error CS` 0
  - 排查记录：用户报"受击后仍能移动" —— 实为 **12 逻辑帧 = 0.2 秒**，而木桩每 2.3 秒才挥一刀，且本阶段还没有顿帧/击退/视觉反馈，肉眼分辨不出；判定实验 = 把 `Attack_Enemy.hitStunFrames` 临时改 120
  - 已证实的事实：SO 资产 YAML 缺字段时运行期取 **C# 字段初始值**（`Attack_Enemy` 缺字段却打出 12 帧），不是取 0
- ✅ 第 2 步：顿帧接命中（验收通过；用户于 2026-10-12 确认后进入第 3 步）
  - `AttackData.hitStopFrames`（`OnValidate` 夹 **≥ 0** —— 0 是合法语义"这一招不顿帧"；硬直才必须 ≥ 1）；4 个资产实填 **4 / 6 / 10 / 6**（Slash1 / Slash2 / Slash3 / Enemy = 67 / 100 / 167 / 100 ms @60Hz）
  - `CombatResolver.ResolveHit` 末尾加一行 `CombatClock.RequestHitStop(attack.hitStopFrames);` —— 顿帧属于"这次命中"而非任何一方；位置在所有守卫之后 ⇒ 空挥不顿帧、已死目标不顿帧、多次命中取 max 不叠加
  - **修掉一个真 bug（计时单位）**：`CombatClock` 原来的冻结是 `Update` 顶部 `hitStopFrames--`，即**每渲染帧扣 1** ⇒ 顿帧真实时长随渲染帧率漂移（本机 483 fps 时 `RequestHitStop(6)` 只有 **12 ms**，30 fps 的机器却要 200 ms，16 倍差）。改为**在 tick 循环里消费**（`ticks++` 提前，`if (hitStopFrames > 0) { hitStopFrames--; continue; }`），于是无论渲染帧率多少，冻结的都是**精确 N 个逻辑帧**；这一格时间是被"吃掉"而不是丢弃，因此解冻既不快进也不丢时间 —— 顶部的早退分支与 `accumulator = 0f` 一并删除
  - 待验收 6 条（时长与渲染帧率无关 / 双方一起冻结 / 解冻不瞬移 / 判定盒保持开着 / 空挥不顿帧 / 反复 Play 不累积）
- ✅ 第 3 步：击退 + `EnemyMotor`（验收通过）
  - 规则：命中时给受击方一个「沿攻击者正前方、持续 N 个逻辑帧、总位移 D 米」的恒速推离。`AttackData.knockbackDistance`（米，`OnValidate` 夹 ≥ 0）+ `knockbackFrames`（帧，夹 ≥ 0；**0 是合法语义"这一招不击退"**，且此时不设速度、避免除零）；4 个资产实填 **0.4/8 · 0.7/10 · 1.4/14 · 0.5/10**（Slash1 / Slash2 / Slash3 / Enemy）
  - 方向在 `CombatResolver.ResolveHit` 里算（只有这里同时握着攻守双方）：`attacker.transform.forward` 压平 y、归一化，`sqrMagnitude < 0.0001f` 时退回 `Vector3.forward`；然后 `defender.ReceiveHit(attack, knockbackDir)`
  - `CombatActor` 持有击退状态，**用截止帧号而不是自减计数器**：`private Vector3 knockbackVelocity;`（米/秒）+ `private int knockbackEndFrame;` + `public Vector3 KnockbackVelocity => (CombatClock.Frame < knockbackEndFrame) ? knockbackVelocity : Vector3.zero;` —— 与 `StateFrames` 同款差值式：不新增订阅（不会重新引入组件顺序问题）、顿帧期间 `CombatClock.Frame` 不增 ⇒ **击退自动冻结**、没有任何需要重置的计数器
  - 单位坑（写错会小 60 倍）：速度存**米/秒** = `距离 / (帧数 × TickDelta)`，移动方每帧再乘 `TickDelta`
  - `PlayerMotor.OnCombatTick` = `if (canMove) Move();` → `ApplyKnockback();` → `ApplyGravity();` —— 击退**刻意放在输入锁之外**，否则 `Hit` / `Dead` 期间 `canMove == false` 会把击退一起挡掉；新建 `Assets/_Project/Scripts/EnemyMotor.cs`（不采样输入，只做被推 + 落地），`Enemy.prefab` 根加 `CharacterController`（Height 2 / Radius 0.5 / Center **(0,0,0)**，与 Player 逐字一致）并**删掉根上原有的 `CapsuleCollider`**（留着会与 CharacterController 重叠成两套碰撞体）
  - **修掉一个编译阻塞**：`CombatActor.cs:30` 残留半截行 `Vector3 knockbackDirection` → `error CS1002: ; expected`，整工程无法编译（`Logs/Editor.log:17225`）
  - 判定层不受影响：`AttackHitbox.cs:19` 用 `LayerMask.GetMask("Hurtbox")`，木桩新的 CharacterController 在 `Enemy`(9) 层，不会被当受击盒；场景两个 Prefab 实例均 `m_RemovedComponents: []`，没有指向已删 CapsuleCollider 的残留引用
  - 验收 6 条：① 位移随招式变化（Slash1 ≈0.4 m / Slash3 ≈1.4 m）② 绕到侧面打 → 沿**攻击者面朝方向**推（不是两人连线）③ 顿帧期间双方不动、解冻后才开始退 ④ 自己被打中 → 被推，且推期间按 WASD 无效 ⑤ 连续两刀不叠加成两倍速 ⑥ 不穿模、反复 Play 3 次一致
- ✅ 第 4 步：生死（`Dead` 状态真正切换 + 死后关判定 + 木桩停手）
  - 规则：死亡不再是"控制器每帧兜底"，而是 `CombatActor.TakeDamage` 在 HP 归零时自己 `SetState(CombatState.Dead)`（决策 2A：状态唯一来源）—— 同一逻辑帧内任何读 `actor.State` 的人都立刻看到 `Dead`
  - 死后收刀（每帧幂等）：`EnemyCombat` / `PlayerCombat` 的死亡收尾无条件 `attackHitbox.Deactivate()` + 清 `attackBuffered` / `bufferFramesLeft` / `comboStep`。**修掉一个真 bug**：`EnemyCombat` 原来的死亡分支只置状态、没收刀 ⇒ 木桩若死在判定帧里判定盒永远开着，**尸体继续打人**
  - 尸体不算命中：`AttackHitbox` 在去重之后插 `if (hurtbox.Owner.IsDead) continue;`（决策 3A），`CombatResolver` 的 `defender.IsDead` 守卫保留作双保险
  - 决策 1A：死亡瞬间保留击退 —— 尸体滑一下再停；`KnockbackVelocity` 本来就是"用截止帧号算出来的属性"，与状态无关，因此这条不需要额外代码
  - `EnemyMotor.cs` / `.meta` 从 `Scripts/` 移到 `Scripts/Character/`（与 `PlayerMotor` 同目录）；`.meta` 的 guid 未变 ⇒ `Enemy.prefab` 的 `m_Script` 引用照旧解析，git 识别为纯重命名 `R100`
  - **修掉一处坏粘贴**：死亡分支被贴进了 `EnemyMotor.OnDisable()` —— 那个 `return` 把 `-= OnCombatTick` 退订挡掉了（本工程禁用 Domain Reload ⇒ 重进 Play 会**重复订阅**，一次 tick 走两遍），且产生 `error CS0103: The name 'attackHitbox' does not exist in the current context`（`Logs/Editor.log:19468`）；同时清掉 `Enemy.prefab` 里为该字段序列化出的 `attackHitbox:` 行
  - 验收 6 条（用户确认第 ② 条通过：打死木桩后判定盒 Gizmos 是**黄框** = 已收刀）：① 木桩死亡立刻收刀、之后无攻击日志 ② 死在 `ACTIVE` 帧内也不再打到你 ③ 对尸体挥刀无伤害 / 无顿帧 / 不算命中 ④ 玩家死后不能动不能攻击、HUD 显示 `DEAD`，尸体仍被推 ⑤ 死后 HP 不再变化 ⑥ 反复进出 Play 3 次一致
  - 提交 `f5b5388`（6 文件 +12/−5，含两个 `R100` 重命名）
  - 教训：改含中文的源码用 `edit` 工具比 pwsh 精确匹配可靠（pwsh 里全角引号 `“ ”` 会让整段脚本无法解析；中文匹配串经命令行传输会对不上，报 `expected 1 match, got 0`）

**阶段 4（已完成）**
- ✅ 决策：**1A** 连招窗口 = `[判定结束, 收招结束 − comboWindowCloseEarly)`（派生 + 提前关窗，不做显式绝对帧字段，避免与 `startup/active/recovery` 重复）；**2A** 接续关系用 `ComboChain` SO 描述（每段声明"允许接什么"，为分支/终结技铺路）；**3A** 本阶段第 1 步就抽共享 `CombatController`（`PlayerCombat` / `EnemyCombat` 现在是两份几乎相同的状态机，阶段 5/6/8 还要往里加东西，现在抽最便宜）
- ✅ 第 0 步：连招窗口规则化（**取消语义**）
  - `AttackData.comboWindowCloseEarly`（默认 0，`OnValidate` 夹 ≥0）+ 派生属性 `ComboWindowCloseFrame => Mathf.Max(0, recoveryFrames - comboWindowCloseEarly)`；只有 `Attack_Slash3` 实填 **3**（终结技最后 3 帧按了不接，收束感），其余三招取 C# 默认 0
  - **修正原先的认知**：旧实现（`if (actor.StateFrames >= currentAttack.recoveryFrames)` 里才看缓冲）**必须把整段收招播完**，收招帧数一直在起作用；只是 `inputBufferFrames = 30` 比任何一招都长（Slash1 全程 22 帧），才使得"招式任何一帧按键都有效"。新实现把两件事分开：**窗口内按键立刻取消剩余收招接下一段**（真正意义上的 cancel window），窗口关闭后丢弃缓冲
  - 收招期间 `CombatActor.StateFrames` 从 **1** 开始（进入 Recovery 的那一帧已被 `UpdateAttack` 用掉）⇒ 窗口条件是 `<=`，不是 `<`
  - 顺手统一哨兵：`comboStep = 0` = "没有连招在进行中"（原先死亡收尾写 1、受击/终止收尾写 0，两种表示并存）；`UpdateIdle` 起手前会设成 1
  - 可调档位：若想要"必须播完收招"的重量感，把 `StartAttack()` 那一支改成只记 `chainPending = true`、到收招末帧再执行即可（窗口退化成输入门）
- ✅ 第 1 步：抽 `CombatController`（共享招式播放器）
  - 新增基类 `Assets/_Project/Scripts/Combat/CombatController.cs`（120 行，由阶段 0 的空占位文件变成实体）：字段名与原子类**完全一致** —— `[SerializeField] protected CombatActor actor;` / `[SerializeField] protected AttackHitbox attackHitbox;`（Unity 按字段名反序列化 ⇒ 改名会让 Prefab 引用静默变 None）；`Awake` 自解析 + 缺失即 `LogError` + `enabled = false`
  - 基类负责"一招怎么播"：`StartAttack(AttackData)` / `TickAttack()`（`StateFrames == ActiveStartFrames` 开判定、`>= ActiveEndFrames` 关判定并进收招）/ `TickRecovery()`（默认播完 → `OnComboEnded` + `ClearPendingInput` + `Idle`）/ `TickHit()`（每帧幂等收刀，按 `LastHit.hitStunFrames` 回 `Idle`）/ 死亡幂等收尾 / `ChangeState` 日志
  - 子类只答"谁在打、打哪一招"：挂钩点 `OnPreTick` / `OnIdleTick` / `OnComboEnded` / `ClearPendingInput`。`PlayerCombat` 246 → 83 行（只留输入采样 + 缓冲 + 连招窗口覆盖），`EnemyCombat` 85 → 17 行（只留 `attack` + `idleFrames` + 数节拍）
  - **重构时丢过一处校验**：`PlayerCombat.Awake` 里的 combo 数组校验（null / 长度 < `maxCombo` / 空槽位 → `LogError` + `enabled = false`）被一行注释顶掉，已补回 —— 教训：抽基类时逐条对照旧文件的生命周期收尾（`Awake` / 死亡 / 受击），别只搬状态机
  - 顺手修：`CombatController.cs` 是第 5 个 GBK 文件 → 转 UTF-8 无 BOM（CRLF 119 / 裸 LF 0 / U+FFFD 0）；`EnemyCombat.cs` 缩进 8 → 4 空格
  - **新验证手法（可复用，不必等 Unity 获得焦点）**：`& 'D:\unity\6000.6.2f1\Editor\Data\DotNetSdk\dotnet.exe' build Assembly-CSharp.csproj -nologo -v:m`（Unity 自己生成的工程文件）→ 2.2 秒拿到 `0 个错误`（只有 7 条 nunit / 目标框架版本的历史警告）；产物落在 `Temp/bin/Debug/`，不碰 `Library/ScriptAssemblies/`
  - 验收靠"行为逐帧不变"（木桩节拍、10/15/25 伤害、12 帧硬直、死亡黄框、四行状态日志格式一字不差）+ 一条硬证据：**`git diff --stat` 里不出现 `Player.prefab` / `Enemy.prefab` / `SampleScene.unity`**（引用一个字都没动）
- ✅ 第 2 步：`ComboChain` SO（每段声明允许接什么）
  - 新增 `Assets/_Project/Scripts/Combat/ComboChain.cs`：`[CreateAssetMenu(… menuName = "ComboHit/ComboChain")]`；嵌套 `[System.Serializable] class Node { public AttackData attack; public List<AttackData> next; }`；`entry` + `nodes` 两个序列化字段；`public AttackData NextOf(AttackData current, int candidateIndex = 0)`（当前段找不到节点 / 无候选 / 下标越界 → 返回 `null` = 链到头；`candidateIndex` 已留好，阶段 7 按输入挑候选即可分支）
  - `OnValidate` 六条自检把数据错误拦在编辑器里：`entry` 为空 → 报错并返回；`entry` 不在节点表；某节点 `attack` 为空；某条边为空；**自环**（自己接自己 = 无限连）；**死边**（边指向的招式不在节点表里，永远接不到）
  - 资产 `Assets/_Project/Data/ComboChain_Player.asset`（guid `5a2d9c7e1b3f4a6d8e0c2b4f6a8d1e3c`）：`entry = Attack_Slash1`；`Slash1 → [Slash2]`、`Slash2 → [Slash3]`、`Slash3 → []`；链的末端就是终点，`maxCombo` 常量退役
  - `PlayerCombat`：`comboStep`（下标）→ `currentNode`（链上当前节点）+ `chainDepth`（本套已打出几段）；`Awake` 改为只校验 `chain == null || chain.Entry == null`（链本身的数据问题归 `OnValidate` 管）；新增 `private const int maxChainDepth = 16` —— 链是数据、写环也不会无限连，兜底时额外打一条 `LogWarning` 提示链里有环
  - **`next == null` 也必须清输入缓冲**（与"窗口关闭"同处理）：否则关窗前按下的那一键会在收招末帧"自己接出来"，表现成"链到头却还能多打一段"
  - `Player.prefab` 的 `combo:`（三个资产引用）换成一行 `chain:` ⇒ 这一步的 Prefab 改动只有这一处，字段名 `chain` 一旦改名，Unity 会按名反序列化失败、引用静默变 None
  - 验收 7 条：① 三连伤害仍是 10/15/25（木桩 90→75→65）② 窗口行为照旧（`Attack_Slash3` 最后 3 帧按下不接）③ 链到头狂点不再出招、停手回 `Idle` ④ **数据驱动证明**：Inspector 里清空 `Slash2.next` ⇒ 第 2 段后不再接第 3 段，改回即恢复 ⑤ 木桩节拍不变（~2.3 秒一刀）⑥ 受击/死亡照旧（12 帧硬直、木桩死时判定盒黄框、玩家 `DEAD`）⑦ 反复进出 Play 3 次一致
  - 编译验证用 `dotnet build Assembly-CSharp.csproj`（0 个错误）；EOL 核对 `PlayerCombat.cs` = `i/crlf w/crlf`（CRLF 99 / 裸 LF 0），脚本里 `comboStep|maxCombo|combo[|combo;` 残留为 0、场景里 `combo` 命中为 0
- ✅ 第 3 步：输入迁移到 `InputSystem_Actions`（删掉直读 `Mouse.current` / `Keyboard.current`）
  - 新增 `Assets/_Project/Scripts/Character/PlayerInputSource.cs`（guid `989364db6f4c1374d9ad5f5af4ff41ef`）= 唯一输入读取点：`[DefaultExecutionOrder(-100)]` + `[SerializeField] InputActionAsset actions`（指向 `Assets/InputSystem_Actions.inputactions`，guid `052faaac586de48259a63d0c4782560b`）；`Awake` 里 `FindActionMap("Player", true)` + `Move` / `Attack` / `Look`，`OnEnable` / `OnDisable` 成对 `Enable()` / `Disable()`，`Update` 只采样（`if (moveAction == null) return;` 早退），对外 `Vector2 Move` / `Vector2 Look` / `bool AttackPressed` + `ConsumeAttack()`
  - `PlayerCombat` / `PlayerMotor` 都改成从它读：攻击仍走原来的 30 帧输入缓冲（桥接语义不变，取用时 `ConsumeAttack()` 清零）；`PlayerMotor` 新增 `turnSensitivity = 0.2`（度/像素），鼠标 `Look` 的**增量**累加到 `pendingYaw`，逻辑帧里 `ApplyFacing(canMove)` 一次性消费并 `transform.rotation = Quaternion.Euler(0f, yaw, 0f)`（不要用 `Rotate` 累加，会攒误差）；`Hit` / `Dead` 期间锁转身并丢弃累积
  - `Player.prefab` 根物体新增 `PlayerInputSource` 组件，`actions` 已指向输入资产（`m_Enabled: 1`）
  - **WASD 失灵的根因（本次踩坑）**：`Player.prefab` 里 `PlayerMotor.input` 与 `PlayerCombat.input` 都是 `{fileID: 0}`，而 `PlayerMotor.Awake` 当时只自解析了 `controller` / `actor`、**漏了 `input`** ⇒ `input == null` ⇒ `moveInput` 永远是 `Vector2.zero`，**静默不动且不报错**（`PlayerCombat` 有自解析，所以鼠标攻击正常）。修法 = 补 `if (input == null) input = GetComponent<PlayerInputSource>();` + `LogError`（`PlayerMotor.Awake` / `PlayerCombat.Awake` 各一条）。**教训：同物体自解析要成套（`controller` / `actor` / `input`），缺引用必须响亮报错 —— 静默失灵最难查**
  - 顺手清理：`PlayerInputSource.cs` 是第 6 个被编辑器存成 GBK 的脚本 → 转 UTF-8 无 BOM（1626 → 1675 字节，CRLF 40 / 裸 LF 0 / U+FFFD 0）；`PlayerMotor.cs` 行尾 mixed → LF（与索引 `i/lf` 一致）；删掉 `PlayerCombat.cs` 里 IDE 自动加的 `using UnityEngine.Windows;`
  - 验收：`dotnet build Assembly-CSharp.csproj` 0 错误；用户在 Unity 里 Play 确认 WASD / 出招 / 鼠标转朝向正常（2026-10-13）

**阶段 5（进行中）** —— 防御三件套（普通防御 / GP / 闪避 · 极限闪避）

- 决策（用户 2026-10-13 全选 A）：
  - **防御键位 = 一键两用**：按下瞬间起 **8 帧**（0~0.13 s）是 GP 窗口（弹开），窗口过后仍按住 = 普通防御（减伤）
  - **GP 成功效果 = 攻方进硬直**：攻方按被弹开招式的 `hitStunFrames` 进 `Hit`，双方各一次 `RequestHitStop`，防守方回耐 **30**
  - **闪避 = 起手后前 9 帧无敌**（0.15 s），其余时间照常挨打；在「对方判定帧开始前 6 帧内」按下 ⇒ **极限闪避**（回耐 + 长顿帧 + HUD 提示）
- 先定数值（便于后面手感打磨时只调数字）：GP 窗口 **8 帧**、闪避无敌 **9 帧**、极限闪避识别窗 **6 帧**、耐力上限 **100**、回复 **25/秒**、消耗后停 **30 帧**、闪避消耗 **25**、普通防御被击掉 **10**、GP 成功回 **30**
- ✅ 第 0 步：耐力地基 `StaminaSystem`
  - 阶段 0 的 0 字节占位文件转实体（guid `779f4a23c02264f4f96adcdd02fd08fb`），并挂在 **Player 根物体**上（原先被挂成名为 `StaminaSystem` 的子物体，这一步顺手挪到根上：与 `CombatActor` / `PlayerMotor` / `PlayerCombat` / `PlayerInputSource` 同层，后续 `GuardSystem` / `DodgeSystem` 才能 `GetComponent<StaminaSystem>()` 直接取）
  - `maxStamina = 100` / `regenPerSecond = 25` / `regenDelayFrames = 30`；`TrySpend(cost)` 不够就不花、够则扣并压制回复；`Restore(amount)` 只加数字、**不动压制计时**（奖励回耐不该顺带解禁回复）
  - 回复按逻辑帧算（`regenPerSecond * CombatClock.TickDelta`）⇒ 与帧率无关，顿帧期间自动冻结；`IsExhausted` 留给阶段 10 破防用
  - 耐力**不写进 `CombatActor`**：它是"资源"不是"状态"，写进去会让 `CombatActor` 同时承担两件事
  - HUD 玩家/敌人行尾各加 `STAM xxx/100`（`CombatDebugHUD` 从已有的 `player` / `enemy` 引用 `GetComponent<StaminaSystem>()` 取，不新增手拖字段；取不到显示 `STAM n/a` = Prefab 漏挂的显式信号）
  - 暂留两个 `[ContextMenu]` 调试钩子（`Debug: 花 30 耐力` / `Debug: 回 40 耐力`），第 1 步接入真正的消耗者时删除
  - 验收：HUD 出现 `STAM 100/100`、花 30 立刻变 70 且 Console `TrySpend(30) = True，剩余 70`、之后 30 帧不动再按 25/秒回、花到 0 后 `TrySpend` 返回 `False` 且不会变负、`Debug: 回 40` 封顶 100、反复进出 Play 3 次开局满耐（用户 2026-10-13 确认通过）
- ✅ 第 1 步：普通防御（`GuardSystem` + 前方格挡盒 + `CombatResolver.ResolveGuard`）
  - 防御键进 `PlayerInputSource`：代码里新建 `InputAction("Guard")`（`<Mouse>/rightButton` + `<Keyboard>/leftShift`），不动那个 1077 行的 `.inputactions` 资产；对外只暴露 `GuardHeld` / `GuardPressed` / `ConsumeGuardPress()`
  - `GuardSystem`：`blockStaminaCost = 10` / `blockHitStopFrames = 2` / `guardDamageMultiplier = 0`；`IsGuardActive`（`Guard` 或 `GuardPoint` 状态）判定 + `TryBlock(attack)`（耐力不够就挡不住）；挡下时打 `[GuardSystem] {name} 挡下 {招式}：扣 10 耐力，剩余 90`
  - **格挡从"按状态 360° 拦截"改成"前方真实格挡盒"**（用户定制需求：要"拿武器挡住对方武器"的方向感）：`Player.prefab` 新增子物体 `GuardBox`（Layer = `Hurtbox`、`isTrigger`、位于角色前方 `z = 0.75 m`、尺寸 `1.6 × 1.6 × 1.0 m`），挂在 Player 根下 ⇒ 随 `yaw` 一起转
  - `Hurtbox` 加 `guardOnly` 标记区分"格挡盒 / 身体"（Gizmos：橙 = 格挡盒、青 = 受击盒、洋红 = owner 未接线）；`AttackHitbox` 改**两遍扫描**（先格挡盒 → `CombatResolver.ResolveGuard`，再身体，已被挡下的目标这一击不再打身体，`blockedActors` 去重）
  - `CombatResolver` 拆出 `ResolveGuard(attacker, defender, attack)`：`ResolveHit` 不再无条件吞掉"防御中的命中"，只有格挡盒相交才挡 ⇒ 绕背砍中身体照常掉血 + 硬直（方向性验收点）
  - 防御期间用 `PlayerMotor` 的 `canMove` 一起锁位移与转身（与 `Hit` / `Dead` 同一套规则）；防御中按攻击可立刻出招（不必先松手）
  - 踩坑记录：① 用户手写的 `AddBinding("<Keyboarf>/leftShift")` 拼错，Input System 会在 `Enable()` 时丢 binding ⇒ 右键防御整条失效；② "耐力没有扣除"的真因是**格挡盒没被扫到**（防御时木桩的刀都挥空了），不是扣耐力逻辑坏 —— 判定是几何相交，不是"按了键就掉耐"
  - 验收：正面按住右键 ⇒ 每挡一下 `STAM` −10 且 `HP` 不动、Console 出 `[GuardSystem]`；用鼠标把木桩转到背后仍按住右键 ⇒ **挡不住**、照常掉血进硬直；不按右键时橙框不参与判定；耐力花光后挡不住（用户 2026-10-13 确认通过）
- ✅ 第 2 步：GP 窗口 + 弹开
  - **GP 窗口 = `Guard` 状态的前 8 帧**（`actor.StateFrames < gpWindowFrames`，进入那帧为 0 ⇒ 0..7 共 8 帧）；刻意**不引入 `CombatState.GuardPoint` 状态**：窗口只是同一个状态里的一段帧区间，独立状态会连带改 `ChangeState` / `PlayerMotor` / HUD / 分派四处，行为完全一样。`GuardPoint` 留给阶段 6 一闪
  - `TryBlock` 返回类型从 `bool` 改成 `public enum GuardResult { None, Blocked, GuardPoint }`（三态：挡不住 / 普通格挡 / GP），`CombatResolver.ResolveGuard` 用 switch 分流
  - GP 效果（决策 2A）：`attacker.EnterHitStun(attack)` 让攻方按**自己这一招**的 `hitStunFrames` 进硬直（只进硬直、不掉血、不击退）+ 全场 `gpHitStopFrames = 8` 的顿帧 + 防守方 `Restore(gpStaminaReward = 30)`（奖励回耐，**不扣**耐力）
  - `CombatActor.EnterHitStun(AttackData)` 是本次新增的第二个"进硬直"入口：`ReceiveHit` = 扣血 + 击退 + 硬直，`EnterHitStun` = 只有硬直；两者都必须写 `lastHit`，否则 `TickHit` 读不到时长（硬直 0 帧，看起来像"弹开没生效"）
  - HUD 在 `Guard` 阶段区分窗口内外：前 8 帧显示 `GP-WIN frame 0..7/8`，之后 `GUARD`
  - 踩坑：用户把新旧两个 `TryBlock` 都留在文件里（只差返回类型）⇒ `CS0111` 重复成员 + 调用点 `CS0121` 歧义；"只有返回类型不同"不能当重载用，必须替换而不是新增
  - 验收：窗口内挨刀 ⇒ `GP 弹开`、STAM +30、木桩 `HIT` 被打断 + 长顿帧；窗口外 ⇒ `挡下`、STAM −10、木桩继续挥；GP 后立刻反击能命中（用户 2026-10-13 确认通过）
- ⬜ 第 3 步：闪避位移 + 无敌帧（`DodgeSystem`）
- ⬜ 第 4 步：极限闪避（`PerfectDodge` + 回耐 + 长顿帧 + HUD 提示）

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
| 命中链路的参数形态：传 `float damage` + `int hitStunFrames` vs 传整个 `AttackData` | ✅ **已定：传 `AttackData`**（阶段 3）—— 阶段 8 相杀要判断"招式身份"（轻/重/特殊），按数值传参每加一个效果都得改签名 |
| 硬直期间的移动：照常能动 vs 锁住 | ✅ **已定：锁住水平位移、重力照常**（阶段 3）—— `PlayerMotor` 读 `CombatActor.State`，`Hit` / `Dead` 时不 `Move()` |
| 顿帧时长的计量单位：渲染帧 vs 逻辑帧 | ✅ **已定：逻辑帧**（阶段 3 第 2 步）—— 按渲染帧计时会让"6 帧"在 483 fps 的机器上只有 12 ms、在 30 fps 的机器上 200 ms（16 倍漂移）；改为在 tick 循环里消费后与渲染帧率完全无关 |
| 击退的运动模型：恒速推 N 帧 vs 冲量 + 每帧衰减 | ✅ **已定：恒速推 N 帧**（阶段 3 第 3 步）—— 距离 ÷ 帧数 = 每帧步长，推完即停、帧数精确可验收；缓动曲线留到阶段 11 手感打磨，不提前复杂化 |
| 死亡瞬间是否还保留击退：尸体滑一下 vs 立刻定住 | ✅ **已定：保留击退**（阶段 3 第 4 步）—— 击退是"用截止帧号算出的属性"，与状态无关；而且人被打飞时不会瞬间悬停，尸体滑出去更自然，代价是死后还有一小段位移 |
| 由谁把状态写成 `Dead`：各控制器自己判断 vs `CombatActor.TakeDamage` 写入 | ✅ **已定：`CombatActor` 写入**（阶段 3 第 4 步）—— 纪律 #3「状态唯一来源」的必然延伸；控制器只做幂等收尾，不再各自 `if (IsDead) SetState(Dead)` |
| 对尸体挥刀：算命中 vs 跳过 | ✅ **已定：跳过**（阶段 3 第 4 步）—— 尸体不该喂招（否则阶段 4 的连招能靠尸体续段），且不给顿帧/伤害；`AttackHitbox` 里跳过 `Owner.IsDead` 的 `Hurtbox`，`CombatResolver` 的 `IsDead` 守卫留作双保险 |
| 连招窗口的定义：派生 `[判定结束, 收招结束 − N)` vs 显式绝对帧字段 vs 保持"整段收招" | ✅ **已定：派生 + 提前关窗**（阶段 4 第 0 步）—— 只加一个默认 0 的字段，改 `recoveryFrames` 时窗口自动跟着走，不会留下对不上的死数据 |
| 连招窗口内的行为：取消剩余收招（真 cancel）vs 等收招播完再切（窗口只做输入门） | ✅ **已定：取消剩余收招**（阶段 4 第 0 步）—— 收招是玩家的"责任时间"，接得上就有节奏、接不上就断；代价是连招总时长短于"收招全播 ×3"，阶段 8 的帧数优势窗口要按新规则重算 |
| 连招接续关系：线性 `comboStep` 顺序 vs `ComboChain` SO（每段声明允许接什么）vs 完整连招图 | ✅ **已定：`ComboChain` SO**（阶段 4 第 2 步）—— 先保住线性 1→2→3 的数据形态，同时为分支（第 2 段后接重攻击/终结技）留位置；完整图等到有气刃与 GP 之后再谈 |
| 共享招式播放器 `CombatController` 的抽取时机：本阶段 vs 阶段 5（GP）vs 不抽 | ✅ **已定：阶段 4 第 1 步就抽**（决策 3A）—— 现在两份状态机几乎相同，越晚抽越贵，且 `PlayerCombat` / `EnemyCombat` 容易行为漂移 |
| 链的数据形态：每段只写"下一个" vs 节点 + 候选列表 vs 直接上完整连招图 | ✅ **已定：节点 + `List<AttackData> next`**（阶段 4 第 2 步）—— 现在只有一个攻击键 ⇒ 取候选第 0 项，等价线性 1→2→3；将来按输入（前/后/重击）索引候选即可分支，数据结构不用再改 |
| 从 `combo` 数组迁移到 `ComboChain`：两套并存过渡 vs 直接替换 | ✅ **已定：直接替换**（阶段 4 第 2 步）—— 一次删净 `maxCombo` / `combo[comboStep-1]` / `comboStep`，只重接一次 Prefab 线；两套连招逻辑并存必然相互漂移 |
| 输入读取的落点：各脚本各自读 `Keyboard.current` / `Mouse.current` vs 单一 `PlayerInputSource` 组件 | ✅ **已定：单一 `PlayerInputSource` 组件**（阶段 4 第 3 步）—— 开关（`Enable` / `Disable`）只有一处、空引用兜底只有一处；将来 GP / 闪避 / 一闪的按键都在这里加一行，不必满工程找 `Keyboard.current` |
| 攻击输入的桥接方式：Action 状态直接喂状态机 vs 采样后转成本地输入缓冲 | ✅ **已定：保持输入缓冲**（阶段 4 第 3 步）—— 采样组件只暴露"这一渲染帧按下过"，`PlayerCombat` 立刻转成 30 帧缓冲，手感与阶段 1–3 完全一致；缓冲是"逻辑侧记忆"，不该由输入层持有 |
| 鼠标 `Look` 的用途：这一步只控角色朝向 vs 顺便接管相机 | ✅ **已定：先只控角色朝向**（阶段 4 第 3 步）—— `Look` 增量驱动 `yaw`，判定盒与击退方向随朝向走；相机保持固定视角，完整的第三人称相机（跟随 + 角色朝移动方向）留单独一步，避免一次改两套坐标系 |
| 防御键位：攻击/防御分两个键 vs 一键两用（前 8 帧 GP、按住普通防御） | ✅ **已定：一键两用**（阶段 5，决策 A）—— 少一个键位、更接近"抓到格挡点"的手感；代价是"按住不放"与"踩点按下"要在表现上区分（盾光 / 音效），且玩家可能一直按住吃减伤 |
| GP 成功的效果：只弹开（双方都不断） vs 攻方进硬直 + 双方顿帧 + 防守方回耐 | ✅ **已定：攻方进硬直 + 双方顿帧 + 回耐**（阶段 5，决策 A）—— GP 的收益必须"看得见、能接后续"（攻方硬直窗口就是反击窗口，也是阶段 6 一闪的入口）；代价是攻方在硬直里更容易被连招 |
| 闪避的无敌与极限闪避判定：全程无敌 vs 只前 9 帧无敌；按"我按得早/晚" vs 按"对方判定帧前 N 帧" | ✅ **已定：前 9 帧无敌 + 对方判定帧开始前 6 帧内算极限闪避**（阶段 5，决策 A）—— 无敌帧有限才有择时压力；用"对方判定帧"而不是"我按键的时刻"来判定，才不受输入延迟与帧率影响 |
| 格挡的判定语义：按状态 360° 拦截 vs 前方真实格挡盒（碰撞体） | ✅ **已定：前方格挡盒**（阶段 5 第 1 步，用户定制）—— 按状态拦截简单，但"背后砍你也挡住"没有方向感；改成 `GuardBox` 子物体后"武器挡住武器"是几何事实。代价：格挡范围变成要调的手感参数（Inspector 改 `BoxCollider` 的 Size / `z`），且绕背或挥空时挡不住是**预期行为** |
| 格挡盒与身体的结算顺序：一遍扫描 vs 两遍扫描 | ✅ **已定：两遍扫描（先格挡盒、再身体）**（阶段 5 第 1 步）—— 同一挥击里两个盒子可能同时相交，先算格挡再用 `blockedActors` 跳过身体，结果与 `OverlapBox` 返回顺序无关；写成一遍会抖动（同一刀有时挡下、有时被打破防） |
| 击退方向：攻击者正前方 vs 攻击者→受击者连线 | ✅ **已定：攻击者正前方**（阶段 3 第 3 步）—— 侧面打也沿朝向推，连招落点可预测，且不会在两人重合时出现零向量退化 |
| 木桩的移动方式：加 `CharacterController` + `EnemyMotor` vs 直接改 `transform.position` | ✅ **已定：加 CharacterController**（阶段 3 第 3 步）—— 与玩家共用同一套移动规则、有碰撞、有重力，将来给木桩加走位不必重做；代价是 Prefab 要删掉根上原来的 CapsuleCollider |
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
| 「攻击」输入仍直读 `Mouse.current`，未使用 `InputSystem_Actions` 的 `Attack` Action | ✅ 已解决（阶段 4 第 3 步：全部输入收进 `PlayerInputSource`，脚本里已无 `Keyboard.current` / `Mouse.current`） |
| `AttackData.cs` 的 `menuName` 拼写为 `ComboHit/AttackDate` | ✅ 已解决（阶段 3 第 2 步：改为 `ComboHit/AttackData`） |
| 脚本被编辑器存成 GBK（`AttackData.cs` 加中文 `Tooltip` 后成为第 4 个 GBK 文件，`read` 报 invalid UTF-8） | ✅ 已解决（阶段 3 第 2 步：转 UTF-8 无 BOM；发现手段 = `read` 工具直接报错 / 严格 UTF-8 解码校验） |
| 木桩 Prefab 沿用 `Enemy` 命名，与规划里的 `TrainingDummy` 不一致 | 4 |
| 无 `.asmdef` 分层；`Scripts/Input` 仍为空目录 | 1–4 |
| 项目内行尾混用（`PlayerMotor.cs` 全 LF、`CombatActor.cs` 前 17 行 LF 之后 CRLF、其余 CRLF），建议加 `.gitattributes` 统一 | 任意（2026-10-12 踩坑：按"统一 CRLF"重写产生 51 行 / 126 行**假 diff**；改文件前先 `git ls-files --eol`，按各文件自身行尾写，禁止全局归一化） |
| `TutorialInfo/`、`Readme.asset` 为模板残留 | 任意 |
| 招式资产缺 `hitStunFrames` 字段（运行期会取 C# 初始值，设计值与实际值可能不一致） | ✅ 已解决（阶段 3 第 1 步：4 个资产实填 10 / 14 / 20 / 12） |
| `EnemyMotor.cs` 与 `PlayerMotor.cs` 是两份几乎相同的代码（重力 + 击退） | 4–7（等木桩需要走位 / 冲刺时再抽公共基类或独立 `Motor` 组件，过早抽象会把两者不同的输入来源硬拧到一起） |
| 连招改为**取消语义**后，阶段 3 记录过的"帧数优势窗口"（例：Slash3 收招 21 帧 vs 硬直 20 帧）不再直接成立 | 8（阶段 4 第 0 步引入：收招可被取消 ⇒ 实际连段总时长变短，相杀/GP 的帧数比较要按新规则重算） |
| 同物体引用自解析不成套（`controller` / `actor` / `input` 三类，漏一个就**静默失灵**） | ✅ 已解决（阶段 4 第 3 步：`PlayerMotor` 补 `input` 自解析 + `PlayerMotor` / `PlayerCombat` 各加一条 `LogError`；教训：缺引用必须响亮报错，否则表现成"按键没反应"） |
| `StaminaSystem` 的两个 `[ContextMenu]` 调试钩子（`Debug: 花 30 耐力` / `Debug: 回 40 耐力`）是临时脚手架 | 5（留到阶段 5 收尾——第 2/3 步验"GP / 极限闪避成功回耐 30"时还要靠它造耐力，收尾时删除） |

## 7. 变更记录

- **2026-10-13 · 阶段 5 第 2 步（GP 窗口 + 弹开）**
  - GP 用"`Guard` 状态的前 8 帧"表达（`StateFrames < gpWindowFrames`），不新增状态；`GuardPoint` 枚举值留给阶段 6 一闪
  - `GuardSystem.TryBlock` 返回 `GuardResult`（`None` / `Blocked` / `GuardPoint`）：窗口内 = 回 30 耐力 + 弹开攻方，窗口外 = 扣 10 耐力 + 普通格挡
  - GP 弹开时 `CombatActor.EnterHitStun(attack)` 只让攻方进硬直（不扣血、不击退），硬直时长取攻方"自己这一招"的 `hitStunFrames` —— 用重招砸 GP 的人被弹得更久，规则自洽
  - 长顿帧 `gpHitStopFrames = 8`（≈133 ms）对比普通格挡的 2 帧，打击反馈的差别靠顿帧长度直接体现
  - 踩坑：`TryBlock` 两版并存（只差返回类型）→ `CS0111` + `CS0121`；同名不同返回类型不是重载
  - 验收：窗口内 `GP 弹开` + STAM +30 + 木桩 `HIT`；窗口外 `挡下` −10 且木桩继续挥；GP 后反击命中（用户确认通过）

- **2026-10-13 · 阶段 5 第 1 步（普通防御 + 前方格挡盒）**
  - 防御从"按状态无条件拦截"变成**几何判定**：`Player.prefab` 新增 `GuardBox` 子物体（Layer `Hurtbox`、trigger、前方 `z = 0.75 m`、`1.6 × 1.6 × 1.0 m`），挂在 Player 根下随 `yaw` 旋转 ⇒ 只有对方的攻击盒与它相交才算挡住，"武器挡武器"是看得见的事实
  - `AttackHitbox` 改**两遍扫描**（先格挡盒 → `CombatResolver.ResolveGuard`，再身体；`blockedActors` 保证一刀只结算一个结果）；`Hurtbox` 加 `guardOnly` 标记与三色 Gizmos（橙 = 格挡盒 / 青 = 受击盒 / 洋红 = owner 未接线）
  - `GuardSystem`：`blockStaminaCost = 10`、`blockHitStopFrames = 2`，`TryBlock` 靠 `StaminaSystem.TrySpend` 决定挡不挡得住 ⇒ "耐力耗尽就挡不住"是同一处代码，不用第二套逻辑
  - 防御键在 `PlayerInputSource` 里用代码新建 `InputAction("Guard")`（右键 + 左 Shift），不动 `.inputactions` 资产；踩坑：`<Keyboarf>` 拼错会让 Input System 在 `Enable()` 时丢 binding，表现为"按键完全没反应"
  - 排错教训：用户报"耐力没有扣除"，日志显示 `进入 Guard` 10 次但 `[GuardSystem]` 0 次、玩家只挨过一次打 ⇒ **格挡是几何相交，不是"按了键就掉耐"**；给 `ResolveGuard` 加了"有格挡盒却没有 `GuardSystem`"的响亮报错，避免同类静默失效
  - 防御期间锁位移与转身（`PlayerMotor` 的 `canMove` 与 `Hit` / `Dead` 同规则）；防御中按攻击可立刻出招
  - 验收：正面挡下扣 10 耐力、绕背挡不住、耐力光后挡不住（用户 2026-10-13 确认通过）；`dotnet build` 0 错误

- **2026-10-13 · 阶段 5 第 0 步（`StaminaSystem` 耐力地基 + HUD 显示）**
  - 耐力从"路线图里的数字"变成可花可回的组件：`StaminaSystem`（`TrySpend` / `Restore` / `IsExhausted`），回复按逻辑帧算、消耗后停 30 帧
  - `Restore` 刻意**不动压制计时**：GP / 极限闪避的"回耐奖励"是奖励，不该顺带解除刚挨打后的回复惩罚（否则奖励越强、惩罚越形同虚设）
  - `Player.prefab`：把 `StaminaSystem` 从子物体挪到 **Player 根物体**（与 `CombatActor` / `PlayerMotor` / `PlayerCombat` / `PlayerInputSource` 同层）⇒ 后续 `GuardSystem` / `DodgeSystem` 用 `GetComponent` 就能取到，不必 `GetComponentInChildren`；校验 18 个文档块、0 个悬空引用
  - HUD 玩家/敌人行尾加 `STAM xxx/100`，从已有引用 `GetComponent<StaminaSystem>()` 取（不新增手拖字段），取不到显示 `STAM n/a` 当"Prefab 漏挂"的显式信号
  - 顺手：`StaminaSystem.cs` 是第 7 个被编辑器存成 GBK 的脚本 → 转 UTF-8 无 BOM；`CombatDebugHUD.cs` 行尾归一 CRLF；`dotnet build Assembly-CSharp.csproj` 0 错误

- **2026-10-13 · 阶段 4 第 3 步（输入迁移到 `InputSystem_Actions`）· 阶段 4 完成**
  - 输入从"各脚本自己读设备"收敛成一个组件：新增 `PlayerInputSource`（`Player` map 的 `Move` / `Attack` / `Look`），`Update`（order -100）只采样、逻辑帧消费；`PlayerCombat` 保留 30 帧输入缓冲（手感与阶段 1–3 一致），`PlayerMotor` 用鼠标 `Look` 的增量驱动 `yaw`（`turnSensitivity = 0.2` 度/像素），判定盒与击退方向随朝向走
  - 踩坑：`PlayerMotor.input` 在 Prefab 里是空引用（`{fileID: 0}`），而 `Awake` 只自解析了 `controller` / `actor` ⇒ WASD **静默失灵**（`PlayerCombat` 有自解析，所以鼠标攻击正常）；两处补上同物体自解析 + `LogError`
  - 顺手：第 6 个被编辑器存成 GBK 的脚本转 UTF-8 无 BOM、`PlayerMotor.cs` 行尾归一 LF、删掉 `PlayerCombat.cs` 里 IDE 自动加的 `using UnityEngine.Windows;`
  - 阶段 4（连招系统）至此全部落地：连招窗口取消语义 → 共享 `CombatController` → `ComboChain` SO → 输入迁移；技术债「攻击输入直读 `Mouse.current`」结清
- **2026-10-13 · 阶段 4 第 2 步（`ComboChain` SO）**
  - 连招从"代码里的数组 + 下标"变成"数据资产里的链"：新增 `ComboChain.cs`（`entry` + `nodes`，每段声明 `next` 候选列表）与 `ComboChain_Player.asset`（`Slash1 → Slash2 → Slash3`），`PlayerCombat` 只记 `currentNode` / `chainDepth`
  - `OnValidate` 六条自检（entry 缺失/不在表、空招式、空边、自环、死边）+ 运行期 `maxChainDepth = 16` 兜底 ⇒ "数据写环导致无限连"这类错误在编辑器里就被拦下
  - `comboStep`（下标）、`maxCombo`、`AttackData[] combo` 全部删除；`Player.prefab` 只改一行 `combo:` → `chain:`
  - 这一步的价值不在玩法（行为与第 1 步逐帧一致）而在**数据形态**：分支连招（第 2 段后接重攻击/终结技）以后只是往 `next` 里加候选，不用碰代码
  - 同时提交一次 Unity 自己的重新序列化（`DefaultVolumeProfile.asset` 多出 `filter` 字段、`URPProjectSettings.asset` 多出 `m_ProjectSettingFolderPath`）—— 与阶段 4 第 1 步引入的 URP 版本升级有关，与游戏逻辑无关

- **2026-10-13 · 阶段 4 第 1 步（抽共享 `CombatController`）**
  - 新增 `CombatController` 基类（共享招式播放器），`PlayerCombat` / `EnemyCombat` 变薄壳：状态机只剩一份，阶段 5/6/8 的新机制以后只在一处加
  - 字段名 `actor` / `attackHitbox` 原样保留 ⇒ 两个 Prefab 与场景一行都不用动（`git diff` 里没有 Prefab / 场景 = 引用未断的硬证据）
  - 补回重构时丢掉的 `PlayerCombat` combo 数组校验；`CombatController.cs` GBK → UTF-8 无 BOM
  - 新增"不必等 Unity 焦点"的编译验证手法：Unity 自带 `dotnet build Assembly-CSharp.csproj`（0 个错误，2.2 秒）
  - 另记：推送开始报 `SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)` —— 本机根证书库里有 Fiddler / 游戏加速器的私有根（`DO_NOT_TRUST_FiddlerRoot`、`XunYouRootCA`、`Leigod CA`…）且 `clash-verge` 在跑，TLS 被中间人替换了签发者；解法是 `-c http.sslCAInfo=<Git 公共 CA + Windows 证书库合并的 bundle>`（生成在 gitignore 的 `Logs/ca-bundle-os.crt`），**没有关闭证书校验**

- **2026-10-12 · 阶段 4 第 0 步（连招窗口 + 取消语义）**
  - `AttackData.comboWindowCloseEarly`（默认 0）+ `ComboWindowCloseFrame` 派生属性 + `OnValidate` 夹 ≥0；`Attack_Slash3` 实填 3，其余取默认 0
  - `PlayerCombat.UpdateRecovery` 改为取消语义：窗口内按键立刻取消剩余收招接下一段；窗口关闭后丢弃缓冲（否则关窗前按下的键会在收招末帧"自己接出来"）
  - 哨兵统一为 `comboStep = 0` = 无连招进行中（原先死亡收尾写 1、受击/终止写 0）
  - 修正阶段 3 的一条认知：旧实现要等整段收招播完才消费缓冲（不是收招第 0 帧），收招帧数一直在起作用
  - 验收 6 条：连打总时长明显短于"收招全播 ×3" / 单按一次收招一帧不少 / 收招最后 3 帧按键不接 / 起手帧预输入在收招第 1 帧接出 / 狂点 10 次只出 1→2→3 不跳段不重复 / 停手回 `IDLE` 且下一次从 Slash1 起手

- **2026-10-12 · 阶段 3 第 4 步（生死）—— 阶段 3 完成**
  - `CombatActor.TakeDamage`：HP 归零时自己 `SetState(CombatState.Dead)`（决策 2A）；`EnemyCombat` / `PlayerCombat` 的死亡收尾转为正式路径并保持每帧幂等（收刀 + 清输入缓冲与 `comboStep`）
  - 修掉"尸体继续打人"：`EnemyCombat` 死亡分支原来只置状态没收刀；`AttackHitbox` 现在跳过 `Owner.IsDead` 的 `Hurtbox`（决策 3A）
  - `EnemyMotor.cs` / `.meta` 移到 `Scripts/Character/`（guid 不变，`Enemy.prefab` 引用不受影响；git 识别为 `R100`）
  - 修掉一处坏粘贴：死亡分支被贴进 `EnemyMotor.OnDisable()`，`return` 挡掉退订（禁用 Domain Reload 下会重复订阅）+ `error CS0103`；并清掉 `Enemy.prefab` 里为该字段序列化出的 `attackHitbox:` 行
  - 提交 `f5b5388`「feat: 阶段 3 第 4 步 —— 生死（Dead 状态 / 死后关判定 / 尸体不算命中）」；验收第 ② 条由用户确认（打死木桩后判定盒黄框）

- **2026-10-12 · 阶段 3 第 3 步（击退 + `EnemyMotor`）**
  - `AttackData` 加 `knockbackDistance` / `knockbackFrames`（4 个资产实填 0.4/8 · 0.7/10 · 1.4/14 · 0.5/10）；`CombatResolver` 算击退方向并 `defender.ReceiveHit(attack, knockbackDir)`；`CombatActor` 用 `knockbackEndFrame` 截止帧号 + `KnockbackVelocity` 差值属性（顿帧自动冻结）；`PlayerMotor` 新增 `ApplyKnockback()` 并放在输入锁之外；新建 `EnemyMotor.cs`
  - `Enemy.prefab` 根加 `CharacterController`（Height 2 / Radius 0.5 / Center (0,0,0)）+ `EnemyMotor`，并**删掉根 CapsuleCollider**（否则两套碰撞体重叠）
  - 修掉编译阻塞：`CombatActor.cs` 里残留的半截行（`error CS1002: ; expected`）
  - 顺手清理：`PlayerMotor.cs` 行尾 w/mixed → LF；`PlayerCombat.cs` 删掉又冒出来的无用 `using UnityEngine.InputSystem.XR;`
  - 三次决策落定（见 §5）：恒速推 N 帧 / 沿攻击者正前方 / 木桩用 CharacterController
  - 验收 6 条已由用户确认通过（2026-10-12）
- **2026-10-12 · 阶段 3 第 2 步（顿帧接命中）**
  - `AttackData.hitStopFrames`（`OnValidate` 夹 ≥ 0）+ 4 个资产实填 4 / 6 / 10 / 6；`CombatResolver.ResolveHit` 调 `CombatClock.RequestHitStop(attack.hitStopFrames)`（顿帧属于"这次命中"，不属于任何一方）
  - **修掉计时单位 bug**：`CombatClock` 的冻结从"`Update` 里每渲染帧扣 1"改为"在 tick 循环里按逻辑帧消费" —— 原实现下本机 483 fps 时 6 帧顿帧只有 12 ms（16 倍漂移），改后与渲染帧率无关
  - `AttackData.cs` 由 GBK 转 UTF-8 无 BOM（第 4 个 GBK 文件，来源：编辑器加中文 `Tooltip` 时按 GBK 保存）；顺带修 `menuName` 拼写 `ComboHit/AttackDate` → `ComboHit/AttackData`
  - HUD 的 `enemy` 引用已按纪律 #9 重新从 **Hierarchy** 接线：场景里现在是本地 stripped ID `&3851966603847174561` + 对应 stripped 块（`m_CorrespondingSourceObject` 指向 `Enemy.prefab` 内的 `EnemyCombat`），不再指向 Prefab 资产
  - 待验收：6 条手感项，含"把 `Attack_Enemy.hitStopFrames` 临时改 60 → 整整 1 秒全场入定"的判定实验
- **2026-10-12 · 阶段 3 第 1 步完成（受击硬直）**
  - `AttackData.hitStunFrames`（4 个资产实填 10 / 14 / 20 / 12）+ `CombatActor.ReceiveHit(AttackData)` / `LastHit` + `CombatResolver.ResolveHit(attacker, defender, attack)`（改传整个招式资产）+ `AttackHitbox.Activate(AttackData)`
  - 两个控制器新增 `Hit` 分支 `UpdateHit()`（每帧幂等收刀 / 清输入缓冲 / 清 `comboStep`）；`PlayerMotor` 在 `Hit` / `Dead` 期间锁水平位移；`CombatDebugHUD` 显示 `HIT` 阶段并改显示"打中我的那一招"
  - 新增死亡兜底：致命伤不进 `Hit`（`ReceiveHit` 提前 `return`），`Dead` 由控制器每 tick 兜底切换
  - **踩坑（引用）**：HUD 的 `enemy` 字段被拖成了 Prefab 资产 —— 场景 YAML 里写成 `{fileID: 3539952178830150286, guid: a12043e7604ae9e468cfb126eb647575, type: 3}`；而正确指向场景实例的 `player` 写作 `{fileID: 55196461}`（场景本地 stripped ID）。**判据：带 Prefab guid 的引用 = 资产模板，不是场景实例**；已撤回该行，回到 `Awake` 自动查找
  - **踩坑（编译日志）**：`Logs/Editor.log` 是追加式、`error CS` 是**累计**计数；只看最后一批才不会把已修好的旧错当现行错（本次真错 = `EnemyCombat.cs(60,35)` / `PlayerCombat.cs(157,35)` 的 `CS1503: cannot convert from 'float' to 'AttackData'`）
  - **踩坑（行尾二）**：pwsh here-string 插入的块是 LF，把 `EnemyCombat.cs` / `CombatDebugHUD.cs` / `PlayerCombat.cs` 变成 mixed，diff 被放大；按各文件索引行尾归一后收敛
  - **踩坑（终端中文）**：`Get-Content` 把 UTF-8 中文写到 stdout 会显示乱码（看着像文件损坏），判断文件内容一律用 read 工具
  - 待验证：死亡兜底尚未经过一次重编译
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
