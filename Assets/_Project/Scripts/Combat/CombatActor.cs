using UnityEngine;

public class CombatActor : MonoBehaviour
{
    // 状态是“角色的数据”，由 CombatActor 独占持有：
    // 外部只能读 State，只能通过 SetState 写（将来非法迁移的拦截也在这里做）
    [SerializeField] private CombatState state = CombatState.Idle;
    [SerializeField] private float hp = 100f;
    public float Hp => hp;
    public bool IsDead => hp <= 0f;

    // 可选：普通防御组件。木桩没有 GuardSystem，所以允许为空（这里绝不 LogError），
    // 用惰性自解析而不是 Awake，是因为 CombatActor 目前没有 Awake，也不想为一个可选项加一条生命周期。
    [SerializeField] private GuardSystem guard;
    public GuardSystem Guard
    {
        get
        {
            if (guard == null) guard = GetComponent<GuardSystem>();
            return guard;
        }
    }

    public CombatState State => state;
    private AttackData lastHit;
    public AttackData LastHit => lastHit;
    // 进入当前状态时的逻辑帧号；StateFrames 是“读出来的差值”，不是自增计数。
    // 为什么不用“订阅 OnTick 自己 ++”：同一帧里谁先被通知取决于组件顺序，
    // 一旦先后颠倒，整条帧数表就会错一帧；差值式没有这个问题（同一逻辑帧内 Frame 恒定）。
    private int stateEnteredFrame;

    /// <summary>当前状态已经持续了几个逻辑帧（刚进入的那一帧为 0）。</summary>
    public int StateFrames => CombatClock.Frame - stateEnteredFrame;

    private Vector3 knockbackVelocity;      // 米/秒
    private int knockbackEndFrame;

    public Vector3 KnockbackVelocity =>
        (CombatClock.Frame < knockbackEndFrame) ? knockbackVelocity : Vector3.zero;

    
    public void SetState(CombatState newState)
    {
        state = newState;
        stateEnteredFrame = CombatClock.Frame;   // 唯一的计时复位点
    }
    public void TakeDamage(float damage)
    {
        if (IsDead)
        {
            
            return;
        }
        if (damage <= 0f)
        {
            return;
        }
        hp -= damage;
        if (hp <= 0f)
        {
            hp = 0f;
            Debug.Log($"[CombatActor] {name} 被击倒（HP 0）", this);
            SetState(CombatState.Dead);   // 决策 2A：死亡由状态拥有者写入，各控制器只做幂等收尾
        }
        else 
        {
            Debug.Log($"[CombatActor] {name} 受到 {damage} 点伤害，剩余 HP {hp}", this);
        }



    }
    /// <summary>
    /// 被命中：扣血 + 进入硬直。硬直时长由“打中我的那一招”决定
    /// </summary>
    public void ReceiveHit(AttackData attack, Vector3 knockbackDirection)
    {
        if (IsDead) return;
        if (attack == null) return;
        TakeDamage(attack.damage);
        if (IsDead) return;
        lastHit = attack;
        if (attack.knockbackFrames > 0 && attack.knockbackDistance > 0f)
        {
            Vector3 dir = knockbackDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
                knockbackVelocity = dir * (attack.knockbackDistance / (attack.knockbackFrames * CombatClock.TickDelta));
                knockbackEndFrame = CombatClock.Frame + attack.knockbackFrames;
            }
        }
        SetState(CombatState.Hit);
        Debug.Log($"[CombatActor] {name} 进入硬直：{attack.attackName}（{attack.hitStunFrames} 帧）", this);
    }
    public void EnterHitStun(AttackData source)
    {
        if (IsDead || source == null) return;
        lastHit = source;
        SetState(CombatState.Hit);
        Debug.Log($"[CombatActor] {name} 被弹开：{source.attackName}（{source.hitStunFrames} 帧硬直）");
    }
}
