using UnityEngine;

[CreateAssetMenu(fileName = "Attack_new",menuName = "ComboHit/AttackDate" )]
public class AttackData : ScriptableObject

{
    public string attackName = "Slash";

    public float damage = 10f;
    public int startupFrames = 6;
    public int activeFrames = 5;
    public int recoveryFrames = 11;

    public int TotalFrames  => startupFrames + activeFrames + recoveryFrames;
    public int ActiveStartFrames => startupFrames;
    public int ActiveEndFrames => startupFrames + activeFrames;


    private void OnValidate()
    {
        startupFrames = Mathf.Max(1, startupFrames);
        activeFrames = Mathf.Max(1, activeFrames);
        recoveryFrames = Mathf.Max(1, recoveryFrames);
    }


}