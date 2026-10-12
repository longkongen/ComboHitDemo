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
    [Tooltip("命中瞬间整个逻辑时钟冻结的逻辑帧数；0 = 不顿帧")]
    public int hitStopFrames = 6;

    private void OnValidate()
    {
        startupFrames = Mathf.Max(1, startupFrames);
        activeFrames = Mathf.Max(1, activeFrames);
        recoveryFrames = Mathf.Max(1, recoveryFrames);
        hitStunFrames = Mathf.Max(1, hitStunFrames);
        hitStopFrames = Mathf.Max(0, hitStopFrames);   // 0 合法：表示这一招不顿帧
    }


}