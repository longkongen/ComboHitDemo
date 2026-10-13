using UnityEngine;
using UnityEngine.InputSystem;
[DefaultExecutionOrder(-100)]
public class PlayerInputSource : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;   // 拖 Assets/InputSystem_Actions.inputactions

    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction lookAction;

    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }             // 本渲染帧的鼠标/右摇杆位移（增量）
    public bool AttackPressed { get; private set; }        // 本渲染帧按下过
                                                           

    private void Awake()
    {
        if (actions == null) { Debug.LogError("[PlayerInputSource] actions 未接线", this); enabled = false; return; }
        InputActionMap map = actions.FindActionMap("Player", true);   // true = 找不到直接抛，别静默
        moveAction = map.FindAction("Move", true);
        attackAction = map.FindAction("Attack", true);
        lookAction = map.FindAction("Look", true);
    }

    private void OnEnable() { moveAction?.Enable(); attackAction?.Enable(); lookAction?.Enable(); }
    private void OnDisable() { moveAction?.Disable(); attackAction?.Disable(); lookAction?.Disable(); }



    private void Update()      // 只采样，不做逻辑（纪律 #1）
    {
        if (moveAction == null) return;
        Move = moveAction.ReadValue<Vector2>();
        Look = lookAction.ReadValue<Vector2>();
        if (attackAction.WasPressedThisFrame()) AttackPressed = true;
    }

    public void ConsumeAttack() { AttackPressed = false; }
}
