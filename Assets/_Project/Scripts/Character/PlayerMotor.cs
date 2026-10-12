using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMotor : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -20f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector2 moveInput;      // 渲染帧采样，逻辑帧消费

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
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
        Move();
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
}