using UnityEngine;

// 普通防御：参数与"这一击能不能挡下"的判定都在这里。
// 状态迁移仍由 CombatController / PlayerCombat 写（纪律 #3：状态的唯一来源是 CombatActor），
// 这里只回答一个问题：这一击挡不挡得住。
public enum GuardResult { None,Blocked,GuardPoint}
public class GuardSystem : MonoBehaviour
{
    [SerializeField] private float blockStaminaCost = 10f;      // 每次挡下扣多少耐力
    [SerializeField] private int blockHitStopFrames = 2;        // 挡下时全场的小顿帧
    [SerializeField] private float guardDamageMultiplier = 0f;  // 0 = 完全挡住（阶段 5 决策 A）
    [SerializeField] private int gpWindowFrames = 8;
    [SerializeField] private int gpHitStopFrames = 8;
    [SerializeField] private float gpStaminaReward = 30f;

    public int GpWindowFrames => gpWindowFrames;
    public int GpHitStopFrames => gpHitStopFrames;

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

    // 尝试挡下这一击。三种结果：
    //   None       = 没在防御，或耐力付不起 => 照常挨打
    //   GuardPoint = 防御起手的前 gpWindowFrames 帧内被砍中 => 弹开攻方（回耐、不扣耐）
    //   Blocked    = 窗口过后仍按住 => 普通格挡（扣 blockStaminaCost，攻方继续挥）
    public GuardResult TryBlock(AttackData attack)
    {
        if (attack == null) return GuardResult.None;
        if (!IsGuardActive) return GuardResult.None;

        // 进入 Guard 的那一帧 StateFrames == 0 => 窗口是 0..gpWindowFrames-1，正好 gpWindowFrames 帧
        if (actor.StateFrames < gpWindowFrames)
        {
            if (stamina != null) stamina.Restore(gpStaminaReward);   // 奖励回耐，不动压制计时
            Debug.Log($"[GuardSystem] {actor.name} GP 弹开 {attack.attackName}：回 {gpStaminaReward} 耐力");
            return GuardResult.GuardPoint;
        }

        // 没有耐力系统时退化成"无限格挡"，靠 Awake 的 LogError 让人发现
        if (stamina == null) return GuardResult.Blocked;

        if (!stamina.TrySpend(blockStaminaCost)) return GuardResult.None;
        Debug.Log($"[GuardSystem] {actor.name} 挡下 {attack.attackName}：扣 {blockStaminaCost} 耐力，剩余 {stamina.Stamina:0}");
        return GuardResult.Blocked;
    }
}
