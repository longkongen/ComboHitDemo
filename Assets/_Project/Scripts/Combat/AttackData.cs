using UnityEngine;

[CreateAssetMenu(fileName = "Attack_new",menuName = "ComboHit/AttackData" )]
public class AttackData : ScriptableObject

{
    public string attackName = "Slash";

    public float damage = 10f;
    public int startupFrames = 6;
    public int activeFrames = 5;
    public int recoveryFrames = 11;
    public int hitStunFrames = 12;
    public int TotalFrames  => startupFrames + activeFrames + recoveryFrames;
    public int ActiveStartFrames => startupFrames;
    public int ActiveEndFrames => startupFrames + activeFrames;
    public int ComboWindowCloseFrame => Mathf.Max(0, recoveryFrames - comboWindowCloseEarly);
    [Tooltip("命中瞬间整个逻辑时钟冻结的逻辑帧数；0 = 不顿帧")]
    public int hitStopFrames = 6;
    [Tooltip("命中后沿攻击者正前方推开的距离（米）；0 = 不击退")]
    public float knockbackDistance = 0.5f;
    [Tooltip("击退持续的逻辑帧数；推完立即停（缓动曲线留到阶段 11）")]
    public int knockbackFrames = 10;
    [Tooltip("连招窗口提前关闭的帧数：窗口 = [判定结束, 收招结束 − 这个值)；0 = 收招全程都能接")]
    public int comboWindowCloseEarly = 0;
    private void OnValidate()
    {
        startupFrames = Mathf.Max(1, startupFrames);
        activeFrames = Mathf.Max(1, activeFrames);
        recoveryFrames = Mathf.Max(1, recoveryFrames);
        hitStunFrames = Mathf.Max(1, hitStunFrames);
        hitStopFrames = Mathf.Max(0, hitStopFrames);// 0 合法：表示这一招不顿帧
        knockbackDistance = Mathf.Max(0f, knockbackDistance);
        knockbackFrames = Mathf.Max(0, knockbackFrames);
        comboWindowCloseEarly = Mathf.Max(0, comboWindowCloseEarly);
    }


}