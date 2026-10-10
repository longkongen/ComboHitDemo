using UnityEngine;
public class Hurtbox : MonoBehaviour
{
    [SerializeField]
    private CombatActor owner;

    public CombatActor Owner => owner;

    public void Awake()
    {
        if (owner == null)
        {
            Debug.LogError("[Hurtbox] owner 未接线", this);
            enabled = false;
            return;
        }
    }

    // 调试用：画出受击盒；owner 没接线时画成洋红（静默失效的东西要刺眼）
    private void OnDrawGizmos()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null) return;

        Gizmos.color = owner != null ? new Color(0.2f, 1f, 1f, 0.35f) : new Color(1f, 0f, 1f, 0.6f);
        Gizmos.DrawWireCube(capsule.bounds.center, capsule.bounds.size);
    }
}
