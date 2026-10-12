using UnityEngine;

public class CombatActor : MonoBehaviour
{
    // 状态是“角色的数据”，由 CombatActor 独占持有：
    // 外部只能读 State，只能通过 SetState 写（将来非法迁移的拦截也在这里做）
    [SerializeField] private CombatState state = CombatState.Idle;
    [SerializeField] private float hp = 100f;
    public float Hp => hp;
    public bool IsDead => hp <= 0f;

    public CombatState State => state;

    // 进入当前状态时的逻辑帧号；StateFrames 是“读出来的差值”，不是自增计数。
    // 为什么不用“订阅 OnTick 自己 ++”：同一帧里谁先被通知取决于组件顺序，
    // 一旦先后颠倒，整条帧数表就会错一帧；差值式没有这个问题（同一逻辑帧内 Frame 恒定）。
    private int stateEnteredFrame;

    /// <summary>当前状态已经持续了几个逻辑帧（刚进入的那一帧为 0）。</summary>
    public int StateFrames => CombatClock.Frame - stateEnteredFrame;

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
        }
        else 
        {
            Debug.Log($"[CombatActor] {name} 受到 {damage} 点伤害，剩余 HP {hp}", this);
        }



    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
