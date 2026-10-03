using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    public int damage = 10;
    private bool isActive = false;
    public void Activate()
    {
        isActive = true;
    }
    public void Deactivate()
    {
        isActive = false;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!isActive)
        {
            return;
        }
        Debug.Log("ЙЅЛїУќжаЃК"+ other.name);
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
