using UnityEngine;

// 临时调试 HUD（阶段 11 会被正式 UI 取代）
// 1) 文本在「逻辑帧」里算好，OnGUI 只负责画 —— OnGUI 属于渲染侧，别在里面做逻辑
// 2) 统一用 ASCII：Unity 内置 IMGUI 字体不含中文字形，写中文会显示成方块
// 3) 一行一个角色（player / enemy），同时暴露 HP、耐力、状态、招式、状态内帧数
public class CombatDebugHUD : MonoBehaviour
{
    [SerializeField] private PlayerCombat player;
    [SerializeField] private EnemyCombat enemy;

    // 耐力从各自的控制器所在物体上取，不新增手拖引用（少一个会漂移的字段）
    private StaminaSystem playerStamina;
    private StaminaSystem enemyStamina;

    private string line1 = "CombatDebugHUD: player not assigned";
    private string line2 = "CombatDebugHUD: enemy not assigned";
    private string line3 = "";

    private void OnEnable()  { CombatClock.OnTick += HandleTick; }
    private void OnDisable() { CombatClock.OnTick -= HandleTick; }

    // 这是调试工具：能自动找到就不该让人手拖，少一个出错点
    // 只在字段为空时自动找 —— 若字段被拖成了 Prefab 资产，这里不覆盖它，
    // 好让「来源拖错」这种错误仍然能显示在屏幕上（下面的自诊断）
    // 用 FindAnyObjectByType 而不是 FindFirstObjectByType：后者按 instanceID 排序取"第一个"，
    // 而 instanceID 依赖运行期创建顺序（Domain Reload 关闭时每次 Play 还会增长），
    // 所以"第一个"不可依赖；本场景玩家/木桩各只有一个，"任意一个"就是正确语义
    private void Awake()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerCombat>();
            Debug.Log($"[CombatDebugHUD] player 未接线，自动找到：{(player != null ? player.name : "没有")}", this);
        }
        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyCombat>();
            Debug.Log($"[CombatDebugHUD] enemy 未接线，自动找到：{(enemy != null ? enemy.name : "没有")}", this);
        }

        // 耐力：取不到就显示 n/a（说明 Prefab 上漏挂 StaminaSystem），别静默什么都不显示
        if (player != null) playerStamina = player.GetComponent<StaminaSystem>();
        if (enemy != null) enemyStamina = enemy.GetComponent<StaminaSystem>();
    }

    private void HandleTick(int frame)
    {
        line1 = DescribePlayer();
        line2 = DescribeEnemy();
        line3 = $"tick {frame,6}   (logic clock 60 Hz)";
    }

    // ===== 自诊断：把接线错误直接写在屏幕上，别让它伪装成逻辑 bug =====
    // 拖成 Project 窗口里的 Prefab 资产时，那个对象永远不会运行、字段永远是初始值
    // 判据：场景实例的 scene.IsValid() 为 true，Prefab 资产为 false
    private string DescribePlayer()
    {
        if (player == null)
            return "player : not assigned -> drag the Player from the HIERARCHY into the player field";
        if (!player.gameObject.scene.IsValid())
            return "player : reference is a Prefab ASSET -> drag from the HIERARCHY, not the PROJECT window";

        return Describe("player", player.Actor, player.CurrentAttack, player.ElapsedFrames, playerStamina);
    }

    private string DescribeEnemy()
    {
        if (enemy == null)
            return "enemy  : not assigned -> drag the Enemy from the HIERARCHY into the enemy field";
        if (!enemy.gameObject.scene.IsValid())
            return "enemy  : reference is a Prefab ASSET -> drag from the HIERARCHY, not the PROJECT window";

        return Describe("enemy ", enemy.Actor, enemy.CurrentAttack, enemy.ElapsedFrames, enemyStamina);
    }

    private static string Describe(string tag, CombatActor actor, AttackData a, int e, StaminaSystem stam)
    {
        if (actor == null) return $"{tag} : CombatActor not assigned (check the prefab wiring)";

        // 注意：e 是「当前状态内」的帧数，所以 Attack 与 Recovery 各有一套边界
        // 这里刻意把阶段判断重复了一遍：HUD 显示 ACTIVE 而判定盒没红，说明两边不一致
        CombatState state = actor.State;
        // 硬直时显示“打中我的那一招”：受害方手里的 currentAttack 是被打断的那一招，不是打中他的
        string name = (state == CombatState.Hit && actor.LastHit != null)
                    ? actor.LastHit.attackName
                    : ((a != null) ? a.attackName : "-");
        string phase;
        int span;

        switch (state)
        {
            case CombatState.Attack:
                phase = (a != null && e < a.ActiveStartFrames) ? "STARTUP " : "ACTIVE  ";
                span  = (a != null) ? a.ActiveEndFrames : 0;
                break;
            case CombatState.Recovery:
                phase = "RECOVERY";
                span  = (a != null) ? a.recoveryFrames : 0;
                break;
            case CombatState.Hit:
                phase = "HIT     ";
                span  = (actor.LastHit != null) ? actor.LastHit.hitStunFrames : 0;
                break;
            case CombatState.Dead:
                phase = "DEAD    ";
                span  = 0;
                break;
            default:
                phase = "IDLE    ";
                span  = 0;
                break;
        }

        string stamText = (stam != null) ? $"  STAM {stam.Stamina,3:0}/{stam.MaxStamina:0}" : "  STAM n/a";
        return $"{tag}  HP {actor.Hp,3:0}  {state,-8} {name,-11} {phase} frame {e,3}/{span,-3}{stamText}";
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10f, 10f, 1200f, 22f), line1);
        GUI.Label(new Rect(10f, 32f, 1200f, 22f), line2);
        GUI.Label(new Rect(10f, 54f, 1200f, 22f), line3);
    }
}
