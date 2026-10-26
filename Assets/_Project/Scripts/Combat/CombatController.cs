using UnityEngine;

// 招式播放器：谁在打、打哪一招由子类决定；"一招怎么播"全部在这里
public class CombatController : MonoBehaviour
{
    // 字段名必须与原子类完全一致（actor / attackHitbox），否则 Prefab 里的引用会静默变成 None
    [SerializeField] protected CombatActor actor;
    [SerializeField] protected AttackHitbox attackHitbox;

    protected AttackData currentAttack;

    // ===== 给 HUD 与子类读的只读快照 =====
    public CombatActor Actor => actor;
    public AttackData CurrentAttack => currentAttack;
    public CombatState State => (actor != null) ? actor.State : CombatState.Idle;
    public int ElapsedFrames => (actor != null) ? actor.StateFrames : 0;

    protected virtual void Awake()
    {
        // 缺失引用属于配置错误：启动就响亮地失败，别等按键才抛 NullReference
        if (actor == null) actor = GetComponent<CombatActor>();
        if (attackHitbox == null) attackHitbox = GetComponentInChildren<AttackHitbox>();

        if (actor == null) { Debug.LogError($"[{GetType().Name}] actor 未接线", this); enabled = false; }
        if (attackHitbox == null) { Debug.LogError($"[{GetType().Name}] attackHitbox 未接线", this); enabled = false; }
    }

    private void OnEnable() { CombatClock.OnTick += OnCombatTick; }
    private void OnDisable() { CombatClock.OnTick -= OnCombatTick; }   // 必须成对

    private void OnCombatTick(int frame)
    {
        if (actor == null) return;
        OnPreTick();                       // 子类每 tick 的准备工作（玩家在这里消费输入缓冲）

        // 死亡收尾（幂等）：CombatActor.TakeDamage 已经把状态写成 Dead，这里只收刀 + 清残留
        if (actor.IsDead)
        {
            attackHitbox.Deactivate();
            ClearPendingInput();
            if (actor.State != CombatState.Dead) actor.SetState(CombatState.Dead);
            return;
        }

        switch (actor.State)
        {
            case CombatState.Attack: TickAttack(); break;
            case CombatState.Recovery: TickRecovery(); break;
            case CombatState.Hit: TickHit(); break;
            case CombatState.Guard: TickGuard(); break;
            default: OnIdleTick(); break;   // Idle（以及将来别的空闲态）交给子类
        }
    }

    // ===== 子类挂钩点 =====
    protected virtual void OnPreTick() { }
    protected virtual void OnIdleTick() { }                 // 玩家：查输入；木桩：数节拍
    protected virtual void TickGuard() { }                  // 玩家：松手就回 Idle；木桩永远不进这个状态
    protected virtual void OnComboEnded() { }               // 回到 Idle 前（玩家：清空当前链节点）
    protected virtual void ClearPendingInput() { }          // 被打断/死亡时清输入（玩家覆盖，木桩空实现）

    // ===== 一招的播放（子类不再各写一遍）=====
    protected void StartAttack(AttackData attack)
    {
        currentAttack = attack;
        ChangeState(CombatState.Attack);      // 内部走 actor.SetState，计时由 StateFrames 派生
    }

    private void TickAttack()
    {
        if (actor.StateFrames == currentAttack.ActiveStartFrames)
        {
            attackHitbox.Activate(currentAttack);
            Debug.Log($"进入 Active（第 {actor.StateFrames} 帧）");
        }
        if (actor.StateFrames >= currentAttack.ActiveEndFrames)
        {
            attackHitbox.Deactivate();
            ChangeState(CombatState.Recovery);
        }
    }

    // 默认语义：收招播完 → Idle。玩家会覆盖它，在收招里插入连招窗口
    protected virtual void TickRecovery()
    {
        if (actor.StateFrames >= currentAttack.recoveryFrames)
        {
            OnComboEnded();
            ClearPendingInput();
            ChangeState(CombatState.Idle);
        }
    }

    private void TickHit()
    {
        attackHitbox.Deactivate();
        ClearPendingInput();

        AttackData hit = actor.LastHit;
        int stun = (hit != null) ? hit.hitStunFrames : 0;
        if (actor.StateFrames >= stun) ChangeState(CombatState.Idle);
    }

    protected void ChangeState(CombatState next)
    {
        actor.SetState(next);
        switch (next)
        {
            case CombatState.Idle: 
                Debug.Log("进入 Idle"); 
                break;
            case CombatState.Attack:
                attackHitbox.Deactivate();      // 保险：进入攻击时判定体必须是关的
                Debug.Log($"进入攻击：{currentAttack.attackName}（起手 {currentAttack.startupFrames} 帧）");
                break;
            case CombatState.Recovery:
                Debug.Log($"进入 Recovery（收招 {currentAttack.recoveryFrames} 帧）");
                break;
            case CombatState.Guard:
                Debug.Log("进入 Guard");     // 不加这条，每次防御都会刷"未处理的状态迁移"
                break;
            default:
                Debug.LogWarning($"[{GetType().Name}] 未处理的状态迁移：{next}", this);
                break;
        }
    }
}