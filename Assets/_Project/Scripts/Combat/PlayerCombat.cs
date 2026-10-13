using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : CombatController
{
    private const int maxChainDepth = 16;   // 防御：链是数据，写环也不会无限连

    [SerializeField] private int inputBufferFrames = 30;
    private bool attackBuffered;
    private int bufferFramesLeft;

    [SerializeField] private ComboChain chain;
    private AttackData currentNode;         // 链上当前节点（null = 没有连招在进行中）
    private int chainDepth;                 // 本套连招已打出几段

    protected override void Awake()
    {
        base.Awake();                       // ← 必须先调：基类的引用校验 + enabled=false 都在里面

        // 连招链是手拖引用，最容易漏；链本身的数据问题由 ComboChain.OnValidate 负责
        if (chain == null || chain.Entry == null)
        {
            Debug.LogError("[PlayerCombat] chain 未接线（或 entry 为空）", this);
            enabled = false;
        }
    }

    private void Update() { CheckAttackInput(); }        // 渲染帧只采样，不进状态机

    protected override void OnPreTick() { UpdateInputBuffer(); }

    protected override void OnIdleTick()
    {
        if (!attackBuffered) return;
        attackBuffered = false;
        bufferFramesLeft = 0;
        currentNode = chain.Entry;          // 每次起手都从链的 entry 开始
        chainDepth = 1;
        StartAttack(currentNode);
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
