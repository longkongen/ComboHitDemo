using UnityEngine;

// 闪避：只管"位移 + 无敌帧窗口"。什么时候允许闪（Idle / 攻击 / 收招 / 防御 都能取消进闪避）
// 由 PlayerCombat 决定，本组件不读输入（与 PlayerMotor 同款分工）
public class DodgeSystem : MonoBehaviour
{
    [SerializeField] private float staminaCost = 25f;
    [SerializeField] private int invulnFrames = 9;      // 只有前 9 帧无敌，剩下的时间是破绽
    [SerializeField] private int totalFrames = 18;      // 整个闪避持续的逻辑帧数
    [SerializeField] private float distance = 2f;       // 总位移（米），按帧均分 → 每帧 2/18 ≈ 0.111 m

    private CombatActor actor;
    private StaminaSystem stamina;
    private CharacterController controller;

    private Vector3 direction = Vector3.back;

    public int InvulnFrames => invulnFrames;
    public int TotalFrames => totalFrames;
    public float StaminaCost => staminaCost;

    public bool IsDodging => (actor != null) && (actor.State == CombatState.Dodge);

    // 无敌帧判定：闪避状态的前 invulnFrames 帧（进入那帧 StateFrames 为 0，所以用 <）
    public bool IsInvulnerable => IsDodging && (actor.StateFrames < invulnFrames);

    private void Awake()
    {
        // 同物体自解析：引用不靠手拖，漏挂就响亮报错（纪律 #9 的本意）
        if (actor == null) actor = GetComponent<CombatActor>();
        if (actor == null) Debug.LogError("[DodgeSystem] 缺 CombatActor，闪避状态读不到", this);

        if (stamina == null) stamina = GetComponent<StaminaSystem>();
        if (stamina == null) Debug.LogError("[DodgeSystem] 缺 StaminaSystem，闪避将不消耗耐力", this);

        if (controller == null) controller = GetComponent<CharacterController>();
        if (controller == null) Debug.LogError("[DodgeSystem] 缺 CharacterController，闪避将不会位移", this);
    }

    // 订阅/退订必须成对（Domain Reload 已禁用，漏退订会让一次 tick 走两遍）
    private void OnEnable() { CombatClock.OnTick += OnCombatTick; }
    private void OnDisable() { CombatClock.OnTick -= OnCombatTick; }

    // 由 PlayerCombat 在"决定要闪"的那一刻调用：扣耐力 + 锁定方向。
    // 返回 false = 耐力不够，这次闪不出来（状态不切换）
    public bool TryStart(Vector2 moveInput)
    {
        if (stamina != null && !stamina.TrySpend(staminaCost))
        {
            Debug.Log($"[DodgeSystem] {actor.name} 耐力不足（{stamina.Stamina:0}/{staminaCost:0}），闪避失败");
            return false;
        }

        direction = new Vector3(moveInput.x, 0f, moveInput.y);
        if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;   // 没按方向键 → 往身后撤
        direction.y = 0f;
        direction.Normalize();
        return true;
    }

    private void OnCombatTick(int frame)
    {
        if (actor == null || controller == null) return;
        if (actor.State != CombatState.Dodge) return;
        if (actor.StateFrames >= totalFrames) return;    // 收尾（回 Idle）由 PlayerCombat 做

        // 每帧位移 = distance / totalFrames（用米/秒的写法换算，与帧率无关）
        float speed = distance / (totalFrames * CombatClock.TickDelta);
        controller.Move(direction * speed * CombatClock.TickDelta);

        if (actor.StateFrames == 0)
            Debug.Log($"[DodgeSystem] {actor.name} 闪避开始 → ({direction.x:0.00}, {direction.z:0.00})，无敌 {invulnFrames} 帧 / 共 {totalFrames} 帧");
    }
}
