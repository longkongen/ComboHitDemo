using UnityEngine;

public class CombatActor : MonoBehaviour
{
    // 状态是“角色的数据”，由 CombatActor 独占持有：
    // 外部只能读 State，只能通过 SetState 写（将来非法迁移的拦截也在这里做）
    [SerializeField] private CombatState state = CombatState.Idle;
    public float hp = 100f;

    public CombatState State => state;

    public void SetState(CombatState newState)
    {
        state = newState;
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
