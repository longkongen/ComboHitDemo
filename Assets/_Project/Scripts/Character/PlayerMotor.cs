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
    [SerializeField] private float turnSensitivity = 0.2f;
    [SerializeField] private PlayerInputSource input;
    private float yaw;
    private float pendingYaw;
    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        // 不靠手拖：同一个根物体上的组件自解析，引用不会漂移（纪律 #9 的本意）
        if (actor == null) actor = GetComponent<CombatActor>();
        if (actor == null) Debug.LogError("[PlayerMotor] 缺 CombatActor，硬直期间锁不住移动", this);

        // 输入源同样是同物体自解析：Prefab 里漏拖 input 时 WASD 会整体失灵，必须响亮地报出来
        if (input == null) input = GetComponent<PlayerInputSource>();
        if (input == null) Debug.LogError("[PlayerMotor] missing PlayerInputSource -> WASD will not move", this);

        yaw = transform.eulerAngles.y;
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
        moveInput = (input != null) ? input.Move : Vector2.zero;
        // 关键：鼠标位移是"增量"，必须累加，否则 483 fps 时一帧只消费 1/8 的位移
        if (input != null) pendingYaw += input.Look.x * turnSensitivity;
    }

    private void OnCombatTick(int frame)
    {
        // 硬直 / 死亡 / 防御期间：锁水平位移（重力与击退照常，见下面两行）
        bool canMove = (actor == null) || (actor.State != CombatState.Hit
                                        && actor.State != CombatState.Dead
                                        && actor.State != CombatState.Guard);
        ApplyFacing(canMove);   // 硬直/死亡期间转身也被锁，并丢弃这段时间累积的位移
        if (canMove) Move();
        ApplyKnockback();
        ApplyGravity();
    }

    private void ApplyFacing(bool canMove)
    {
        if (canMove) yaw += pendingYaw;
        pendingYaw = 0f;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);   // 只用 yaw 重建，不要 Rotate 累加误差
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