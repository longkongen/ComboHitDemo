using UnityEngine;

public class PlayerCombat : CombatController
{
    private const int maxChainDepth = 16;   // 防御：链是数据，写环也不会无限连

    [SerializeField] private int inputBufferFrames = 30;
    private bool attackBuffered;
    private int bufferFramesLeft;

    [SerializeField] private ComboChain chain;
    private AttackData currentNode;         // 链上当前节点（null = 没有连招在进行中）
    private int chainDepth;// 本套连招已打出几段
    [SerializeField]
    private PlayerInputSource input;

    protected override void Awake()
    {
        base.Awake();                       // ← 必须先调：基类的引用校验 + enabled=false 都在里面

        // 连招链是手拖引用，最容易漏；链本身的数据问题由 ComboChain.OnValidate 负责
        if (chain == null || chain.Entry == null)
        {
            Debug.LogError("[PlayerCombat] chain 未接线（或 entry 为空）", this);
            enabled = false;
        }
        if (input == null) input = GetComponent<PlayerInputSource>();
        if (input == null) Debug.LogError("[PlayerCombat] missing PlayerInputSource -> attack input will not work", this);
    }

    private void Update() { CheckAttackInput(); }        // 渲染帧只采样，不进状态机

    protected override void OnPreTick() { UpdateInputBuffer(); }

    protected override void OnIdleTick()
    {
        // 攻击优先：按着防御键时按下攻击也能出手
        if (TryStartBufferedAttack()) return;

        // 其次：按住防御键进普通防御（本步不做"攻击中取消进防御"，要等收招播完才回到这里）
        if (input != null && input.GuardHeld) ChangeState(CombatState.Guard);
    }

    protected override void TickGuard()
    {
        // 防御中按下攻击 -> 立刻出招（不必先松手）
        if (TryStartBufferedAttack()) return;

        // 松手回 Idle。这里刻意**不清**输入缓冲：防御中按下的攻击键会在松手那一帧立刻出招，这是想要的响应性
        if (input == null || !input.GuardHeld) ChangeState(CombatState.Idle);
    }

    // 从链的 entry 起手；返回 true 表示这一 tick 用掉了输入缓冲
    private bool TryStartBufferedAttack()
    {
        if (!attackBuffered) return false;

        attackBuffered = false;
        bufferFramesLeft = 0;
        currentNode = chain.Entry;          // 每次起手都从链的 entry 开始
        chainDepth = 1;
        StartAttack(currentNode);
        return true;
    }

    protected override void TickRecovery()
    {
        // 连招窗口（决策 1A）：判定帧结束 → 收招结束前 comboWindowCloseEarly 帧
        bool inWindow = (actor.StateFrames <= currentAttack.ComboWindowCloseFrame);
        AttackData next = chain.NextOf(currentNode);      // 链到头 = null

        if (inWindow && attackBuffered && next != null && chainDepth < maxChainDepth)
        {
            attackBuffered = false;
            bufferFramesLeft = 0;
            currentNode = next;
            chainDepth++;
            StartAttack(next);               // 取消剩余收招
            return;
        }
        if (chainDepth >= maxChainDepth && next != null)
        {
            Debug.LogWarning($"[PlayerCombat] 连招深度达到上限 {maxChainDepth}，强制收招（链里是不是有环？）", this);
        }
        // 链到头（next == null）也必须清缓冲：否则关窗前按下的键会在收招末帧"自己接出来"
        if (!inWindow || next == null) { attackBuffered = false; bufferFramesLeft = 0; }

        base.TickRecovery();                 // 收招播完 → Idle（含 OnComboEnded + 清残留）
    }

    protected override void OnComboEnded() { currentNode = null; chainDepth = 0; }

    protected override void ClearPendingInput()
    {
        attackBuffered = false;
        bufferFramesLeft = 0;
        currentNode = null;
        chainDepth = 0;
    }

    private void CheckAttackInput()
    {
        if (input == null || !input.AttackPressed) return;
        input.ConsumeAttack();
        attackBuffered = true;
        bufferFramesLeft = inputBufferFrames;
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
