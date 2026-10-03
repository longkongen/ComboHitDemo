using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMotor : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -20f;
    private CharacterController controller;
    private Vector3 velocity;
    private void Move()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed)
        {
            input.y = 1f;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            input.y = -1f;
        }
        if (Keyboard.current.aKey.isPressed)
        {
            input.x = -1f;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            input.x = 1f;
        }
        Vector3 moveDirection = new Vector3(input.x,0f,input.y).normalized;
        controller.Move(moveDirection*moveSpeed*Time.deltaTime);

    }
    private void ApplyGravity()
    {
        if(controller.isGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;

        }
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);

    }
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        
    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Move();
        ApplyGravity();
    }
}
