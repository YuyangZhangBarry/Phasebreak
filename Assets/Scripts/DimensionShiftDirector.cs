using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;

/// <summary>
/// 维度切换的视觉编排器。订阅 <see cref="ModeSwitcher.OnDimensionChanged"/>，
/// 统一驱动 Hitstop、后处理脉冲、Cinemachine Impulse、模型形变、粒子爆发、屏幕闪白、无敌帧。
/// 挂在与 ModeSwitcher 相同的 GameObject 上。
/// 所有需要的子系统（Volume、Canvas、ImpulseSource）会在运行时自动创建，无需手动配置。
/// </summary>
[RequireComponent(typeof(ModeSwitcher))]
public class DimensionShiftDirector : MonoBehaviour
{
    [Header("模型形变")]
    [Tooltip("包含模型/贴图的子物体。只对该物体做缩放形变，避免破坏根节点碰撞盒。若留空则使用自身 transform。")]
    public Transform graphicsRoot;

    [Header("无敌帧")]
    [Tooltip("玩家的 Health 组件。若留空则自动搜索。")]
    public Health playerHealth;
    [Tooltip("切换时是否授予短暂无敌帧。")]
    public bool grantIFramesOnSwitch = true;
    [Tooltip("无敌帧持续时间（游戏秒）。")]
    public float iFrameDuration = 0.2f;

    [Header("过渡锁定")]
    [Tooltip("切换后 F 键被屏蔽的最短真实时间（秒）。应覆盖 Cinemachine 相机混合的完整时长。")]
    [SerializeField] private float minTransitionLockDuration = 1.2f;

    [Header("Hitstop — 2D → 3D")]
    [Tooltip("切入 3D 时的完全冻结时长（真实秒）。")]
    [SerializeField] private float freezeDurationTo3D = 0.04f;
    [Tooltip("切入 3D 时从冻结恢复到正常的渐变时长（真实秒）。")]
    [SerializeField] private float rampDurationTo3D = 0.12f;

    [Header("Hitstop — 3D → 2D")]
    [SerializeField] private float freezeDurationTo2D = 0.025f;
    [SerializeField] private float rampDurationTo2D = 0.10f;

    [Header("后处理脉冲")]
    [Tooltip("Chromatic Aberration 峰值。")]
    [SerializeField] private float caPeak = 0.8f;
    [Tooltip("Lens Distortion 峰值（负值 = 桶形畸变）。")]
    [SerializeField] private float ldPeak = -0.35f;
    [Tooltip("Vignette 峰值。")]
    [SerializeField] private float vignettePeak = 0.45f;
    [Tooltip("Post Exposure 峰值（EV）。")]
    [SerializeField] private float exposurePeak = 0.6f;

    [Header("Cinemachine Impulse")]
    [Tooltip("切入 3D 时的屏幕震动冲量。")]
    [SerializeField] private Vector3 impulseVelocityTo3D = new Vector3(0f, -0.4f, 0f);
    [Tooltip("切入 2D 时的屏幕震动冲量。")]
    [SerializeField] private Vector3 impulseVelocityTo2D = new Vector3(0f, 0.3f, 0.1f);

    [Header("模型 Squash-Stretch")]
    [Tooltip("切入 3D 时的初始 Y 轴缩放比例（极端压缩态）。")]
    [SerializeField] private float squashStartTo3D = 0.3f;
    [Tooltip("切入 2D 时的初始 Y 轴缩放比例（极端压扁态）。")]
    [SerializeField] private float squashStartTo2D = 0.4f;

    [Header("屏幕闪白")]
    [Tooltip("闪白峰值透明度。")]
    [SerializeField] private float peakFlashAlpha = 0.25f;
    [Tooltip("闪白的指数衰减速率（越大衰减越快）。")]
    [SerializeField] private float flashDecayRate = 25f;
    [Tooltip("切入 3D 时的闪光颜色。")]
    [SerializeField] private Color flashColorTo3D = new Color(1f, 0.85f, 0.5f, 1f);
    [Tooltip("切入 2D 时的闪光颜色。")]
    [SerializeField] private Color flashColorTo2D = new Color(0.5f, 0.85f, 1f, 1f);

