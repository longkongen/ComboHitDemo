using UnityEngine;

// 普通防御：参数与"这一击能不能挡下"的判定都在这里。
// 状态迁移仍由 CombatController / PlayerCombat 写（纪律 #3：状态的唯一来源是 CombatActor），
// 这里只回答一个问题：这一击挡不挡得住。
public class GuardSystem : MonoBehaviour
{
    [SerializeField] private float blockStaminaCost = 10f;      // 每次挡下扣多少耐力
    [SerializeField] private int blockHitStopFrames = 2;        // 挡下时全场的小顿帧
    [SerializeField] private float guardDamageMultiplier = 0f;  // 0 = 完全挡住（阶段 5 决策 A）

    private CombatActor actor;
    private StaminaSystem stamina;

    public float BlockStaminaCost => blockStaminaCost;
    public int BlockHitStopFrames => blockHitStopFrames;
    public float GuardDamageMultiplier => guardDamageMultiplier;

    // Guard = 普通防御；GuardPoint 是阶段 5 第 2 步的 GP 窗口（先一起认，第 2 步不必再改这里）
    public bool IsGuardActive => (actor != null)
        && (actor.State == CombatState.Guard || actor.State == CombatState.GuardPoint);

    // 只读转发：方便将来 HUD / 表现层直接问"还有多少耐力可以挡"
    public float Stamina => (stamina != null) ? stamina.Stamina : 0f;

    private void Awake()
    {
        // 同物体自解析：引用不随 Prefab 漂移（纪律 #9 的本意），不必手拖
        actor = GetComponent<CombatActor>();
        stamina = GetComponent<StaminaSystem>();

        if (actor == null) Debug.LogError("[GuardSystem] 缺 CombatActor", this);
        if (stamina == null) Debug.LogError("[GuardSystem] 缺 StaminaSystem -> 防御不会消耗耐力（等于无限格挡）", this);
    }

    // 尝试挡下这一击：付得起耐力才算挡住（决策 A）
    public bool TryBlock(AttackData attack)
    {
        if (!IsGuardActive) return false;

        // 没有耐力系统时退化成"无限格挡"，靠 Awake 的 LogError 让人发现
        if (stamina == null) return true;

        if (!stamina.TrySpend(blockStaminaCost)) return false;

        Debug.Log($"[GuardSystem] {actor.name} 挡下 {attack.attackName}：扣 {blockStaminaCost} 耐力，剩余 {stamina.Stamina:0}");
        return true;
    }
}
