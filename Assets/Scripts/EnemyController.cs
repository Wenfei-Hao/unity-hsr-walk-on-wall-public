using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

public class EnemyController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;          // PlayerRoot

    [Header("Components")]
    public NavMeshAgent agent;        // NavMeshAgent
    public Animator animator;         // 敌人 Animator（在 EnemyModel 上）

    [Header("Sign UI")]
    public Transform signRoot;        // 头顶的 SignRoot（世界空间 Canvas 的父节点）
    public Image questionBaseImage;
    public Image signImage;           // Canvas 下的 Image（Type = Filled Vertical）
    public Sprite questionSprite;     // 问号 png
    public Sprite exclamationSprite;  // 叹号 png

    [Header("Sign Colors")]
    public Color baseQuestionColor = Color.white;   // 问号开始颜色
    public Color fillQuestionColor   = Color.yellow; // 问号填满颜色
    public Color exclamationColor   = Color.red;    // 叹号颜色

    [Header("Chase Settings")]
    public float detectDistance = 10f;          // 进入这个距离才可能被发现
    public float loseDistance   = 12f;          // 超过这个距离就算丢失
    [Range(-1f, 1f)]
    public float samePlaneDotThreshold = 0.3f;  // 平面一致阈值（玩家和敌人 up 的点积）
    [Range(-1f, 1f)]
    public float fovDotThreshold       = 0.5f;  // 视野夹角阈值（大约 60°）

    [Header("Alarm Settings")]
    public float chargeSpeed = 0.5f;           // 看到玩家时，问号填充速度 / 秒
    public float decaySpeed  = 1.0f;           // 看不见玩家时，问号退回速度 / 秒

    [Range(0f, 1f)]
    public float alarmValue = 0f;              // 0~1 填充进度
    [Range(0, 2)]
    public int alarmLevel = 0;                 // 0=完全没发现,1=问号阶段,2=叹号阶段

    // 内部状态
    bool isFollowPlayer = false;
    Vector3 initialPosition;

    void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        initialPosition = transform.position;

        // 初始化 Sign：一开始隐藏
        if (questionBaseImage != null)
        {
            questionBaseImage.enabled = false;
            if (questionSprite != null)
                questionBaseImage.sprite = questionSprite;
            questionBaseImage.color = baseQuestionColor;
        }

        if (signImage != null)
        {
            signImage.fillAmount = 0f;
            signImage.enabled = false;
            if (questionSprite != null)
                signImage.sprite = questionSprite;
            signImage.color = fillQuestionColor;
        }
    }

    void Update()
    {
        if (player == null || agent == null) return;

        UpdateAlarmAndSign();
        UpdateMovement();

        // Billboard：让图标永远面向相机
        if (signRoot != null && Camera.main != null)
        {
            signRoot.rotation = Camera.main.transform.rotation;
        }
    }

    /// <summary>
    /// 更新警戒等级 & 问号/叹号 UI。
    /// </summary>
    void UpdateAlarmAndSign()
    {
        // 计算玩家是否“正在被看到”
        Vector3 toPlayer = player.position - transform.position;
        float distance   = toPlayer.magnitude;
        Vector3 dirToPlayer = (distance > 0.001f) ? toPlayer.normalized : Vector3.zero;

        bool samePlane = Vector3.Dot(player.up, transform.up) > samePlaneDotThreshold;
        bool inDistance = distance < detectDistance;
        bool inFront = Vector3.Dot(dirToPlayer, transform.forward) > fovDotThreshold;
        bool canSeePlayer = samePlane && inDistance && inFront;

        switch (alarmLevel)
        {
            // 完全安静，没发现玩家
            case 0:
                isFollowPlayer = false;

                if (canSeePlayer)
                {
                    // 开始怀疑：进入问号阶段
                    alarmLevel = 1;
                    alarmValue = 0f;
                    ShowQuestionSign();
                }
                break;

            // 问号阶段：填充/退回
            case 1:
                isFollowPlayer = false;

                if (canSeePlayer)
                {
                    alarmValue += chargeSpeed * Time.deltaTime;
                    if (alarmValue >= 1f)
                    {
                        alarmValue = 1f;
                        alarmLevel = 2;
                        ShowExclamationSign();
                    }
                }
                else
                {
                    alarmValue -= decaySpeed * Time.deltaTime;
                    if (alarmValue <= 0f)
                    {
                        alarmValue = 0f;
                        alarmLevel = 0;
                        HideSign();
                    }
                }

                alarmValue = Mathf.Clamp01(alarmValue);
                UpdateQuestionSignVisual();
                break;

            // 叹号阶段：追击中
            case 2:
                isFollowPlayer = true;

                // 玩家离开平面 or 跑太远 -> 降级回问号阶段
                bool tooFar       = distance > loseDistance;
                bool notSamePlane = Vector3.Dot(player.up, transform.up) < samePlaneDotThreshold;

                if (tooFar || notSamePlane)
                {
                    alarmLevel = 1;
                    // 可以保留当前 alarmValue（比如仍然是满格问号）
                    ShowQuestionSign();
                    UpdateQuestionSignVisual();
                    isFollowPlayer = false;
                }
                break;
        }
    }

    /// <summary>
    /// 根据 isFollowPlayer 在 NavMesh 上追击玩家或者回原点，并驱动 IsChase 动画。
    /// </summary>
    void UpdateMovement()
    {
        Vector3 targetPos = isFollowPlayer ? player.position : initialPosition;

        if (agent.destination != targetPos)
        {
            agent.SetDestination(targetPos);
        }

        bool isIdle = false;

        if (!agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                isIdle = true;
            }
        }

        if (isIdle)
        {
            agent.isStopped = true;

            if (animator != null)
            {
                // 敌人停下：回到 Idle 动画
                animator.SetBool("IsChase", false);
            }
        }
        else
        {
            agent.isStopped = false;

            if (animator != null)
            {
                // 敌人在路上：追击/走路动画
                animator.SetBool("IsChase", true);
            }
        }
    }

    #region Sign 辅助方法
    // 进入问号阶段时调用
    void ShowQuestionSign()
    {
        if (questionBaseImage != null)
        {
            questionBaseImage.enabled = true;
            questionBaseImage.sprite = questionSprite;
            questionBaseImage.color  = baseQuestionColor;
        }

        if (signImage != null)
        {
            signImage.enabled = true;
            signImage.sprite = questionSprite;
            signImage.color  = fillQuestionColor;
            signImage.fillAmount = alarmValue;     // 刚进入时通常是 0
        }
    }

    //问号填满->叹号
    void ShowExclamationSign()
    {
        // 叹号阶段不再显示底层问号
        if (questionBaseImage != null)
        {
            questionBaseImage.enabled = false;
        }

        if (signImage != null)
        {
            signImage.enabled = true;
            signImage.sprite = exclamationSprite;
            signImage.color  = exclamationColor;
            signImage.fillAmount = 1f;            // 叹号直接满格
        }
    }

    // 完全丢失（alarmLevel 回到 0）时调用
    void HideSign()
    {
        if (questionBaseImage != null)
            questionBaseImage.enabled = false;
        if (signImage != null)
            signImage.enabled = false;
    }

    // 问号阶段的视觉更新：只改“黄色填充层”
    void UpdateQuestionSignVisual()
    {
        if (signImage == null) return;
        if (!signImage.enabled) return;
        if (alarmLevel != 1) return; // 只在问号阶段更新

        signImage.fillAmount = alarmValue;
        // 这里不再 Lerp 颜色，直接用 fillQuestionColor
        signImage.color = fillQuestionColor;
    }

    #endregion
}
