using System.Collections;
using UnityEngine;

/// <summary>
/// Floating eye: every attackCooldown seconds, snapshots player position once,
/// then fires bullets in a straight line along that direction.
/// 【已升级】：兼容精英怪大招锁 (isEliteSweeping)
/// </summary>
public class FloatingEyeAttack : MonoBehaviour
{
    [Header("Refs")]
    public Transform firePoint;
    public GameObject bulletPrefab;

    [Header("Damage")]
    public float bulletDamage = 8f;

    [Header("Burst")]
    public float firstAttackDelay = 3f;
    public float attackCooldown = 4f;
    public int bulletsPerBurst = 3;
    public float timeBetweenBullets = 0.15f;

    [Header("Optional facing")]
    public float rotateTowardPlayerSpeed = 45f;

    // 【新增】精英大招状态锁
    [HideInInspector] public bool isEliteSweeping = false;

    private Transform _player;
    private Coroutine _attackLoop;

    private void Start()
    {
        if (firePoint == null)
            firePoint = transform;

        ResolvePlayer();
        if (bulletPrefab != null)
            _attackLoop = StartCoroutine(AttackLoop());
    }

    private void OnDisable()
    {
        if (_attackLoop != null)
        {
            StopCoroutine(_attackLoop);
            _attackLoop = null;
        }
    }

    private void Update()
    {
        // 【核心修改】：如果精英怪正在放大招，停止追踪玩家的旋转！
        if (rotateTowardPlayerSpeed <= 0f || isEliteSweeping)
            return;

        if (_player == null)
            ResolvePlayer();

        if (_player == null)
            return;

        Vector3 to = _player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            target,
            rotateTowardPlayerSpeed * Time.deltaTime);
    }

    private void ResolvePlayer()
    {
        if (PlayerStatsManager.Instance != null)
        {
            _player = PlayerStatsManager.Instance.transform;
            return;
        }
        
        GameObject go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            _player = go.transform;
    }

    private IEnumerator AttackLoop()
    {
        var waitFirst = new WaitForSeconds(firstAttackDelay);
        var waitCooldown = new WaitForSeconds(attackCooldown);
        var waitBetween = new WaitForSeconds(timeBetweenBullets);

        yield return waitFirst;

        while (enabled)
        {
            // 【核心修改】：正在放大招时，普通攻击协程挂起等待，不发射子弹
            if (isEliteSweeping)
            {
                yield return null;
                continue;
            }

            if (_player == null)
                ResolvePlayer();

            if (_player != null && bulletPrefab != null)
            {
                Vector3 aimPoint = _player.position;
                Vector3 origin = firePoint.position;
                Vector3 dir = aimPoint - origin;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = transform.forward;
                else
                    dir.Normalize();

                for (int i = 0; i < bulletsPerBurst; i++)
                {
                    // 防止连发中途突然进入大招
                    if (isEliteSweeping) break;

                    GameObject go = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(dir));
                    EnemyBullet b = go.GetComponent<EnemyBullet>();
                    if (b != null)
                        b.Setup(dir, bulletDamage);
                    else
                        Debug.LogWarning("[FloatingEyeAttack] bulletPrefab needs EnemyBullet component.", this);

                    if (i < bulletsPerBurst - 1)
                        yield return waitBetween;
                }
            }

            yield return waitCooldown;
        }
    }
}