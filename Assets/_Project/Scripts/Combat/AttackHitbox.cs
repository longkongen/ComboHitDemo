using System.Collections.Generic;
using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    // 伤害由本次攻击的 AttackData 传入，这里不再保留“第二份伤害数据”
    [SerializeField]
    private CombatActor owner;
    [SerializeField]
    private BoxCollider shape;
    private readonly HashSet<Hurtbox> hitTargets = new HashSet<Hurtbox>();  // 本次挥击已结算过的目标
    private readonly Collider[] results = new Collider[16];                 // 复用数组，别每帧 new
    private int hurtboxMask;
    private AttackData currentAttack;
    private bool isActive = false;

    private void Awake()
    {
        hurtboxMask = LayerMask.GetMask("Hurtbox");
        if (owner == null) { Debug.LogError("[AttackHitbox] owner 未接线", this); enabled = false; return; }
        if (shape == null) shape = GetComponent<BoxCollider>();
        if (shape == null) { Debug.LogError("[AttackHitbox] 缺 BoxCollider", this); enabled = false; return; }
        if (hurtboxMask == 0) Debug.LogError("[AttackHitbox] 找不到 Hurtbox 层", this);
    }
    public void Activate(AttackData attack)
    {
        currentAttack = attack;
        hitTargets.Clear();
        isActive = true;
        CheckHit();
    }
    public void CheckHit()
    {
        if (!isActive || currentAttack == null) return;
        Physics.SyncTransforms();   // 必须在查询之前，理由见下

        Vector3 center = shape.transform.TransformPoint(shape.center);
        Vector3 halfExtents = Vector3.Scale(shape.size, shape.transform.lossyScale) * 0.5f;

        int count = Physics.OverlapBoxNonAlloc(
            center, halfExtents, results, shape.transform.rotation,
            hurtboxMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Hurtbox hurtbox = results[i].GetComponentInParent<Hurtbox>();
            if (hurtbox == null) continue;
            if (hurtbox.Owner == null) continue;
            if (hurtbox.Owner == owner) continue;         // 不打自己
            if (hitTargets.Contains(hurtbox)) continue;   // 同一次挥击只结算一次

            hitTargets.Add(hurtbox);
            CombatResolver.ResolveHit(owner, hurtbox.Owner, currentAttack);
        }
    }
     private void OnEnable() { CombatClock.OnTick += OnTick; }
     private void OnDisable() { CombatClock.OnTick -= OnTick; }
     private void OnTick(int frame) { CheckHit(); }   // 非判定帧时 CheckHit 自己早退

    public void Deactivate()
    {
        isActive = false;
    }

    // 调试用：把判定盒画出来（黄 = 待命，红 = 判定帧开着）。Gizmos 只在编辑器可见，不进包。
    private void OnDrawGizmos()
    {
        BoxCollider box = shape != null ? shape : GetComponent<BoxCollider>();
        if (box == null) return;

        Vector3 center = box.transform.TransformPoint(box.center);
        Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);

        Gizmos.color = isActive ? new Color(1f, 0.2f, 0.2f, 1f) : new Color(1f, 1f, 0.2f, 0.25f);
        Gizmos.DrawWireCube(center, size);
        if (isActive) Gizmos.DrawCube(center, size);
    }
}
