// ============================================================
// ChargeProjectile.cs  v1.1
// 차징 투사체 구현체
//
// [v1.1 변경]
//   _shieldLayer 필드 추가 (EnemyShield 레이어).
//   OnTriggerEnter2D 에서 EnemyShield 레이어 감지 시
//   막힘 처리 분기 추가:
//     HitFeedback.PlayerAttackBlocked() 호출
//     투사체 Die() — 방패에 막혀 소멸
//
//   [기존 _enemyLayer 설정 오류 수정 안내]
//     Prefab Inspector 에서 _enemyLayer = Enemy(15) 레이어로 재설정 필요.
//     기존 값 262144 = Layer 18 = EnemyShield 로 잘못 설정되어 있음.
//
// [충돌 처리 분기 — v1.1]
//   EnemyShield 레이어 → 방패 막힘 → HitFeedback + Die
//   Enemy 레이어       → HandleEnemyHit (LockComponent or IDamageable)
//   Ground/Wall        → Die
//   기타               → 무시 (관통)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 차징 투사체 구현체. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [충돌 처리]
    ///   EnemyShield 레이어 → 방패 막힘 피드백 + 소멸 (v1.1 추가)
    ///   Enemy 레이어       → LockComponent.TakeDamage() or IDamageable
    ///   Ground / Wall      → 즉시 소멸
    ///   lifetime 초과      → 자동 소멸
    ///
    /// [Inspector 주의]
    ///   _enemyLayer = Enemy 레이어 (Layer 15)
    ///   _shieldLayer = EnemyShield 레이어 (Layer 18)
    ///   두 레이어를 반드시 분리 설정할 것.
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class ChargeProjectile : MonoBehaviour, IChargeProjectile
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 이동 설정 ──────────────────────")]

        [Tooltip("기본 투사체 속도. 차징 비율로 최대 maxSpeedMultiplier 배까지 증가.")]
        [Min(1f)]
        [SerializeField] private float _baseSpeed = 10f;

        [Tooltip("최대 속도 배율. chargePower=1 시 baseSpeed × 이 값.")]
        [Range(1f, 3f)]
        [SerializeField] private float _maxSpeedMultiplier = 2f;

        [Tooltip("투사체 최대 생존 시간 (초). 초과 시 자동 소멸.")]
        [Min(0.5f)]
        [SerializeField] private float _projectileLifetime = 3f;

        [Header("── 충돌 레이어 ──────────────────────")]

        /// <summary>
        /// 적 본체 레이어. Enemy 레이어 (Layer 15) 선택.
        /// 명중 시 LockComponent or IDamageable 처리.
        /// </summary>
        [Tooltip("적 본체 레이어. Enemy 레이어 선택.")]
        [SerializeField] private LayerMask _enemyLayer;

        /// <summary>
        /// 방패 레이어. EnemyShield 레이어 (Layer 18) 선택.
        /// 감지 시 방패 막힘 피드백 + 투사체 소멸.
        ///
        /// [방패 차단 원리]
        ///   ShieldCollider.isTrigger=OFF → 물리적으로 투사체 통과 차단.
        ///   그러나 CircleCollider2D(isTrigger=ON) vs BoxCollider2D(isTrigger=OFF)
        ///   조합에서는 OnTriggerEnter2D 가 발생할 수도 있음.
        ///   코드 레벨에서 명시적으로 막힘 처리하여 확실히 차단.
        /// </summary>
        [Tooltip("방패 레이어. EnemyShield 레이어 선택. 감지 시 막힘 피드백 + 소멸.")]
        [SerializeField] private LayerMask _shieldLayer;

        [Tooltip("지형 레이어 (Ground + Wall). 충돌 시 즉시 소멸.")]
        [SerializeField] private LayerMask _terrainLayer;

        [Header("── DOTween 피드백 ──────────────────────")]

        [Tooltip("발사 시 크기 Punch 강도. 차징 비율에 비례.")]
        [Min(0f)]
        [SerializeField] private float _launchPunchStrength = 0.5f;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _isDying;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            _rigid2D.gravityScale = 0f;
            _rigid2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnDestroy()
        {
            DOTween.Kill(transform);
        }

        // ══════════════════════════════════════════════════════
        // IChargeProjectile 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 투사체 발사.
        /// velocity 설정 → DOTween 발사 피드백 → lifetime 코루틴 시작.
        /// </summary>
        public void Launch(Vector2 direction, float chargePower)
        {
            float speed = Mathf.Lerp(_baseSpeed, _baseSpeed * _maxSpeedMultiplier, chargePower);
            _rigid2D.linearVelocity = direction * speed;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            float punchStr = _launchPunchStrength * (0.5f + chargePower * 0.5f);
            transform.DOPunchScale(
                    Vector3.one * punchStr,
                    duration: 0.2f,
                    vibrato: 3,
                    elasticity: 0.5f)
                .SetEase(Ease.OutQuart);

            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.Lerp(Color.white, Color.yellow, chargePower);

            StartCoroutine(LifetimeRoutine());
        }

        // ══════════════════════════════════════════════════════
        // 충돌 처리
        // ══════════════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDying) return;

            int otherLayer = 1 << other.gameObject.layer;

            // ── EnemyShield 레이어 → 방패에 막힘 ──────────────
            if (_shieldLayer.value != 0 && (_shieldLayer.value & otherLayer) != 0)
            {
                HandleShieldBlocked(other);
                Die();
                return;
            }

            // ── Enemy 레이어 → 적 명중 ──────────────────────
            if ((_enemyLayer.value & otherLayer) != 0)
            {
                HandleEnemyHit(other);
                Die();
                return;
            }

            // ── 지형 충돌 → 소멸 ────────────────────────────
            if ((_terrainLayer.value & otherLayer) != 0)
            {
                Die();
            }
        }

        // ══════════════════════════════════════════════════════
        // 방패 막힘 처리 (v1.1 추가)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 방패에 막혔을 때 처리.
        /// HitFeedback.PlayerAttackBlocked() 호출 후 투사체 소멸.
        ///
        /// [효과]
        ///   방패: 파란 플래시 + DOShakePosition
        ///   투사체: 반발 DOPunchScale 후 소멸
        /// </summary>
        private void HandleShieldBlocked(Collider2D shieldCol)
        {
            var shieldSr = shieldCol.GetComponent<SpriteRenderer>();

            // 투사체 자체 반발 효과 — 막히는 순간 임팩트 스케일
            transform.DOPunchScale(
                    Vector3.one * 0.3f,
                    duration: 0.1f,
                    vibrato: 4,
                    elasticity: 0.2f)
                .SetEase(Ease.OutQuart);

            // 방패 + 무기 반발 피드백
            HitFeedback.PlayerAttackBlocked(
                shieldSr,
                shieldCol.transform,
                transform,
                _rigid2D.linearVelocity.normalized);

            Debug.Log($"[ChargeProjectile] 방패에 막힘: {shieldCol.name}");
        }

        // ══════════════════════════════════════════════════════
        // 적 명중 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 적 명중 처리.
        /// LockComponent 우선 탐색 → 없으면 IDamageable 일반 피격.
        /// </summary>
        private void HandleEnemyHit(Collider2D other)
        {
            var lockComp = other.GetComponentInChildren<LockComponent>()
                        ?? other.GetComponentInParent<LockComponent>();

            if (lockComp != null)
            {
                var info = new DamageInfo(
                    attackerPosition: transform.position,
                    amount: 0f,
                    direction: _rigid2D.linearVelocity.normalized,
                    attackType: AttackType.Combo1);

                lockComp.TakeDamage(info);
                Debug.Log($"[ChargeProjectile] 자물쇠 피격: {other.name}");
                return;
            }

            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                var info = new DamageInfo(
                    attackerPosition: transform.position,
                    amount: 10f,
                    direction: _rigid2D.linearVelocity.normalized,
                    attackType: AttackType.Combo1);

                damageable.TakeDamage(info);
            }
        }

        // ══════════════════════════════════════════════════════
        // 소멸
        // ══════════════════════════════════════════════════════

        private void Die()
        {
            if (_isDying) return;
            _isDying = true;

            _rigid2D.linearVelocity = Vector2.zero;

            transform.DOScale(Vector3.zero, 0.1f)
                .SetEase(Ease.InQuart)
                .OnComplete(() =>
                {
                    if (gameObject != null)
                        Destroy(gameObject);
                });
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(_projectileLifetime);
            Die();
        }
    }
}