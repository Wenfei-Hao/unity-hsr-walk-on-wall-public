using UnityEngine;

/// <summary>
/// 让角色在平面 / 曲面 / 墙面上行走的玩家控制器（Rigidbody 版本）
/// 挂在 PlayerRoot 上
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WalkOnWallPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3.5f;          // 移动速度
    public float turnSpeed = 10f;           // 转向插值速度

    [Header("Gravity")]
    public float gravity = 25f;             // 假重力加速度
    public float groundCheckDistance = 1.2f;// 射线检测地面的距离
    public LayerMask groundMask = ~0;       // 地面 Layer（默认全部）

    public float alignToGroundSpeed = 10f;  // 对齐地面法线的平滑速度

    [Header("Animation")]
    public string walkBoolName = "IsWalk";  // Animator 里 bool 参数名
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private Vector3 currentUp;              // 当前“身体向上”方向
    private Vector2 moveInput;              // 输入（x,z）
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;             // 禁用 Unity 自带重力
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (animator == null)
        {
            // 默认在子物体上找 Animator（比如 Orc 模型上）
            animator = GetComponentInChildren<Animator>();
        }

        currentUp = transform.up;
    }

    void Update()
    {
        // --- 1. 读取输入（旧版 Input System：Horizontal / Vertical）---
        float h = Input.GetAxisRaw("Horizontal"); // A / D 或 左 / 右
        float v = Input.GetAxisRaw("Vertical");   // W / S 或 前 / 后

        moveInput = new Vector2(h, v);

        // --- 2. 更新动画状态 ---
        if (animator != null && !string.IsNullOrEmpty(walkBoolName))
        {
            bool isWalking = moveInput.sqrMagnitude > 0.01f;
            animator.SetBool(walkBoolName, isWalking);
        }
    }

    void FixedUpdate()
    {
        UpdateGroundAndOrientation();
        ApplyMovement();
        ApplyGravity();
    }

    /// <summary>
    /// 射线检测地面 + 对齐角色 Up 到地面法线
    /// </summary>
    void UpdateGroundAndOrientation()
    {
        RaycastHit hit;
        // 从当前角色位置沿着 -currentUp 方向打射线
        if (Physics.Raycast(transform.position, -currentUp, out hit, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;

            // 目标 up 是地面法线
            Vector3 targetUp = hit.normal;

            // 根据 currentUp -> targetUp 构建旋转
            Quaternion toSurface = Quaternion.FromToRotation(currentUp, targetUp);
            Quaternion targetRotation = toSurface * transform.rotation;

            // 用 Slerp 平滑对齐，避免瞬间旋转
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                alignToGroundSpeed * Time.fixedDeltaTime
            );

            // 更新 currentUp，为之后的移动 / 重力使用
            currentUp = transform.up;
        }
        else
        {
            isGrounded = false;
            //（可选）脱离地面时，可以慢慢往世界 up 纠正，
            // 这里只是简单保持 currentUp 不变
        }
    }

    /// <summary>
    /// 在当前“切平面”上移动（相对相机或世界坐标）
    /// </summary>
    void ApplyMovement()
    {
        if (moveInput.sqrMagnitude < 0.0001f)
        {
            return; // 没有输入就不移动
        }

        // --- 1. 确定前 / 右方向（参考相机，如果有的话） ---
        Vector3 forward;
        Vector3 right;

        if (Camera.main != null)
        {
            forward = Camera.main.transform.forward;
            right   = Camera.main.transform.right;
        }
        else
        {
            // 如果场景里没有标记 MainCamera，就用世界 Z / X 轴
            forward = Vector3.forward;
            right   = Vector3.right;
        }

        // 把前 / 右向量投影到当前切平面上（法线是 currentUp）
        forward = Vector3.ProjectOnPlane(forward, currentUp).normalized;
        right   = Vector3.ProjectOnPlane(right,   currentUp).normalized;

        // --- 2. 组合输入方向 ---
        Vector3 desiredMoveDir = forward * moveInput.y + right * moveInput.x;
        if (desiredMoveDir.sqrMagnitude > 1f)
        {
            desiredMoveDir.Normalize();
        }

        // --- 3. 让角色朝移动方向转过去（保持 currentUp 为 Up） ---
        Quaternion targetRot = Quaternion.LookRotation(desiredMoveDir, currentUp);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            turnSpeed * Time.fixedDeltaTime
        );

        // --- 4. 沿着切平面移动（Rigidbody.MovePosition 保持物理稳定） ---
        Vector3 moveDelta = desiredMoveDir * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + moveDelta);
    }

    /// <summary>
    /// 自定义重力：永远沿 -currentUp 方向
    /// </summary>
    void ApplyGravity()
    {
        Vector3 gravityDir = -currentUp;

        // 沿着“脚下”方向施加一个持续的加速度
        rb.AddForce(gravityDir * gravity, ForceMode.Acceleration);

        // 在踩着地面时，稍微处理一下沿地面法线的速度，避免抖动
        if (isGrounded)
        {
            Vector3 vel = rb.velocity;
            Vector3 alongUp = Vector3.Project(vel, currentUp);

            // 如果速度是朝“地里钻”的方向，就去掉这一部分
            if (Vector3.Dot(alongUp, currentUp) < 0f)
            {
                vel -= alongUp;
                rb.velocity = vel;
            }
        }
    }
}