    [Header("粒子爆发")]
    [Tooltip("粒子 Burst 数量。")]
    [SerializeField] private int burstCount = 25;
    [Tooltip("切入 3D 时的粒子颜色。")]
    [SerializeField] private Color particleColorTo3D = new Color(1f, 0.7f, 0.2f, 1f);
    [Tooltip("切入 2D 时的粒子颜色。")]
    [SerializeField] private Color particleColorTo2D = new Color(0.3f, 0.8f, 1f, 1f);

    private ModeSwitcher modeSwitcher;
    private Volume transitionVolume;
    private CinemachineImpulseSource impulseSource;
    private Image screenFlashImage;
    private Coroutine transitionCoroutine;

    private void Awake()
    {
        modeSwitcher = GetComponent<ModeSwitcher>();

        if (playerHealth == null)
            playerHealth = GetComponent<Health>();

        EnsureTransitionVolume();
        EnsureImpulseSource();
        EnsureScreenFlash();
    }

    private void OnEnable()
    {
        if (modeSwitcher != null)
            modeSwitcher.OnDimensionChanged += HandleDimensionChanged;
    }

    private void OnDisable()
    {
        if (modeSwitcher != null)
        {
            modeSwitcher.OnDimensionChanged -= HandleDimensionChanged;
            modeSwitcher.isTransitioning = false;
        }

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (transitionVolume != null)
            transitionVolume.weight = 0f;
    }

    private void HandleDimensionChanged(bool is2DMode)
    {
        modeSwitcher.isTransitioning = true;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(RunTransition(is2DMode));
    }

    private IEnumerator RunTransition(bool enteredIs2D)
    {
        float freezeDuration = enteredIs2D ? freezeDurationTo2D : freezeDurationTo3D;
        float rampDuration = enteredIs2D ? rampDurationTo2D : rampDurationTo3D;
        Vector3 impulseVel = enteredIs2D ? impulseVelocityTo2D : impulseVelocityTo3D;
        Color flashColor = enteredIs2D ? flashColorTo2D : flashColorTo3D;
        float squashStart = enteredIs2D ? squashStartTo2D : squashStartTo3D;

        // === 第 0 帧：所有层级瞬间启动 ===

        // Hitstop 冻结
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0.001f;

        // 后处理峰值
        if (transitionVolume != null)
            transitionVolume.weight = 1f;

        // Cinemachine Impulse
        if (impulseSource != null)
            impulseSource.GenerateImpulse(impulseVel);

        // 屏幕闪白
        if (screenFlashImage != null)
            screenFlashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, peakFlashAlpha);

        // 模型形变：瞬间 snap 到极端 pose
        Transform target = graphicsRoot != null ? graphicsRoot : transform;
        Vector3 originalScale = target.localScale;
        float invSqrt = 1f / Mathf.Sqrt(Mathf.Max(0.1f, squashStart));
        target.localScale = new Vector3(
            originalScale.x * invSqrt,
            originalScale.y * squashStart,
            originalScale.z * invSqrt);

        // 粒子爆发
        SpawnDimensionBurst(enteredIs2D);

        // 无敌帧
        if (grantIFramesOnSwitch && playerHealth != null)
            playerHealth.SetInvincibleUntil(Time.time + iFrameDuration);

        // === 冻结阶段 ===
        float freezeElapsed = 0f;
        while (freezeElapsed < freezeDuration)
        {
            freezeElapsed += Time.unscaledDeltaTime;

            FadeScreenFlash(flashColor, freezeElapsed, freezeDuration + rampDuration);

            yield return null;
        }

