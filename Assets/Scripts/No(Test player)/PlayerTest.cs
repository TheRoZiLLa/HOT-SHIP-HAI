using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed   = 6f;
    public float sprintSpeed = 10f;

    [Header("Jump")]
    public float jumpForce      = 7f;
    public float coyoteTime     = 0.15f;
    public float jumpBufferTime = 0.15f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundRadius = 0.35f;
    public LayerMask groundLayer;

    [Header("Gravity")]
    public float gravityStrength = 25f;

    [Header("Camera")]
    public Transform cameraTransform;

    [Header("Animation")]
    public Animator anim;

    [HideInInspector] public bool gravityFlipped = false;
    [HideInInspector] public int  gravityDir     = 1;

    Rigidbody rb;
    Vector3   moveDir;
    bool      isGrounded;
    float     coyoteTimer;
    float     jumpBufferTimer;
    bool      doJump;

    void Start()
    {
        rb                = GetComponent<Rigidbody>();
        rb.useGravity     = false;
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        Cursor.lockState  = CursorLockMode.Locked;
        Cursor.visible    = false;

        if (anim == null)
            anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        ReadMovementInput();
        CheckGround();
        HandleJumpInput();
        HandleGravityFlipInput();
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        ApplyGravity();
        ApplyMovement();

        if (doJump)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce * gravityDir, rb.linearVelocity.z);
            doJump = false;
        }
    }

    void ReadMovementInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        if (cameraTransform != null)
        {
            Vector3 fwd   = cameraTransform.forward; fwd.y = 0; fwd.Normalize();
            Vector3 right = cameraTransform.right;   right.y = 0; right.Normalize();
            moveDir = (fwd * z + right * x).normalized;
        }
        else
        {
            moveDir = new Vector3(x, 0, z).normalized;
        }
    }

    void CheckGround()
    {
        if (groundCheck == null) return;

        isGrounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundLayer);

        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;
    }

    void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer -= Time.deltaTime;

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            doJump          = true;
            jumpBufferTimer = 0f;
            coyoteTimer     = 0f;
        }
    }

    void HandleGravityFlipInput()
    {
        if (!Input.GetKeyDown(KeyCode.F)) return;

        gravityFlipped = !gravityFlipped;
        gravityDir     = gravityFlipped ? -1 : 1;

        transform.Rotate(0f, 0f, 180f, Space.Self);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    void ApplyGravity()
    {
        rb.AddForce(Vector3.up * (-gravityStrength * gravityDir), ForceMode.Acceleration);
    }

    void ApplyMovement()
    {
        float   speed   = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        Vector3 target  = moveDir * speed;
        Vector3 current = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 delta   = target - current;

        rb.AddForce(new Vector3(delta.x, 0f, delta.z) * 10f, ForceMode.Acceleration);

        if (moveDir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRot, 15f * Time.fixedDeltaTime);
        }
    }

    void UpdateAnimation()
    {
        if (anim == null) return;

        anim.SetFloat("Speed", moveDir.magnitude);
        anim.SetBool("isJump", !isGrounded);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundRadius);
    }
}