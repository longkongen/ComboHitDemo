using UnityEngine;
public class Hurtbox : MonoBehaviour
{
    [SerializeField]
    private CombatActor owner;

    // 格挡盒：同样挂在 Hurtbox 层、同样是一个 Hurtbox，但只有「防御中且被对方的攻击盒扫到」才生效。
    // 方向性不靠角度判断，而是靠这个盒子自己摆在角色前方、并随角色朝向旋转（阶段 5 第 1 步的格挡语义）。
    [SerializeField]
    private bool guardOnly;

    public CombatActor Owner => owner;
    public bool GuardOnly => guardOnly;

    public void Awake()
    {
        if (owner == null)
        {
            Debug.LogError("[Hurtbox] owner 未接线", this);
            enabled = false;
            return;
        }
        if (GetComponent<Collider>() == null)
        {
            Debug.LogError("[Hurtbox] 缺 Collider -> 这个盒子永远不会被扫到", this);
        }
    }

    // 调试用：受击盒青色、格挡盒橙色、owner 未接线时洋红（静默失效的东西要刺眼）
    private void OnDrawGizmos()
    {
        Collider body = GetComponent<Collider>();
        if (body == null) return;

        if (owner == null) Gizmos.color = new Color(1f, 0f, 1f, 0.6f);
        else if (guardOnly) Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
        else Gizmos.color = new Color(0.2f, 1f, 1f, 0.35f);

        Gizmos.DrawWireCube(body.bounds.center, body.bounds.size);
    }
}
