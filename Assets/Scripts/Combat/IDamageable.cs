/// <summary>
/// Minimal damage contract for any entity that can receive damage.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damageAmount, bool ignoreInvincibility = false);
}
