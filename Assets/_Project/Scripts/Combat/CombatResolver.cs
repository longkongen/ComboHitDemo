using UnityEngine;
public static class CombatResolver//判定仲裁
{
    public static void ResolveHit(CombatActor attacker, CombatActor defender, AttackData attack)
    {
        if (attacker == null || defender == null || attack == null) return;
        if (attacker == defender) return;
        if (defender.IsDead) return;

        // 无敌帧（阶段 5 第 3 步）：闪避前 9 帧内完全不吃判定
        DodgeSystem dodge = defender.Dodge;
        if (dodge != null && dodge.IsInvulnerable)
        {
            Debug.Log($"[DodgeSystem] {defender.name} 无敌帧内闪过 {attack.attackName}");
            return;                                                      // ← 阶段 5 第 4 步：极限闪避的判定与奖励插在这里
        }

        Vector3 knockbackDir = attacker.transform.forward;
        knockbackDir.y = 0f;
        if (knockbackDir.sqrMagnitude < 0.0001f) knockbackDir = Vector3.forward;
        knockbackDir.Normalize();

        defender.ReceiveHit(attack, knockbackDir);// 伤害 + 硬直都收敛到 CombatActor
        CombatClock.RequestHitStop(attack.hitStopFrames);
        // ← 阶段 5 第 2 步：GP 的拦截插在这里（必须排在普通格挡之前，GP 才能赢过普通格挡）
        // ← 阶段 6-8：极限闪避 / 相杀的拦截插在这里
    }

    // 格挡：只有「格挡盒」被攻方的攻击盒扫到时才会走到这里。
    // 方向性来自格挡盒的摆放与朝向（摆在角色前方、随角色 yaw 旋转），不是靠角度判断。
    public static bool ResolveGuard(CombatActor attacker, CombatActor defender, AttackData attack)
    {
        if (attacker == null || defender == null || attack == null) return false;
        if (attacker == defender) return false;
        if (defender.IsDead) return false;

        GuardSystem guard = defender.Guard;
        if (guard == null)
        {
            // 有格挡盒却没有 GuardSystem：防御会静默失效，必须响亮报出来
            Debug.LogError($"[CombatResolver] {defender.name} 有格挡盒但没有 GuardSystem 组件 -> 格挡无效（检查 Prefab 上是否真的挂了这个组件）", defender);
            return false;
        }
        // 三种结果分流：普通格挡只顿一下；GP 弹开还要打断攻方
        switch (guard.TryBlock(attack))
        {
            case GuardResult.None:
                return false;                                            // 没防御 / 耐力不够 => 照常挨打

            case GuardResult.Blocked:
                CombatClock.RequestHitStop(guard.BlockHitStopFrames);    // 挡下的小顿帧
                return true;                                             // 不调 ReceiveHit = 无伤害、无硬直

            case GuardResult.GuardPoint:
                attacker.EnterHitStun(attack);                           // 攻方按"自己这一招"的硬直帧数被打断
                CombatClock.RequestHitStop(guard.GpHitStopFrames);       // GP 的顿帧比普通格挡长得多
                return true;

            default:
                return false;
        }
    }
}
