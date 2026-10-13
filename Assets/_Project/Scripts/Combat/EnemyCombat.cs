using UnityEngine;

/// <summary>
/// 木桩敌人：不做 AI，只按固定节拍挥刀。
/// 招式数据、判定帧开关、仲裁口全部与玩家共用同一条链路。
/// </summary>
public class EnemyCombat : CombatController
{
    [SerializeField] private AttackData attack;
    [SerializeField] private int idleFrames = 90;

    protected override void OnIdleTick()
    {
        if (actor.StateFrames >= idleFrames) StartAttack(attack);
    }
}