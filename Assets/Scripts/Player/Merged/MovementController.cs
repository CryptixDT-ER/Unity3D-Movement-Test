/*  Rant 1 - 28-31.5.2025 -
    Iteration 5 of this script because I can't decide if I want these functions merged or separate
    I discovered regions and am now going to use them EVERYWHERE!!! I LOVE BEING ORGANIZED!!!!!!!!!!
    Got plenty more ideas for cool movement and ZERO idea how to implement them but what is programming if not the process of breaking shit
    First rant up here since I decided I want to keep track of dumb things I do (Gotta let future me make fun of current me)
    Speaking of dumb things at some point I broke the run speed boost so yup gotta fix that
    Sorry in advance to future me for taking such a daunting task as a complete C# beginner
    Rant over 
*/

using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour
{

    #region Variables
    [Header("General")]
    public float maxSpeed = 150f;
    public float gravity = 20.0f;
    [SerializeField] private bool canMove = true;
    
    [Header("Walk, Run, [P]Slide")]
    public float walkingSpeed = 7.5f;
    public float walkAcceleration = 14f;

    public float runningSpeed = 11.5f;
    public float runAcceleration = 20f;
    public float runSpeedBoost = 1.5f;
    public float runSpeedBoostDecay = 2f;
    public float runSpeedBoostRampUp = 10f;
    public float runSpeedBoostDuration = 0.5f;

    // Reminder to add Sliding logic later (slideDuration, boolean slidingDownwards(Continue sliding while downwards rather than counting down a timer))
    
    [Header("Jump, Vault, [P]WallRun, [P]WallJump")]
    public float jumpSpeed = 8.0f;
    public float jumpHelpDuration = 0.5f;
    public float minFastFallHeight = 10f;

    // TODO: Figure out why 0.3f in minimum vault height is larger than a 3 meter block in world height like what
    public float minVaultHeight = 0.3f;
    public float vaultDetectionDistance = 3f;
    public LayerMask vaultLayerMask = -1;

    // Reminder to add WallRun logic later (wallRunDuration, wallRunSpeed(Accelerate forward based on downward momentum))

    // Reminder to add WallJump logic later (wallJumpTimer, wallJumpBounce(How far the player should bounce if jumping off a wall(Maybe based on camera angle?)))
    
    [Header("Camera Config")]
    public Camera playerCamera;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 85.0f;
    public float runTilt = 5f;
    public float tiltSpeed = 5f;

    // Reminder to set up camera shake
    
    // Component refs
    private CharacterController characterController;
    private MovementDebugger debugger;
    private CapsuleCollider capsuleCollider;
    
    // Various states
    private Vector3 moveDirection = Vector3.zero;
    private Vector2 inputVector;
    private bool isMoving;
    private bool isRunning;
    private bool isCrouching;
    private bool isSliding;
    
    // Walking/Running
    [SerializeField] private float currentSpeed = 0f;
    [SerializeField] private float currentSpeedBoost = 1f;
    [SerializeField] private float runSpeedBoostTimer = 0f;
    [SerializeField] private bool wasRunning = false;
    //slide
    
    // Jumping state
    [SerializeField] private float jumpHelpTimer = 0f;
    [SerializeField] private bool hasJumped = false;
    [SerializeField] private float lastGroundDistance = 0f;
    //wallrun, walljump
    
    // Camera state
    [SerializeField] private float rotationX = 0;
    [SerializeField] private float currentTilt = 0f;
    [SerializeField] private float targetTilt = 0f;
    //camshake intensity
    #endregion

    #region Updates
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        debugger = GetComponent<MovementDebugger>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        // Probably don't need both capsule collider and character controller but my goofy ahh did it like this from the start
        // and I am now too afraid of breaking everything if I change it
        // TODO: Get better I guess

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        HandleInput();
        Movement();
        HandleCameraTilt();
        HandleDebugging();
        
        // Apply final movement
        characterController.Move(moveDirection * Time.deltaTime);
    }
    
    void LateUpdate()
    {
        if (canMove)
        {
            HandleMouseLook();
        }
    }
    #endregion
    
    #region Inputs
    private void HandleInput()
    {
        if (!canMove) return;
        
        float inputX = Input.GetAxis("Vertical");
        float inputY = Input.GetAxis("Horizontal");
        
        inputVector = new Vector2(inputX, inputY);
        if (inputVector.magnitude > 1f)
            inputVector = inputVector.normalized;
        
        isMoving = inputVector.magnitude > 0.1f;
        isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving && !isCrouching;
        isCrouching = Input.GetKey(KeyCode.LeftControl);
        isSliding = isRunning && isCrouching;

    }
    #endregion
    
    #region Movement System
    private void Movement()
    {
        // Determine movement state and apply appropriate movement
        if (IsGrounded)
        {
            // Check for vault when grounded and running
            if (isRunning && Input.GetKey(KeyCode.E) && CanVault())
            {   
                Vault();
                return;
            }
            
            // Handle ground-based movement states (Probably not the best way to do this because scalability but ehhhh I like being organized and it looks pretty)
            // runSpeedBoostTimer currently reset when not running because the logic is flawed in some way I can't figure out
            // This is a crude solution until I decide to try and fix it again
            if (isSliding)
            {
                runSpeedBoostTimer = 0f;
                Slide();
            }
            else if (isCrouching)
            {
                runSpeedBoostTimer = 0f;
                Crouch();
            }
            else if (isRunning)
            {
                Run();
            }
            else if (isMoving)
            {
                runSpeedBoostTimer = 0f;
                Walk();
            }
            else
            {
                Idle();
            }
            
            // Handle jumping from grounded state
            if (Input.GetKeyDown(KeyCode.Space) && canMove)
            {
                Jump();
            }
            else
            {
                moveDirection.y = -1f; // Grounded state (Setting it to 0 for some reason destroys Unity's collision detection)
                hasJumped = false;
                jumpHelpTimer = 0f;
            }
        }
        else
        {
            AirMovement();
            Jump();
        }
    }
    
    private void Walk()
    {
        Vector3 targetDirection = GetMoveDirection(walkingSpeed);
        ApplyHorizontalMovement(targetDirection, walkAcceleration);
        UpdateSpeedTracking(walkingSpeed, walkAcceleration);
    }
    
    private void Run()
    {
        UpdateRunningBoost();
        float finalSpeed = runningSpeed * currentSpeedBoost;
        Vector3 targetDirection = GetMoveDirection(finalSpeed);
        ApplyHorizontalMovement(targetDirection, runAcceleration);
        UpdateSpeedTracking(runningSpeed, runAcceleration);
    }
    
    private void Crouch()
    {
        float crouchSpeed = walkingSpeed / 2f;
        float crouchAcceleration = walkAcceleration / 2f;
        Vector3 targetDirection = GetMoveDirection(crouchSpeed);
        ApplyHorizontalMovement(targetDirection, crouchAcceleration);
        UpdateSpeedTracking(crouchSpeed, crouchAcceleration);
    }
    
    private void Slide()
    {
        // TODO: Implement sliding mechanics
        // Treating as crouching for now
        Crouch();
    }
    
    private void Idle()
    {
        Vector3 targetDirection = Vector3.zero;
        ApplyHorizontalMovement(targetDirection, walkAcceleration * 1.5f);
        UpdateSpeedTracking(0f, walkAcceleration);
    }
    
    private void AirMovement()
    {
        // Maintain horizontal movement control in air
        float airSpeed = isRunning ? runningSpeed * currentSpeedBoost : walkingSpeed;
        Vector3 targetDirection = GetMoveDirection(airSpeed);
        ApplyHorizontalMovement(targetDirection, walkAcceleration * 0.5f); // Reduced air control specifically for air movement because TF2 does that and it feels nice
    }
    
    private Vector3 GetMoveDirection(float speed)
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        return (forward * inputVector.x * speed) + (right * inputVector.y * speed);
    }
    
    private void ApplyHorizontalMovement(Vector3 targetDirection, float accelerationRate)
    {
        Vector3 currentHorizontal = new Vector3(moveDirection.x, 0, moveDirection.z);
        Vector3 smoothedHorizontal = Vector3.Lerp(currentHorizontal, targetDirection, accelerationRate * Time.deltaTime);
        moveDirection = new Vector3(smoothedHorizontal.x, moveDirection.y, smoothedHorizontal.z);
    }
    
    private void UpdateSpeedTracking(float targetSpeed, float accelerationRate)
    {
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed*currentSpeedBoost, accelerationRate * Time.deltaTime);
        if(currentSpeed < 0.01f) currentSpeed = 0f; // This here so speed doesn't keep counting into the negative infinitely
    }
    
    private void UpdateRunningBoost()
    {
        if (isRunning)
        {
            if (!wasRunning)
            {
                currentSpeedBoost = Mathf.Lerp(currentSpeedBoost, runSpeedBoost, runSpeedBoostRampUp * Time.deltaTime);
            }
            else
            {
                runSpeedBoostTimer += Time.deltaTime;
                
                if (runSpeedBoostTimer < runSpeedBoostDuration)
                {
                    currentSpeedBoost = Mathf.Lerp(currentSpeedBoost, runSpeedBoost, runSpeedBoostRampUp * Time.deltaTime);
                }
                else
                {
                    currentSpeedBoost = Mathf.Lerp(currentSpeedBoost, 1f, runSpeedBoostDecay * Time.deltaTime);
                }
            }
        }
        else
        {
            currentSpeedBoost = Mathf.Lerp(currentSpeedBoost, 1f, runSpeedBoostDecay * Time.deltaTime);
        }
        
        wasRunning = isRunning;
    }
    #endregion
    
    #region Jumping
    private void Jump()
    {
        // If grounded, set jump status to false so player can jump again
        if (IsGrounded)
        {
            hasJumped = false;
            jumpHelpTimer = 0f;
            
            if (Input.GetKeyDown(KeyCode.Space) && canMove)
            {
                hasJumped = true;
                moveDirection.y = jumpSpeed;
            }
        }
        else
        {
            jumpHelpTimer += Time.deltaTime;
            float newYVelocity = moveDirection.y - gravity * Time.deltaTime;
            
            lastGroundDistance = GetDistanceToGround();
            
            // Coyote jump to solve annoying false-jumps
            if (Input.GetKeyDown(KeyCode.Space) && jumpHelpTimer <= jumpHelpDuration && !hasJumped)
            {
                hasJumped = true;
                jumpHelpTimer = jumpHelpDuration + 1f; // Immediately set timer past max
                moveDirection.y = jumpSpeed;
            }
            // Fast fall, sort of a ground slam but really underwhelming without camera shake and particles
            else if (hasJumped && Input.GetKeyDown(KeyCode.Space) && lastGroundDistance >= minFastFallHeight)
            {
                hasJumped = false;
                moveDirection.y = -maxSpeed / 3f;
            }
            else
            {
                moveDirection.y = newYVelocity;
            }
        }
    }
    #endregion
    
    #region Vaulting System
    private void Vault()
    {
        hasJumped = true;
        capsuleCollider.height /= 8f;
        characterController.center = new Vector3(0f, 1f, 0f);
        capsuleCollider.center = new Vector3(0f, 1f, 0f); 
        Invoke(nameof(ResetCollider), 0.4f);
        
        float vaultHeight = GetVaultHeight();
        // v = sqrt(2 * g * h)
        moveDirection.y = Mathf.Sqrt(2f * gravity * vaultHeight);
    }
    
    private bool CanVault()
    {
        // Check if movementDebugger's Camera Ray has hit a surface
        if (!debugger.CameraRayHitSomething)
            return false;
           
        // Check if movementDebugger's Camera Ray is within range to allow Vaulting
        if (debugger.LastCameraRayDistance > vaultDetectionDistance)
            return false;

        // Check if the hit object has the Vaultable tag
        if (!debugger.LastCameraHit.collider.CompareTag("Vaultable"))
            return false;
           
        return GetObstacleHeight() >= minVaultHeight;
    }
   
    private float GetVaultHeight()
    {
        if (!CanVault()) return 0f;
        return GetObstacleHeight() + 0.5f; // Add small clearance to not clip the surface's hitbox
        // TODO: Fix the terrible vaulting logic, maybe disable collision temporarily as a whole, compensate tho
    }
   
    private float GetObstacleHeight()
    {
        if (!debugger.CameraRayHitSomething) return 0f;
       
        Vector3 hitPoint = debugger.LastCameraHit.point;
        Vector3 playerFeet = transform.position;
       
        // Raycast down from above the hit point to find the top of hit object, get its height for use in Vaulting
        Vector3 scanStart = hitPoint + Vector3.up * 5f;
        RaycastHit topHit;
       
        if (Physics.Raycast(scanStart, Vector3.down, out topHit, 10f, vaultLayerMask))
        {
            if (topHit.collider == debugger.LastCameraHit.collider)
            {
                return Mathf.Max(0f, topHit.point.y - playerFeet.y);
            }
        }
       
        // Fallback
        return Mathf.Max(0f, hitPoint.y - playerFeet.y);
    }

    private void ResetCollider()
    {
        // Reset the player's collider's and capsule's collider's hitboxes after Vaulting
        // Wait why do I have 2 colliders
        capsuleCollider.center = Vector3.Lerp(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 0f), 200f * Time.deltaTime);
        characterController.center = Vector3.Lerp(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 0f), 200f * Time.deltaTime);
        // OK I should've used moveTowards instead of Lerp but we ball I'll just fix it manually
        if (capsuleCollider.center.y <= 0f) {
            capsuleCollider.center = new Vector3(0f, 0f, 0f);
            characterController.center = new Vector3(0f, 0f, 0f);
        }
        capsuleCollider.height *= 8f;
    }
    #endregion
    
    #region Utility Methods
    private float GetDistanceToGround()
    {
        RaycastHit hit;
        float maxDistance = 100f;
        
        // Simply check how far up you are for use in the fast-fall ground slam
        if (Physics.Raycast(Position, Vector3.down, out hit, maxDistance))
        {
            return hit.distance;
        }
        
        return maxDistance;
    }
    //only one method in the region called utility methods :((((( one day
    #endregion
    
    #region Camera System
    private void HandleCameraTilt()
    {
        if (isRunning && isMoving)
        {
            targetTilt = inputVector.y * (runTilt * -1f);
        }
        else
        {
            targetTilt = inputVector.y * (runTilt * -0.5f);
        }
        
        // I call this the hell-yeah effect, camera tilting feels awesome
        // TODO: Add some vertical and horizontal camera-sway on top perhaps
        currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltSpeed * Time.deltaTime);
        if(currentTilt < 0.01f) currentTilt = 0f; // This here so camera tilt doesn't count into the negatives infinitely
    }
    
    private void HandleMouseLook()
    {
        rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
        
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, currentTilt);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
    }
    #endregion
    
    #region Debugging
    private void HandleDebugging()
    {
        debugger?.UpdateDebug();
    }
    #endregion
    
    #region Compatibility stuff
    public bool IsGrounded => characterController.isGrounded;
    public Vector3 Position => transform.position;
    public float MaxSpeed => maxSpeed;
    public float Gravity => gravity;
    
    // For debugger
    public float LastGroundDistance => lastGroundDistance;
    public bool HasJumped => hasJumped;
    public float MinFastFallHeight => minFastFallHeight;
    
    // Movement states
    public bool IsWalking => isMoving && !isRunning && !isCrouching && IsGrounded;
    public bool IsRunning => isRunning && IsGrounded;
    public bool IsCrouching => isCrouching && !isRunning && IsGrounded;
    public bool IsSliding => isSliding && IsGrounded;
    public bool IsInAir => !IsGrounded;
    #endregion
}