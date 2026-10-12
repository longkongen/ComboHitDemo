using UnityEngine;

/// <summary>
/// 木桩敌人：不做 AI，只按固定节拍挥刀。
/// 招式数据、判定帧开关、仲裁口全部与玩家共用同一条链路。
/// </summary>
public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private CombatActor actor;
    [SerializeField] private AttackHitbox attackHitbox;
    [SerializeField] private AttackData attack;      // 木桩只有一招
    [SerializeField] private int idleFrames = 90;    // 两次挥刀之间的间隔（逻辑帧，90 = 1.5 秒）

    // 状态内计时由 CombatActor.StateFrames 统一派生，这里不再自己数帧

    private void Awake()
    {
        if (actor == null) { Debug.LogError("[EnemyCombat] actor 未接线", this); enabled = false; return; }
        if (attackHitbox == null) { Debug.LogError("[EnemyCombat] attackHitbox 未接线", this); enabled = false; return; }
        if (attack == null) { Debug.LogError("[EnemyCombat] attack 未配置", this); enabled = false; return; }
    }

    private void OnEnable() { CombatClock.OnTick += OnCombatTick; }
    private void OnDisable() { CombatClock.OnTick -= OnCombatTick; }

    private void OnCombatTick(int frame)
    {
        if (actor == null || attackHitbox == null || attack == null) return;   // 兜底

        // 被打死了就躺平：阶段 3 会把这里做成正式的状态迁移
        if (actor.IsDead)
        {
            if (actor.State != CombatState.Dead) actor.SetState(CombatState.Dead);
            return;
        }

        switch (actor.State)
        {
            case CombatState.Idle: UpdateIdle(); break;
            case CombatState.Attack: UpdateAttack(); break;
            case CombatState.Recovery: UpdateRecovery(); break;
        }
    }

    private void UpdateIdle()
    {
        if (actor.StateFrames >= idleFrames)   // 节拍到了就抬手
        {
            Debug.Log($"木桩进入攻击: {attack.attackName}（起手 {attack.startupFrames} 帧）");
            actor.SetState(CombatState.Attack);
        }
    }

    private void UpdateAttack()
    {
        if (actor.StateFrames == attack.ActiveStartFrames)   // 起手结束那一帧：开判定
        {
            Debug.Log($"木桩进入 Active（第 {actor.StateFrames} 帧）");
            attackHitbox.Activate(attack.damage);            // ← 注意这里日志在 Activate 之前
        }

        if (actor.StateFrames >= attack.ActiveEndFrames)     // 判定窗口结束：关判定并收招
        {
            attackHitbox.Deactivate();
            actor.SetState(CombatState.Recovery);
        }
    }

    private void UpdateRecovery()
    {
        if (actor.StateFrames >= attack.recoveryFrames) actor.SetState(CombatState.Idle);
    }

    // ===== 调试用只读快照（与 PlayerCombat 对称）：不参与战斗逻辑，只给 CombatDebugHUD 看 =====
    public CombatActor Actor => actor;
    public AttackData CurrentAttack => attack;
    public int ElapsedFrames => actor.StateFrames;
}