using UnityEngine;

public class EnemyMotor : MonoBehaviour
{
    public float gravity = -20f;

    [SerializeField]
    private CombatActor actor;      // 同一个根物体上的战斗状态（被击退时读它）

    private CharacterController controller;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 不靠手拖：同一个根物体上的组件自解析（纪律 #9 的本意）
        if (actor == null) actor = GetComponent<CombatActor>();
        if (controller == null) Debug.LogError("[EnemyMotor] 缺 CharacterController，击退推不动", this);
        if (actor == null) Debug.LogError("[EnemyMotor] 缺 CombatActor，读不到击退速度", this);
    }

    // 订阅/退订必须成对：本工程禁用了 Domain Reload，漏退订会让一次 tick 走两遍
    private void OnEnable()
    {
        CombatClock.OnTick += OnCombatTick;
    }

    private void OnDisable()
    {
        CombatClock.OnTick -= OnCombatTick;
    }

    // 木桩没有走位需求，这一层只做“被推”和落地
    private void OnCombatTick(int frame)
    {
        if (controller == null) return;
        ApplyKnockback();
        ApplyGravity();
    }

    // 击退不受硬直锁限制：这正是击退的意义
    private void ApplyKnockback()
    {
        if (actor == null) return;
        Vector3 kb = actor.KnockbackVelocity;
        if (kb == Vector3.zero) return;
        controller.Move(kb * CombatClock.TickDelta);
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * CombatClock.TickDelta;
        controller.Move(velocity * CombatClock.TickDelta);
    }
}