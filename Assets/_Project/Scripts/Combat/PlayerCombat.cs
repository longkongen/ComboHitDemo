using UnityEngine;
using UnityEngine.InputSystem;

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

    // 输入缓冲：按键先被记住，等取消窗口到来再消费；超时作废，
    // 否则很久以前的按键会在回到 Idle 的瞬间“复活”，打出一段玩家没按过的攻击。
    [SerializeField]
    private float inputBufferDuration = 0.5f;
    private float bufferTimer;

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

        // 缺失引用属于"配置错误"，启动时就响亮地失败，别等玩家按键才抛 NullReference
        if (actor == null)
        {
            Debug.LogError("[PlayerCombat] actor 未接线", this);
            enabled = false;
        }
        if (attackHitbox == null)
        {
            Debug.LogError("[PlayerCombat] attackHitbox 未接线", this);
            enabled = false;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CheckAttackInput();
        UpdateInputBuffer();
        switch (actor.State)
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
    private void ChangeState(CombatState next)
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
                stateTimer = currentAttack.recovery;
                Debug.Log("进入 Recovery");
                break;

            default:
                // 兜底：未接线的状态不能沿用上一状态残留的计时，否则下一帧会立刻溢出跳走
                stateTimer = 0f;
                Debug.LogWarning($"[PlayerCombat] 未处理的状态迁移：{next}，stateTimer 已归零", this);
                break;
        }
    }
    private void UpdateIdle()
    {
        if (attackBuffered)
        {
            attackBuffered = false;
            bufferTimer = 0f;
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
                attackHitbox.Activate(currentAttack.damage);
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
                bufferTimer = 0f;
                comboStep++;
                // StartAttack 内部已经完成 ChangeState(Attack)，这里不能再调一次
                StartAttack();
            }
            else
            {
                // 连招终止：必须清掉残留缓冲，否则回到 Idle 的瞬间会自动接出下一轮第一段
                attackBuffered = false;
                bufferTimer = 0f;
                comboStep = 0;
                ChangeState(CombatState.Idle);
            }
        }
    }
    private void CheckAttackInput()
    {
        if (Mouse.current == null) return;
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            attackBuffered = true;
            bufferTimer = inputBufferDuration;
        }
    }
    private void UpdateInputBuffer()
    {
        if (!attackBuffered) return;

        bufferTimer -= Time.deltaTime;
        if (bufferTimer <= 0f)
        {
            attackBuffered = false;
            bufferTimer = 0f;
        }
    }



}
