using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class PlayerCombat : MonoBehaviour
{
    [SerializeField]
    private CombatActor actor;

    public float attackDuration = 0.25f;
    public float recoveryDuration = 0.20f;
    private int comboStep = 0;
    private const int maxCombo = 3;
    private bool attackBuffered = false;
    private bool attackActive = false;
    private float stateTimer;

    private AttackData attack1;
    private AttackData attack2;
    private AttackData attack3;
    private AttackData currentAttack;

    [SerializeField]
    private AttackHitbox attackHitbox;

    private void Awake()
    {
        attack1 = new AttackData(
            "Slash 1",
            10f,
            0.10f,
            0.08f,
            0.18f
            );
        attack2 = new AttackData(
            "Slash 2",
            15f,
            0.14f,
            0.10f,
            0.22f
            );
        attack3 = new AttackData(
            "Slash 3",
            25f,
            0.20f,
            0.15f,
            0.35f
            );

        if (actor == null) { Debug.LogError("[PlayerCombat] actor 未接线", this); enabled = false; }

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CheckAttackInput();
        switch (actor.state)
        {
            case CombatState.Idle:
                UpdateIdle();
                break;
            case CombatState.Attack:
                UpdateAttack();
                break;
            case CombatState.Recovery:
                UpdateRecovery();
                break;

        }
    }
    private  void ChangeState(CombatState next)
    {
        actor.SetState(next);
        switch (next)
        {
            case CombatState.Idle:
                Debug.Log("进入 Idle");
                break;

            case CombatState.Attack:
                
                Debug.Log("进入攻击："+currentAttack.attackName);
                break;

            case CombatState.Recovery:
                attackActive = false;
                stateTimer = recoveryDuration;
                Debug.Log("进入 Recovery");
                break;
        }
    }
    private void UpdateIdle()
    {
        if (attackBuffered)
        {
            attackBuffered = false;
            comboStep = 1;
            StartAttack();
           
        } 
    }
    private void StartAttack()
    {
        if ( comboStep == 1)
        {
            currentAttack = attack1;
            
        }else if ( comboStep == 2)
        {
            currentAttack = attack2;
        }else if ( comboStep == 3)
        {
            currentAttack = attack3;
        }
        stateTimer = currentAttack.startup;
        attackActive = false;
        ChangeState(CombatState.Attack);
        Debug.Log("开始攻击：" + currentAttack.attackName);
        
            
        
    }
    private void UpdateAttack()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            if (!attackActive)
            {
                attackActive = true;
                stateTimer = currentAttack.active;
                attackHitbox.Activate();
                Debug.Log("进入Active");
            }
            else
            {
                attackActive = false;
                attackHitbox.Deactivate();
                ChangeState(CombatState.Recovery);
            }
        }
    }
    private void UpdateRecovery()
    {
        stateTimer -= Time.deltaTime;

        if (stateTimer <= 0f)
        {
            if (attackBuffered && comboStep < maxCombo)
            {
                attackBuffered = false;
                comboStep++;
                StartAttack();
                ChangeState(CombatState.Attack);
            }
            else
            {
                comboStep = 0;
                ChangeState(CombatState.Idle);
            }
        }
    }
    private void CheckAttackInput()
    {
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            attackBuffered = true;
        }
    }



}
