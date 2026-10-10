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

    public void SetState(CombatState newState)
    {
        state = newState;
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
