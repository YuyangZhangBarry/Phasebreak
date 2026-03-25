using UnityEngine;

/// <summary>
/// Straight-line enemy projectile: no homing. Initialized via <see cref="Setup"/>.
/// Prefab: trigger Collider + Rigidbody (kinematic, no gravity) for reliable triggers vs static world.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class EnemyBullet : MonoBehaviour
{
    [Tooltip("World units per second along fire direction.")]
    public float speed = 20f;

    [Tooltip("Set by Setup() after Instantiate.")]
    private float _damage;

    [Tooltip("Seconds before auto-destroy (pool-friendly alternative: return to pool).")]
    public float lifeTime = 5f;

    [Tooltip("Layers treated as solid environment (walls / ground) in addition to Tag Environment.")]
    [SerializeField] private LayerMask environmentLayers;

    private Vector3 _direction;
    private bool _initialized;
    private float _spawnTime;

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        if (c != null)
            c.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    /// <summary>Call immediately after Instantiate. Direction is normalized internally.</summary>
    public void Setup(Vector3 fireDirection, float damage)
    {
        if (fireDirection.sqrMagnitude < 0.0001f)
            fireDirection = Vector3.forward;

        _direction = fireDirection.normalized;
        _damage = Mathf.Max(0f, damage);
        _initialized = true;
        _spawnTime = Time.time;
    }

    private void Update()
    {
        if (!_initialized)
            return;

        if (Time.time - _spawnTime >= lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += _direction * (speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
            return;

        if (other.CompareTag("Player"))
        {
            Health h = other.GetComponentInParent<Health>();
            if (h == null)
                h = other.GetComponent<Health>();
            if (h != null && h.IsAlive)
                h.TakeDamage(_damage);

            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Environment"))
        {
            Destroy(gameObject);
            return;
        }

        if (environmentLayers.value != 0 && ((1 << other.gameObject.layer) & environmentLayers.value) != 0)
            Destroy(gameObject);
    }
}