        // === 恢复阶段：timeScale 从 0 ease-out 到 1 ===
        float rampElapsed = 0f;
        while (rampElapsed < rampDuration)
        {
            rampElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(rampElapsed / rampDuration);
            float easeT = 1f - Mathf.Pow(1f - t, 3f);

            Time.timeScale = Mathf.Lerp(0f, 1f, easeT);
            Time.fixedDeltaTime = 0.02f * Mathf.Max(0.01f, Time.timeScale);

            if (transitionVolume != null)
                transitionVolume.weight = 1f - easeT;

            FadeScreenFlash(flashColor, freezeDuration + rampElapsed, freezeDuration + rampDuration);

            // 弹性回弹
            float elasticT = ElasticEaseOut(t);
            float scaleY = Mathf.LerpUnclamped(squashStart, 1f, elasticT);
            float scaleXZ = 1f / Mathf.Sqrt(Mathf.Max(0.1f, scaleY));
            target.localScale = new Vector3(
                originalScale.x * scaleXZ,
                originalScale.y * scaleY,
                originalScale.z * scaleXZ);

            yield return null;
        }

        // === 视觉清理（果汁结束，但锁定可能仍在） ===
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (transitionVolume != null)
            transitionVolume.weight = 0f;

        if (screenFlashImage != null)
            screenFlashImage.color = new Color(1f, 1f, 1f, 0f);

        target.localScale = originalScale;

        // === 等待最小锁定时长（覆盖 Cinemachine 相机混合） ===
        float totalElapsed = freezeDuration + rampDuration;
        while (totalElapsed < minTransitionLockDuration)
        {
            totalElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transitionCoroutine = null;

        if (modeSwitcher != null)
            modeSwitcher.NotifyShiftComplete();
    }

    private void FadeScreenFlash(Color baseColor, float elapsed, float totalDuration)
    {
        if (screenFlashImage == null)
            return;

        float alpha = peakFlashAlpha * Mathf.Exp(-elapsed * flashDecayRate);
        screenFlashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Max(0f, alpha));
    }

    #region 运行时自动创建子系统

    private void EnsureTransitionVolume()
    {
        if (transitionVolume != null)
            return;

        GameObject volumeGo = new GameObject("DimensionTransitionVolume");
        volumeGo.transform.SetParent(transform, false);

        transitionVolume = volumeGo.AddComponent<Volume>();
        transitionVolume.isGlobal = true;
        transitionVolume.priority = 100;
        transitionVolume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var ca = profile.Add<ChromaticAberration>();
        ca.intensity.Override(caPeak);

        var ld = profile.Add<LensDistortion>();
        ld.intensity.Override(ldPeak);

        var vignette = profile.Add<Vignette>();
        vignette.intensity.Override(vignettePeak);

        var colorAdj = profile.Add<ColorAdjustments>();
        colorAdj.postExposure.Override(exposurePeak);

        transitionVolume.profile = profile;
    }

    private void EnsureImpulseSource()
    {
        impulseSource = GetComponent<CinemachineImpulseSource>();
        if (impulseSource == null)
            impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
    }

    private void EnsureScreenFlash()
    {
        if (screenFlashImage != null)
            return;

        GameObject canvasGo = new GameObject("DimensionFlashCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imgGo = new GameObject("FlashImage");
        imgGo.transform.SetParent(canvasGo.transform, false);

        screenFlashImage = imgGo.AddComponent<Image>();
        RectTransform rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        screenFlashImage.color = new Color(1f, 1f, 1f, 0f);
        screenFlashImage.raycastTarget = false;
    }

    #endregion

    #region 粒子爆发

    private void SpawnDimensionBurst(bool to2D)
    {
        Color color = to2D ? particleColorTo2D : particleColorTo3D;

        GameObject go = new GameObject("DimensionBurst");
        go.transform.position = transform.position + Vector3.up * 1f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 0.35f;
        main.startSpeed = 7f;
        main.startSize = 0.12f;
        main.maxParticles = burstCount + 5;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = color;
        main.playOnAwake = false;
        main.loop = false;
        main.gravityModifier = 0.3f;

        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            Material mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            renderer.material = mat;
        }

        ps.Play();
        Destroy(go, 1.5f);
    }

    #endregion

    #region 缓动函数

    private static float ElasticEaseOut(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        const float p = 0.3f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - p / 4f) * (2f * Mathf.PI) / p) + 1f;
    }

    #endregion
}
