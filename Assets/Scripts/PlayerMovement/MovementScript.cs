using UnityEngine;

public class MovementScript : MonoBehaviour
{
    public float speed;
    public float jumpForce;

    public Rigidbody rb;

    private float verticalInput;
    private float horizontalInput;

    Vector3 moveDirection;
    public Transform orientation;

    [SerializeField] private float playerHeight;
    public LayerMask whatIsGround;
    bool grounded;

    [SerializeField] private float crouchHeight;
    [SerializeField] private float standHeight;

    public KeyCode crouchKey = KeyCode.LeftControl;



    void Start()
    {

    }

    void Update()
    {
        grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight + 0.3f, whatIsGround);

        MyInput();
        Inputs();
        //Crouch();
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    void MyInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        verticalInput = Input.GetAxisRaw("Vertical");
    }

    void Inputs()
    {
        if(Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }

        
        
    }

    void MovePlayer()
    {

        moveDirection = orientation.forward * verticalInput + orientation.right * horizontalInput;
        Vector3 targetPos = rb.position + moveDirection * speed * Time.deltaTime;
        rb.MovePosition(targetPos);

        
        
        if (Input.GetKey(crouchKey))
        {
            transform.position = new Vector3(targetPos.x, crouchHeight, targetPos.z);
            speed = 2;
        }
        else
        {
            transform.position = new Vector3(targetPos.x, targetPos.y, targetPos.z);
            speed = 5;
        }

    }

    void Jump()
    {
        if(grounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
    void Crouch()
    {
        
    }
}
