using UnityEngine;

public class StaminaSystem : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float regenPerSecond = 25f;   // 自然回复速度
    [SerializeField] private int regenDelayFrames = 30;    // 消耗后压制 N 帧再开始回

    private float stamina;
    private int regenHoldFrames;

    public float Stamina => stamina;
    public float MaxStamina => maxStamina;
    public bool IsExhausted => stamina <= 0f;              // 阶段 10「破防」将来要用

    private void Awake() { stamina = maxStamina; }

    private void OnEnable() { CombatClock.OnTick += OnCombatTick; }   // 成对，纪律 #6
    private void OnDisable() { CombatClock.OnTick -= OnCombatTick; }

    /// <summary>够就扣掉并返回 true；不够原样返回 false（由调用方负责放弃这个动作）。</summary>
    public bool TrySpend(float cost)
    {
        if (cost <= 0f) return true;
        if (stamina < cost) return false;
        stamina -= cost;
        regenHoldFrames = regenDelayFrames;
        return true;
    }

    /// <summary>奖励性回复（GP / 极限闪避），不影响压制计时。</summary>
    public void Restore(float amount)
    {
        if (amount <= 0f) return;
        stamina = Mathf.Min(maxStamina, stamina + amount);
    }

    private void OnCombatTick(int frame)
    {
        if (regenHoldFrames > 0) { regenHoldFrames--; return; }        // 刚花过，先不回
        if (stamina >= maxStamina) return;
        stamina = Mathf.Min(maxStamina, stamina + regenPerSecond * CombatClock.TickDelta);
    }

    // ── 临时：第 0 步验收用，第 1 步接入真正的消耗者后删掉 ──
    [ContextMenu("Debug: 花 30 耐力")]
    private void DebugSpend30() { Debug.Log($"[StaminaSystem] TrySpend(30) = {TrySpend(30f)}，剩余 {stamina:0}"); }

    [ContextMenu("Debug: 回 40 耐力")]
    private void DebugRestore40() { Restore(40f); Debug.Log($"[StaminaSystem] Restore(40)，剩余 {stamina:0}"); }
}