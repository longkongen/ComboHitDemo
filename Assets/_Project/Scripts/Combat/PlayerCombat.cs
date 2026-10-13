using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombat : CombatController
{
    private const int maxCombo = 3;

    [SerializeField] private int inputBufferFrames = 30;
    private bool attackBuffered;
    private int bufferFramesLeft;
    private int comboStep;                  // 0 = 没有连招在进行中

    [SerializeField] private AttackData[] combo;

    protected override void Awake()
    {
        base.Awake();                       // ← 必须先调：基类的引用校验 + enabled=false 都在里面

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

    private void Update() { CheckAttackInput(); }        // 渲染帧只采样，不进状态机

    protected override void OnPreTick() { UpdateInputBuffer(); }

    protected override void OnIdleTick()
    {
        if (!attackBuffered) return;
        attackBuffered = false;
        bufferFramesLeft = 0;
        comboStep = 1;
        StartAttack(combo[comboStep - 1]);
    }

    protected override void TickRecovery()
    {
        // 连招窗口（决策 1A）：判定帧结束 → 收招结束前 comboWindowCloseEarly 帧
        bool inWindow = (actor.StateFrames <= currentAttack.ComboWindowCloseFrame);

        if (inWindow && attackBuffered && comboStep < maxCombo)
        {
            attackBuffered = false;
            bufferFramesLeft = 0;
            comboStep++;
            StartAttack(combo[comboStep - 1]);   // 取消剩余收招
            return;
        }
        if (!inWindow) { attackBuffered = false; bufferFramesLeft = 0; }

        base.TickRecovery();                 // 收招播完 → Idle（含 OnComboEnded + 清残留）
    }

    protected override void OnComboEnded() { comboStep = 0; }

    protected override void ClearPendingInput()
    {
        attackBuffered = false;
        bufferFramesLeft = 0;
        comboStep = 0;
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
