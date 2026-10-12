using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMotor : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -20f;

    [SerializeField]
    private CombatActor actor;      // 同一个根物体上的战斗状态（硬直 / 死亡时锁水平位移）

    private CharacterController controller;
    private Vector3 velocity;
    private Vector2 moveInput;      // 渲染帧采样，逻辑帧消费

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 不靠手拖：同一个根物体上的组件自解析，引用不会漂移（纪律 #9 的本意）
        if (actor == null) actor = GetComponent<CombatActor>();
        if (actor == null) Debug.LogError("[PlayerMotor] 缺 CombatActor，硬直期间锁不住移动", this);
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

    // 渲染帧只负责“采样输入”
    private void Update()
    {
        moveInput = Vector2.zero;
        if (Keyboard.current == null) return;

        if (Keyboard.current.wKey.isPressed) moveInput.y = 1f;
        if (Keyboard.current.sKey.isPressed) moveInput.y = -1f;
        if (Keyboard.current.aKey.isPressed) moveInput.x = -1f;
        if (Keyboard.current.dKey.isPressed) moveInput.x = 1f;
    }

    // 逻辑帧只负责“执行位移”，步长恒为 1/60 秒（顿帧期间 tick 不广播，位移自然冻结）
    private void OnCombatTick(int frame)
    {
        // 硬直 / 死亡期间不能水平移动；重力照常加，否则空中被打中会钉在天上
        bool canMove = (actor == null) || (actor.State != CombatState.Hit && actor.State != CombatState.Dead);
        if (canMove) Move();   // 输入位移：Hit / Dead 期间被锁
        ApplyKnockback();      // 击退：不受输入锁限制（这正是击退的意义）
        ApplyGravity();
    }

    private void Move()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        controller.Move(moveDirection * moveSpeed * CombatClock.TickDelta);
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
    private void ApplyKnockback()
    {
        if (actor == null) return;
        Vector3 kb = actor.KnockbackVelocity;
        if (kb == Vector3.zero) return;
        controller.Move(kb * CombatClock.TickDelta);
    }
}