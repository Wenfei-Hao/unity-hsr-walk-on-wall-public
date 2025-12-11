using UnityEngine;

/// <summary>
/// 适配“墙上行走 / 球面行走”的第三人称相机。
/// 功能：
/// * 鼠标左右旋转镜头（Yaw）
/// * 鼠标上下抬头/低头（Pitch）
/// * 鼠标滚轮拉近/拉远（Zoom）
/// * 相机 up 始终对齐到 Player 的 up（即“当地重力的反方向”）
///
/// 挂在 CameraRig 上，Main Camera 作为其子物体。
/// </summary>
public class WalkOnWallCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;              // 玩家根节点：PlayerRoot

    [Header("Orbit")]
    public float distance      = 5f;      // 相机到角色的基础距离
    public float minDistance   = 2f;      // 最小距离（滚轮缩放下限）
    public float maxDistance   = 8f;      // 最大距离（滚轮缩放上限）
    public float heightOffset  = 1.5f;    // 整体抬高一点，让角色不要在屏幕正中心

    [Header("Mouse")]
    public float mouseSensitivityX = 3f;  // 鼠标左右灵敏度
    public float mouseSensitivityY = 3f;  // 鼠标上下灵敏度
    public float zoomSpeed         = 5f;  // 滚轮缩放速度

    [Tooltip("俯仰角范围（单位：度），负数是向下看，正数是向上看")]
    public float minPitch = -40f;         // 最低可以俯视多少度
    public float maxPitch = 75f;          // 最高可以仰视多少度

    [Header("Smoothing")]
    public float followSpeed   = 10f;     // 相机位置跟随平滑
    public float upAlignSpeed  = 8f;      // 相机 up 对齐 Player.up 的平滑速度

    // 内部状态
    private float yaw;                    // 绕 up 的水平角
    private float pitch;                  // 垂直俯仰角
    private Vector3 currentUp = Vector3.up;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("WalkOnWallCameraController: target 未设置！");
            enabled = false;
            return;
        }

        currentUp = target.up;

        // 初始化 yaw / pitch，尽量保持和当前相机视角接近
        Vector3 camToTarget = (target.position + currentUp * heightOffset) - transform.position;
        if (camToTarget.sqrMagnitude < 0.001f)
        {
            // 如果一开始 CameraRig 就在玩家脚底，给一个默认位置
            transform.position = target.position 
                                 - target.forward * distance 
                                 + target.up * heightOffset;
            camToTarget = (target.position + currentUp * heightOffset) - transform.position;
        }

        Vector3 lookDir = camToTarget.normalized; // 从相机看向目标的方向

        // 计算“参考前方向”（在当前 up 的切平面内）
        Vector3 basisForward = GetBasisForward(currentUp);

        // 将视线在切平面上的投影，用来求 yaw
        Vector3 lookOnPlane = Vector3.ProjectOnPlane(lookDir, currentUp);
        if (lookOnPlane.sqrMagnitude < 0.0001f)
        {
            lookOnPlane = basisForward;
        }
        lookOnPlane.Normalize();

        // yaw：参考前方向 -> 当前视线投影 的角度（绕 up）
        yaw = Vector3.SignedAngle(basisForward, lookOnPlane, currentUp);

        // pitch：切平面内方向 -> 实际视线 的角度（绕 right 轴）
        Vector3 right = Vector3.Cross(currentUp, lookOnPlane).normalized;
        pitch = Vector3.SignedAngle(lookOnPlane, lookDir, right);
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void Update()
    {
        if (target == null) return;

        // 1. 鼠标控制 yaw / pitch
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw   += mouseX * mouseSensitivityX;
        pitch -= mouseY * mouseSensitivityY;  // 鼠标往上推 -> 抬头，常见做法是减号
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // 2. 鼠标滚轮控制缩放
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            distance -= scroll * zoomSpeed;
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 相机的 up 逐渐对齐到 Player 的 up（适应墙 / 球面）
        Vector3 targetUp = target.up;
        currentUp = Vector3.Slerp(currentUp, targetUp, upAlignSpeed * Time.deltaTime);

        // 2. 计算一个在当前 up 切平面内的“基础前方向”
        Vector3 basisForward = GetBasisForward(currentUp);

        // 3. 绕 up 做 yaw 旋转，得到水平方向
        Quaternion yawRot = Quaternion.AngleAxis(yaw, currentUp);
        Vector3 yawForward = yawRot * basisForward;

        // 4. 求 right 轴，再绕 right 做 pitch 旋转
        Vector3 right = Vector3.Cross(currentUp, yawForward).normalized;
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, right);

        // 最终视线方向（从相机看向角色）
        Vector3 lookDir = pitchRot * yawForward;
        lookDir.Normalize();

        // 5. 相机理想位置 = 角色位置 + 抬高一点 - 往 lookDir 反方向退 distance
        Vector3 targetPos = target.position + currentUp * heightOffset;
        Vector3 desiredPos = targetPos - lookDir * distance;

        // 6. 平滑插值到该位置
        transform.position = Vector3.Lerp(transform.position, desiredPos, followSpeed * Time.deltaTime);

        // 7. 相机朝向：看向角色，up 使用 currentUp
        transform.rotation = Quaternion.LookRotation(lookDir, currentUp);
    }

    /// <summary>
    /// 在给定的 up 下，取一个稳定的“前方向”作为 yaw 的参考基准。
    /// 避免 up 和世界 forward 共线时退化。
    /// </summary>
    private Vector3 GetBasisForward(Vector3 up)
    {
        // 先尝试用世界 forward
        Vector3 f = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (f.sqrMagnitude < 0.001f)
        {
            // 如果碰巧 up 和 forward 几乎平行，就改用右方向
            f = Vector3.ProjectOnPlane(Vector3.right, up);
        }
        return f.normalized;
    }
}
