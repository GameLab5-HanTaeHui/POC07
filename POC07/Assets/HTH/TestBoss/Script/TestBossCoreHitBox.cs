using KEY;
using UnityEngine;

// Core 오브젝트에 부착
public class TestBossCoreHitbox : MonoBehaviour
{
    [SerializeField] private TestBossCore _core;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<PlayerWeaponHitboxManager>(out _))
        {
            var info = new DamageInfo(other.transform.position, 30f,
                (transform.position - other.transform.position).normalized,
                AttackType.Combo1);
            _core.TakeDamage(info);
        }
    }
}
