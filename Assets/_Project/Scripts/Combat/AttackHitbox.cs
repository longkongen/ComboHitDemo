using UnityEngine;

public class AttackHitbox : MonoBehaviour
{
    // 伤害由本次攻击的 AttackData 传入，这里不再保留“第二份伤害数据”
    public float damage = 10f;
    private bool isActive = false;

    public void Activate(float attackDamage)
    {
        damage = attackDamage;
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
        Debug.Log("攻击命中："+ other.name);
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
