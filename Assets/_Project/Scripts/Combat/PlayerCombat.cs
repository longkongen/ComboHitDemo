using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField]
    private CombatActor actor;

    private const int maxCombo = 3;

    // 输入缓冲：按键先被记住，等取消窗口到来再消费；超时作废，
    // 否则很久以前的按键会在回到 Idle 的瞬间“复活”，打出一段玩家没按过的攻击。
    // 单位是逻辑帧：30 帧 = 0.5 秒，可以直接和招式帧数比较。
    [SerializeField]
    private int inputBufferFrames = 30;
    private bool attackBuffered;
    private int bufferFramesLeft;

    // 状态内帧数不在本地自增了：由 CombatActor.StateFrames 统一派生
    // （状态由 CombatActor 独占持有，计时必须跟状态走，否则外部改写状态时会对不上）

    private int comboStep;
    private AttackData currentAttack;

    [SerializeField]
    private AttackData[] combo;

    [SerializeField]
    private AttackHitbox attackHitbox;

    private void Awake()
    {
        // 缺失引用属于“配置错误”，启动时就响亮地失败，别等玩家按键才抛 NullReference
        if (actor == null)
        {
            Debug.LogError("[PlayerCombat] actor 未接线", this);
            enabled = false;
        }
        if (attackHitbox == null)
        {
            Debug.LogError("[PlayerCombat] attackHitbox 未接线", this);
            enabled = false;
        }

        // combo 是手拖引用，最容易漏；长度不足或含空槽位都在这里当场拦下
        if (combo == null || combo.Length < maxCombo)
        {
            int count = (combo == null) ? 0 : combo.Length;
            Debug.LogError($"[PlayerCombat] combo 至少需要 {maxCombo} 个招式资产，当前 {count} 个", this);
            enabled = false;
        }
        else
        {
            for (int i = 0; i < maxCombo; i++)
            {
                if (combo[i] == null)
                {
                    Debug.LogError($"[PlayerCombat] combo[{i}] 未接线", this);
                    enabled = false;
                }
            }
        }
    }

    // ===== 调试用只读快照：不参与战斗逻辑，只给 CombatDebugHUD 看 =====
    public CombatActor Actor => actor;
    public CombatState State => (actor != null) ? actor.State : CombatState.Idle;
    public AttackData CurrentAttack => currentAttack;
    public int ElapsedFrames => actor.StateFrames;

    // 纪律：渲染帧只负责采样输入，逻辑帧才推进状态机（CombatClock 的执行顺序是 order 1000）
    private void OnEnable()
    {
        CombatClock.OnTick += OnCombatTick;
    }

    private void OnDisable()
    {
        CombatClock.OnTick -= OnCombatTick;      // 必须成对退订，否则一次 tick 会走两遍逻辑
    }

    private void Update()
    {
        CheckAttackInput();
    }

    private void OnCombatTick(int frame)
    {
        UpdateInputBuffer();

        switch (actor.State)
        {
            case CombatState.Idle:
                UpdateIdle();
                break;
            case CombatState.Attack:
                UpdateAttack();
                break;
            case CombatState.Recovery:
                UpdateRecovery();
                break;
        }
    }

    // 这个包装不只是转发：还要打日志、并在进入攻击时做一次收刀保险。
    // 计时复位不在这里 —— 它跟着 SetState 走，由 CombatActor 独占（StateFrames）
    private void ChangeState(CombatState next)
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

            default:
                Debug.LogWarning($"[PlayerCombat] 未处理的状态迁移：{next}", this);
                break;
        }
    }

    private void UpdateIdle()
    {
        if (attackBuffered)
        {
            attackBuffered = false;
            bufferFramesLeft = 0;
            comboStep = 1;
            StartAttack();
        }
    }

    private void StartAttack()
    {
        currentAttack = combo[comboStep - 1];
        ChangeState(CombatState.Attack);        // 内部走 actor.SetState，计时由 StateFrames 派生
    }

    private void UpdateAttack()
    {
        // 跨进判定期的那一帧：开判定（帧化后可以精确用 == 定位某一帧，秒计时做不到）
        if (actor.StateFrames == currentAttack.ActiveStartFrames)
        {
            attackHitbox.Activate(currentAttack.damage);
            Debug.Log($"进入 Active（第 {actor.StateFrames} 帧）");
        }

        // 判定期结束的那一帧：关判定并进收招
        if (actor.StateFrames >= currentAttack.ActiveEndFrames)
        {
            attackHitbox.Deactivate();
            ChangeState(CombatState.Recovery);
        }
    }

    private void UpdateRecovery()
    {
        if (actor.StateFrames >= currentAttack.recoveryFrames)
        {
            if (attackBuffered && comboStep < maxCombo)
            {
                attackBuffered = false;
                bufferFramesLeft = 0;
                comboStep++;
                // StartAttack 内部已完成 ChangeState(Attack)，这里不能重复调用
                StartAttack();
            }
            else
            {
                // 连招终止：必须清掉残留缓冲，否则回到 Idle 的瞬间会自动接出下一轮第一段
                attackBuffered = false;
                bufferFramesLeft = 0;
                comboStep = 0;
                ChangeState(CombatState.Idle);
            }
        }
    }

    private void CheckAttackInput()
    {
        if (Mouse.current == null) return;
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            attackBuffered = true;
            bufferFramesLeft = inputBufferFrames;
        }
    }

    private void UpdateInputBuffer()
    {
        if (!attackBuffered) return;

        bufferFramesLeft--;
        if (bufferFramesLeft <= 0)
        {
            attackBuffered = false;
            bufferFramesLeft = 0;
        }
    }
}
