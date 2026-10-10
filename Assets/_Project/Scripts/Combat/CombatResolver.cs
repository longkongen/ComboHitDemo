using UnityEngine;
public static class CombatResolver//判定仲裁
{
    public static void ResolveHit(CombatActor attack, CombatActor defender, float damage)
    {
        if(attack == null || defender == null) return;
        if(attack == defender) return;  
        if(defender.IsDead) return;
        defender.TakeDamage(damage);
    }
}