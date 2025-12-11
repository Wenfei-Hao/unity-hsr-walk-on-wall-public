using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControl : MonoBehaviour
{
    [Header("移动参数")]
    public float moveSpeed = 5f;

    [Header("相机参数")]
    public float mouseXSpeed = 100f;
    public float mouseYSpeed = 200f;
    public float zoomSpeed   = 5f;
    public float minDistance = 2f;
    public float maxDistance = 10f;
    public float distance    = 5f;

    [Header("重力参数")]
    public float gravityStrength = 10f;
    public float groundCheckDistance = 2f;

    [Header("引用")]
    public Transform followCameraPos;   // 用来放相机的“跟随点”（一个空物体）
    public string walkBoolName = "IsWalk"; // 你的 Animator 参数名

    // 内部状态
    private float mouseX;
    private float mouseY;
    private Vector3 gravity;

    private Animator animator;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;                         // 禁用 Unity 自带重力
        rb.constraints = RigidbodyConstraints.FreezeRotation; // 只用我们自己算的旋转

        // 默认假设模型是第一个子物体
        if (transform.childCount > 0)
        {
            animator = transform.GetChild(0).GetComponent<Animator>();
        }
        else
        {
            Debug.LogWarning("PlayerControl: 找不到子物体上的 Animator，请手动在脚本中引用。");
        }

        // 如果没在 Inspector 里拖 followCameraPos，就在 Start 里创建一个
        if (followCameraPos == null)
        {
            GameObject camFollow = new GameObject("FollowCameraPos");
            followCameraPos = camFollow.transform;
        }
    }

    void Update()
    {
        HandleCamera();
        HandleMove();
        HandleGravityAndAlign();
    }

    /// <summary>
    /// 鼠标控制相机的绕角色旋转 + 滚轮缩放
    /// </summary>
    void HandleCamera()
    {
        // 鼠标滚轮缩放
        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 鼠标左右 / 上下
        mouseX += Input.GetAxis("Mouse X") * mouseXSpeed * Time.deltaTime;
        mouseY += Input.GetAxis("Mouse Y") * mouseYSpeed * Time.deltaTime;

        // 限制角度范围，避免抖动与翻转
        mouseX = Mathf.Repeat(mouseX + 180f, 360f) - 180f;  // 始终在 [-180,180]
        mouseY = Mathf.Clamp(mouseY, -12f, 60f);

        // 计算相机球坐标方向（局部空间）
        float x = Mathf.Sin(-mouseX * Mathf.Deg2Rad) * Mathf.Cos(mouseY * Mathf.Deg2Rad);
        float y = Mathf.Sin(mouseY * Mathf.Deg2Rad);
        float z = -Mathf.Cos(-mouseX * Mathf.Deg2Rad) * Mathf.Cos(mouseY * Mathf.Deg2Rad);

        // 玩家当前旋转决定“局部坐标系”
        Quaternion rotation = Quaternion.Euler(transform.eulerAngles);

        // 相机跟随点位置：玩家位置 + 旋转后的偏移 * 距离
        Vector3 offset = rotation * new Vector3(x, y, z) * distance;
        followCameraPos.position = transform.position + offset;

        // 看向玩家，up 使用当前 transform.up（适配墙/球面）
        followCameraPos.LookAt(transform.position, transform.up);

        // 把主相机的位置和朝向同步到跟随点
        if (Camera.main != null)
        {
            Camera.main.transform.position = followCameraPos.position;
            Camera.main.transform.rotation = followCameraPos.rotation;
        }
    }

    /// <summary>
    /// WASD 移动 + 模型朝向
    /// </summary>
    /*
    void HandleMove()
    {
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");

        if (Camera.main == null) return;

        // 局部右 / 前方向：基于相机 forward 和当前 up 计算
        Vector3 localRight = -Vector3.Cross(Camera.main.transform.forward, transform.up).normalized;
        Vector3 localForward = Vector3.Cross(localRight, transform.up).normalized;

        // 调试用射线（可删）
        Debug.DrawRay(transform.position, localForward, Color.blue);
        Debug.DrawRay(transform.position, localRight, Color.red);

        // 让模型（子物体）根据输入转向
        Transform model = (transform.childCount > 0) ? transform.GetChild(0) : null;

        if (model != null)
        {
            if (Mathf.Abs(verticalInput) > 0.01f)
            {
                float angle = Vector3.SignedAngle(
                    transform.forward,
                    localForward * verticalInput,
                    transform.up);
                model.localEulerAngles = new Vector3(0, angle, 0);
            }
            else if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                float angle = Vector3.SignedAngle(
                    transform.forward,
                    localRight * horizontalInput,
                    transform.up);
                model.localEulerAngles = new Vector3(0, angle, 0);
            }
        }

        // 根据模型 forward 移动 Player（根节点）
        Vector3 moveDir = Vector3.zero;
        if (model != null)
        {
            float moveAmount = Mathf.Clamp01(Mathf.Abs(verticalInput) + Mathf.Abs(horizontalInput));
            moveDir = model.forward * moveAmount * moveSpeed * Time.deltaTime;
            transform.position += moveDir;
        }

        // 动画切换（用你的参数名）
        if (animator != null && !string.IsNullOrEmpty(walkBoolName))
        {
            bool isWalking = Mathf.Abs(verticalInput) > 0.01f || Mathf.Abs(horizontalInput) > 0.01f;
            animator.SetBool(walkBoolName, isWalking);
        }

        // 调试移动向量
        Debug.DrawRay(transform.position, moveDir * 10f, Color.white);
    }
    */
    void HandleMove()
{
    // 1. 原来的输入，换成 AxisRaw，去掉平滑
    float verticalInput = Input.GetAxisRaw("Vertical");
    float horizontalInput = Input.GetAxisRaw("Horizontal");

    if (Camera.main == null) return;

    // 2. 基于当前相机和玩家 up 计算“局部前/右”（贴着曲面走）
    Vector3 localRight   = -Vector3.Cross(Camera.main.transform.forward, transform.up).normalized;
    Vector3 localForward =  Vector3.Cross(localRight, transform.up).normalized;

    // 3. 组合输入方向：允许同时前后 + 左右，得到一个平面上的 moveInput
    Vector3 moveInput =
        localForward * verticalInput +
        localRight   * horizontalInput;

    Transform model = (transform.childCount > 0) ? transform.GetChild(0) : null;

    Vector3 moveDir = Vector3.zero;

    if (moveInput.sqrMagnitude > 0.0001f)
    {
        // 归一化得到真正的移动方向（走斜线时自动 45°）
        moveDir = moveInput.normalized;

        // 4. 让模型朝向 moveDir （带一点转身插值会更顺眼）
        if (model != null)
        {
            Quaternion targetModelRot =
                Quaternion.LookRotation(moveDir, transform.up);

            // 你可以调整 10f 这个旋转速度
            model.rotation = Quaternion.Slerp(
                model.rotation,
                targetModelRot,
                10f * Time.deltaTime
            );
        }

        // 5. 移动根节点（沿曲面切线方向）
        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }

    // 6. 动画参数：有任意方向输入就切到 Walk
    if (animator != null && !string.IsNullOrEmpty(walkBoolName))
    {
        bool isWalking = moveInput.sqrMagnitude > 0.0001f;
        animator.SetBool(walkBoolName, isWalking);
    }

    // Debug：可以暂时保留看看方向是否正确
    Debug.DrawRay(transform.position, localForward, Color.blue);
    Debug.DrawRay(transform.position, localRight,   Color.red);
    Debug.DrawRay(transform.position, moveDir * 2f, Color.white);
}




    /// <summary>
    /// 射线检测地面法线 + 假重力 + 角色对齐曲面
    /// </summary>
    void HandleGravityAndAlign()
    {
        if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hitInfo, groundCheckDistance))
        {
            if (hitInfo.collider != null)
            {
                Vector3 normal = hitInfo.normal;
                gravity = -normal * gravityStrength;

                // 把 transform.up 对齐到法线方向
                Quaternion targetRotation = Quaternion.FromToRotation(transform.up, normal);
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    targetRotation * transform.rotation,
                    Time.deltaTime * 5f);
            }
        }

        // 沿“当前重力方向”施加加速度
        rb.AddForce(gravity, ForceMode.Acceleration);

        Debug.DrawRay(transform.position, gravity, Color.cyan);
    }
}
