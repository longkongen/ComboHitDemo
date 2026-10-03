using UnityEngine;

public class CombatActor : MonoBehaviour
{
    public CombatState state;
    public float hp = 100;
    



    public void SetState( CombatState newState)
    {
        state = newState;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
