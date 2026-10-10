using UnityEngine;

// 临时调试 HUD（阶段 11 会被正式 UI 取代）
// 1) 文本在「逻辑帧」里算好，OnGUI 只负责画 —— OnGUI 属于渲染侧，别在里面做逻辑
// 2) 统一用 ASCII：Unity 内置 IMGUI 字体不含中文字形，写中文会显示成方块
public class CombatDebugHUD : MonoBehaviour
{
    [SerializeField] private PlayerCombat player;

    private string line1 = "CombatDebugHUD: player not assigned";
    private string line2 = "";

    private void OnEnable()  { CombatClock.OnTick += HandleTick; }
    private void OnDisable() { CombatClock.OnTick -= HandleTick; }

    private void HandleTick(int frame)
    {
        if (player == null) return;

        AttackData a = player.CurrentAttack;
        if (a == null)
        {
            line1 = $"STATE {player.State}   (no attack yet)   tick {frame}";
            line2 = "";
            return;
        }

        // 注意：elapsedFrames 是「当前状态内」的帧数，所以 Attack 与 Recovery 各有一套边界
        // 这里刻意把阶段判断重复了一遍：如果 HUD 显示 ACTIVE 而判定体没开，说明两边不一致
        int e = player.ElapsedFrames;
        string phase;
        int span;

        switch (player.State)
        {
            case CombatState.Attack:
                phase = (e < a.ActiveStartFrames) ? "STARTUP " : "ACTIVE  ";
                span  = a.ActiveEndFrames;
                break;
            case CombatState.Recovery:
                phase = "RECOVERY";
                span  = a.recoveryFrames;
                break;
            default:
                phase = "IDLE    ";
                span  = 0;
                break;
        }

        line1 = $"STATE {player.State,-8} {a.attackName,-8} {phase} frame {e,3}/{span,-3} tick {frame}";
        line2 = $"startup {a.ActiveStartFrames}   active {a.ActiveEndFrames - a.ActiveStartFrames}   recovery {a.TotalFrames - a.ActiveEndFrames}   total {a.TotalFrames}";
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10f, 10f, 1000f, 22f), line1);
        GUI.Label(new Rect(10f, 32f, 1000f, 22f), line2);
    }
}
