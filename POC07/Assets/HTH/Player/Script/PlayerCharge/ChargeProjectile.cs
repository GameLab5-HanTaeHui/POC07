// ============================================================
// ChargeProjectile.cs  v1.0
// 차징 투사체 구현체
//
// [역할]
//   IChargeProjectile 구현체.
//   Launch() 호출 시 지정 방향으로 직진.
//   충돌 처리:
//     Enemy 레이어  → LockComponent 기능 잠금 (자물쇠 기능 봉인)
//     Ground/Wall   → 즉시 소멸
//     기타          → 무시 (관통)
//   projectileLifetime 초 경과 후 자동 소멸.
//
// [Prefab 구조]
//   ChargeProjectile (Prefab)
//   ├── [ChargeProjectile]
//   ├── [Rigidbody2D]       GravityScale=0 / Collision=Continuous
//   ├── [CircleCollider2D]  isTrigger=true
//   └── [SpriteRenderer]    투사체 스프라이트 (추후 연결)
//
// [DOTween 활용]
//   발사 시 DOScale 로 투사체 크기 punch 효과.
//   chargePower 에 따라 크기 비율 변화.
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
    /// 차징 투사체 구현체. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [충돌 처리]
    ///   Enemy 레이어 명중 → LockComponent.LockFunction() 호출 (자물쇠 기능 잠금)
    ///   Ground / Wall 충돌 → 즉시 소멸
    ///   lifetime 초과 → 자동 소멸
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

        /// <summary>
        /// 기본 투사체 속도 (units/s).
        /// chargePower 에 따라 baseSpeed ~ baseSpeed * maxSpeedMultiplier 범위.
        /// </summary>
        [Tooltip("기본 투사체 속도. 차징 비율로 최대 maxSpeedMultiplier 배까지 증가.")]
        [Min(1f)]
        [SerializeField] private float _baseSpeed = 10f;

        /// <summary>
        /// 최대 속도 배율.
        /// chargePower=1 일 때 baseSpeed × 이 값이 최대 속도.
        /// </summary>
        [Tooltip("최대 속도 배율. chargePower=1 시 baseSpeed × 이 값.")]
        [Range(1f, 3f)]
        [SerializeField] private float _maxSpeedMultiplier = 2f;

        /// <summary>
        /// 투사체 최대 생존 시간 (초).
        /// 이 시간 초과 시 아무것도 맞히지 못해도 소멸.
        /// </summary>
        [Tooltip("투사체 최대 생존 시간 (초). 초과 시 자동 소멸.")]
        [Min(0.5f)]
        [SerializeField] private float _projectileLifetime = 3f;

        [Header("── 충돌 레이어 ──────────────────────")]

        /// <summary>
        /// 적 감지 레이어. 명중 시 LockComponent 기능 잠금 적용.
        /// </summary>
        [Tooltip("적 레이어. 명중 시 LockComponent 기능 잠금.")]
        [SerializeField] private LayerMask _enemyLayer;

        /// <summary>
        /// 지형 레이어 (Ground + Wall).
        /// 충돌 시 투사체 즉시 소멸.
        /// </summary>
        [Tooltip("지형 레이어 (Ground + Wall). 충돌 시 즉시 소멸.")]
        [SerializeField] private LayerMask _terrainLayer;

        [Header("── DOTween 피드백 ──────────────────────")]

        /// <summary>
        /// 발사 시 크기 Punch 강도.
        /// chargePower 에 비례하여 증가.
        /// </summary>
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

        /// <summary> 이미 소멸 처리 중 플래그. 중복 소멸 방지. </summary>
        private bool _isDying;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 물리 설정
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
        /// velocity 설정 → DOTween 발사 피드백 → lifetime 소멸 코루틴 시작.
        /// </summary>
        /// <param name="direction">발사 방향 (정규화)</param>
        /// <param name="chargePower">차징 비율 0~1</param>
        public void Launch(Vector2 direction, float chargePower)
        {
            // ① 속도 설정 — 차징 비율에 따라 baseSpeed ~ baseSpeed * maxSpeedMultiplier
            float speed = Mathf.Lerp(_baseSpeed, _baseSpeed * _maxSpeedMultiplier, chargePower);
            _rigid2D.linearVelocity = direction * speed;

            // ② 투사체 방향 회전 (스프라이트 방향 맞춤)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            // ③ DOTween 발사 피드백 — 크기 Punch (차징 강할수록 더 크게)
            float punchStr = _launchPunchStrength * (0.5f + chargePower * 0.5f);
            transform.DOPunchScale(
                    Vector3.one * punchStr,
                    duration: 0.2f,
                    vibrato: 3,
                    elasticity: 0.5f)
                .SetEase(Ease.OutQuart);

            // ④ 차징 비율에 따른 색상 — 약 = 흰색, 강 = 노란색
            if (_spriteRenderer != null)
                _spriteRenderer.color = Color.Lerp(Color.white, Color.yellow, chargePower);

            // ⑤ 생존 시간 코루틴
            StartCoroutine(LifetimeRoutine());
        }

        // ══════════════════════════════════════════════════════
        // 충돌 처리
        // ══════════════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isDying) return;

            int otherLayer = 1 << other.gameObject.layer;

            // ── 적 명중 ──────────────────────
            if ((_enemyLayer.value & otherLayer) != 0)
            {
                HandleEnemyHit(other);
                Die();
                return;
            }

            // ── 지형(Ground / Wall) 충돌 ──────────────────────
            if ((_terrainLayer.value & otherLayer) != 0)
            {
                Die();
            }
        }

        // ══════════════════════════════════════════════════════
        // 적 명중 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 적 명중 처리.
        /// LockComponent 를 찾아 기능 잠금 적용.
        ///
        /// [자물쇠 기능 잠금 연동]
        ///   적 오브젝트 또는 자식에서 LockComponent 탐색.
        ///   LockComponent.TakeDamage(DamageInfo) 호출.
        ///   자물쇠가 없는 적(EnemyDummy 등)은 IDamageable 일반 피격.
        /// </summary>
        private void HandleEnemyHit(Collider2D other)
        {
            // LockComponent 가 있으면 자물쇠 피격
            var lockComp = other.GetComponentInChildren<LockComponent>()
                           ?? other.GetComponentInParent<LockComponent>();

            if (lockComp != null)
            {
                var info = new DamageInfo(
                    attackerPosition: transform.position,
                    amount: 0f,   // 자물쇠는 데미지 수치 무관 (횟수 기반)
                    direction: _rigid2D.linearVelocity.normalized,
                    attackType: AttackType.Combo1 // 추후 ChargeShotType 으로 확장
                );
                lockComp.TakeDamage(info);
                Debug.Log($"[ChargeProjectile] 자물쇠 피격: {other.name}");
                return;
            }

            // LockComponent 없으면 일반 피격
            if (other.TryGetComponent<IDamageable>(out var damageable))
            {
                var info = new DamageInfo(
                    attackerPosition: transform.position,
                    amount: 10f,
                    direction: _rigid2D.linearVelocity.normalized,
                    attackType: AttackType.Combo1
                );
                damageable.TakeDamage(info);
            }
        }

        // ══════════════════════════════════════════════════════
        // 소멸
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 투사체 소멸.
        /// DOTween 축소 후 Destroy.
        /// 중복 호출 방지: _isDying 플래그.
        /// </summary>
        private void Die()
        {
            if (_isDying) return;
            _isDying = true;

            _rigid2D.linearVelocity = Vector2.zero;

            // DOTween 소멸 연출 — 빠르게 축소 후 제거
            transform.DOScale(Vector3.zero, 0.1f)
                .SetEase(Ease.InQuart)
                .OnComplete(() =>
                {
                    if (gameObject != null)
                        Destroy(gameObject);
                });
        }

        /// <summary>
        /// 생존 시간 초과 시 소멸 코루틴.
        /// </summary>
        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(_projectileLifetime);
            Die();
        }
    }
}