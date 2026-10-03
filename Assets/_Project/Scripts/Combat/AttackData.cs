public class AttackData
{
    public string attackName;

    public float damage;
    public float startup;
    public float active;
    public float recovery;

    public AttackData(string name , float damage, float startup, float active,float recovery)
    {
        attackName = name;
        this.damage = damage;
        this.startup = startup; 
        this.active = active;   
        this.recovery = recovery;   
    }


}