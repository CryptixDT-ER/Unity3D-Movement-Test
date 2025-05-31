using UnityEngine;

public class MovementDebugger : MonoBehaviour
{
    public bool showRaycasts = true;
    public float lineOffset = 0.0f;
    
    [Header("Camera Raycast")]
    public float cameraRaycastDistance = 50f;
    public LayerMask raycastLayerMask = -1; // Hit all layers by default
    
    private LineRenderer groundRaycast;
    private LineRenderer cameraRaycast;
    private Material redMaterial, greenMaterial, blueMaterial, yellowMaterial;
    private MovementController movementController;
    private Camera playerCamera;
    
    // Raycast hit info
    private RaycastHit lastCameraHit;
    private bool cameraRayHitSomething;
    private float lastCameraRayDistance;
    
    // Surface info
    private Vector3 surfaceNormal;
    private float surfaceAngle;
    private bool isWallSurface;
    private bool isFloorSurface;
    private bool isCeilingSurface;
    
    void Start()
    {
        movementController = GetComponent<MovementController>();
        
        // Get the player camera from the MovementController
        playerCamera = movementController.playerCamera;
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera == null)
            {
                Debug.LogWarning("MovementDebugger: No camera found. Camera raycast will not work.");
            }
        }
        
        CreateDebugMaterials();
        CreateGroundRaycasts();
    }
    
    public void UpdateDebug()
    {
        HandleDebugInput();
        UpdateDebugLines();
        UpdateCameraRaycast();
    }
    
    // Bunch'a debugging keybinds
    private void HandleDebugInput()
    {
        // Ground distance and fast fall / ground slam
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log($"Ground Distance: {movementController.LastGroundDistance:F2}m, Has Jumped: {movementController.HasJumped}");
        }
        // Ground raycast
        if (Input.GetKeyDown(KeyCode.H))
        {
            showRaycasts = !showRaycasts;
            Debug.Log($"Ground raycast {(showRaycasts ? "shown" : "hidden")}.");
        }
        // Surface info
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (cameraRayHitSomething)
            {
                Debug.Log($"Camera Raycast Hit: {lastCameraHit.collider.name} at distance {lastCameraRayDistance:F2}m");
                Debug.Log($"Surface Normal: {surfaceNormal}, Angle: {surfaceAngle:F1}°");
                Debug.Log($"Surface Type: {GetSurfaceTypeString()}");
                Debug.Log($"Hit Point: {lastCameraHit.point}");
            }
            else
            {
                Debug.Log($"Camera Raycast: No hit within {cameraRaycastDistance:F2}m");
            }
        }
        
        // Debug for movement states
        if (Input.GetKeyDown(KeyCode.M))
        {
            ShowMovementState();
        }
    }
    
    private void ShowMovementState()
    {
        string currentState = "Unknown";
        
        if (movementController.IsSliding)
            currentState = "Sliding";
        else if (movementController.IsCrouching)
            currentState = "Crouching";
        else if (movementController.IsRunning)
            currentState = "Running";
        else if (movementController.IsWalking)
            currentState = "Walking";
        else if (movementController.IsInAir)
            currentState = "In Air";
        else
            currentState = "Idle";
            
        Debug.Log($"Movement State: {currentState}");
        Debug.Log($"Grounded: {movementController.IsGrounded}");
    }
    
    private void UpdateDebugLines()
    {
        if (!showRaycasts || groundRaycast == null) return;
        
        Vector3 offset = transform.right * lineOffset;
        Vector3 startPos = transform.position + offset;
        Vector3 endPos = startPos + Vector3.down * movementController.LastGroundDistance;
        
        groundRaycast.SetPosition(0, startPos);
        groundRaycast.SetPosition(1, endPos);
        
        Material lineColor = movementController.LastGroundDistance >= movementController.MinFastFallHeight ? greenMaterial : redMaterial;
        groundRaycast.material = lineColor;
    }
    
    private void UpdateCameraRaycast()
    {
        if (!showRaycasts || cameraRaycast == null || playerCamera == null) return;
        
        Vector3 rayOrigin = playerCamera.transform.position;
        Vector3 rayDirection = playerCamera.transform.forward;
        
        // Perform the raycast
        cameraRayHitSomething = Physics.Raycast(rayOrigin, rayDirection, out lastCameraHit, cameraRaycastDistance, raycastLayerMask);
        
        Vector3 rayEnd;
        Material rayMaterial;
        
        if (cameraRayHitSomething)
        {
            rayEnd = lastCameraHit.point;
            lastCameraRayDistance = lastCameraHit.distance;
            
            // Analyze the surface
            SeeSurface();
            
            rayMaterial = RaycastMaterial();
        }
        else
        {
            rayEnd = rayOrigin + rayDirection * cameraRaycastDistance;
            lastCameraRayDistance = cameraRaycastDistance;
            rayMaterial = blueMaterial; // Blue for no hits
        }
        
        // Update line renderer
        cameraRaycast.SetPosition(0, rayOrigin);
        cameraRaycast.SetPosition(1, rayEnd);
        cameraRaycast.material = rayMaterial;
    }
    
    private void CreateDebugMaterials()
    {
        redMaterial = new Material(Shader.Find("Sprites/Default"));
        redMaterial.color = Color.red;
        
        greenMaterial = new Material(Shader.Find("Sprites/Default"));
        greenMaterial.color = Color.green;
        
        blueMaterial = new Material(Shader.Find("Sprites/Default"));
        blueMaterial.color = Color.blue;
        
        yellowMaterial = new Material(Shader.Find("Sprites/Default"));
        yellowMaterial.color = Color.yellow;
    }
    
    private void CreateGroundRaycasts()
    {
        // Ground debug line
        if (showRaycasts)
        {
            GameObject debugLineObj = new GameObject("Debug Raycast Line");
            debugLineObj.transform.parent = transform;
            groundRaycast = debugLineObj.AddComponent<LineRenderer>();
            groundRaycast.material = redMaterial;
            groundRaycast.startWidth = 0.06f;
            groundRaycast.endWidth = 0.06f;
            groundRaycast.positionCount = 2;
            groundRaycast.useWorldSpace = true;
        }
        
        // Camera raycast line
        if (showRaycasts && playerCamera != null)
        {
            GameObject cameraRayObj = new GameObject("Camera Raycast Line");
            cameraRayObj.transform.parent = transform;
            cameraRaycast = cameraRayObj.AddComponent<LineRenderer>();
            cameraRaycast.material = blueMaterial;
            cameraRaycast.startWidth = 0.04f;
            cameraRaycast.endWidth = 0.04f;
            cameraRaycast.positionCount = 2;
            cameraRaycast.useWorldSpace = true;
        }
    }
    
    private void SeeSurface()
    {
        if (!cameraRayHitSomething) return;
        
        surfaceNormal = lastCameraHit.normal;
        
        // Calculate angle between surface normal and up vector
        surfaceAngle = Vector3.Angle(surfaceNormal, Vector3.up);
        
        // Surface types based on angle
        // I can already tell this is gonna be janky when I get to giving surfaces specific angles
        // That is future me's problem though
        isFloorSurface = surfaceAngle <= 70f;                       // 0-70° = floor/ground
        isWallSurface = surfaceAngle > 70f && surfaceAngle < 135f;  // 70-135° = wall
        isCeilingSurface = surfaceAngle >= 135f;                    // 135-180° = ceiling
    }
    
    private Material RaycastMaterial()
    {
        if (isWallSurface) return redMaterial;          // Red for walls
        if (isFloorSurface) return blueMaterial;        // Blue for floors
        if (isCeilingSurface) return yellowMaterial;    // Yellow for ceilings
        return greenMaterial;                           // Green for other/unknown
    }
    
    private string GetSurfaceTypeString()
    {
        if (isFloorSurface) return "Floor";
        if (isWallSurface) return "Wall";
        if (isCeilingSurface) return "Ceiling";
        return "Unknown";
    }

    // Public gets for accessing raycast info from other scripts
    public bool CameraRayHitSomething => cameraRayHitSomething;
    public RaycastHit LastCameraHit => lastCameraHit;
    public float LastCameraRayDistance => lastCameraRayDistance;
    
    // Surface gets
    public Vector3 SurfaceNormal => surfaceNormal;
    public float SurfaceAngle => surfaceAngle;
    public bool IsWallSurface => isWallSurface;
    public bool IsFloorSurface => isFloorSurface;
    public bool IsCeilingSurface => isCeilingSurface;
}