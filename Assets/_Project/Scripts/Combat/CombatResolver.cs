using UnityEngine;
public static class CombatResolver//判定仲裁
{
    public static void ResolveHit(CombatActor attacker, CombatActor defender, AttackData attack)
    {
        if (attacker == null || defender == null || attack == null) return;
        if (attacker == defender) return;
        if (defender.IsDead) return;

        defender.ReceiveHit(attack); // 伤害 + 硬直都收敛到 CombatActor
        CombatClock.RequestHitStop(attack.hitStopFrames);
        // ← 阶段 3 第 3 步：击退
        // ← 阶段 5/6/8：GP / 极限闪避 / 相杀的拦截都插在这里
    }
}