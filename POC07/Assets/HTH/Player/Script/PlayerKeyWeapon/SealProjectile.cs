// ============================================================
// SealProjectile.cs  v2.0
// 봉인 열쇠 투사체 컴포넌트
//
// [v2.0 변경]
//   SealDataSO 참조 완전 제거 → KeyDataSO 참조로 통합.
//   Launch(SealDataSO, float) → Launch(KeyDataSO, float, float) 로 변경.
//   chargePower 파라미터 추가 — 투사체 속도/크기 비율에 사용.
//   봉인 수치는 KeyDataSO 의 seal* 필드에서 읽음.
//
// [충돌 분기]
//   EnemyShield 레이어 → 방패 막힘 피드백 + Expire()
//   Enemy 레이어       → SealComponent.ApplySeal(KeyDataSO) + Expire()
//   지형 레이어        → Expire()
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
    /// 봉인 투사체. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [Prefab 구조]
    ///   SealProjectile               Layer: PlayerHitbox
    ///   ├── [SealProjectile]
    ///   ├── [Rigidbody2D]    GravityScale=0
    ///   ├── [CircleCollider2D] isTrigger=ON
    ///   └── [SpriteRenderer]
    ///
    /// [Inspector 연결]
    ///   _sealLayer   = Enemy 레이어
    ///   _shieldLayer = EnemyShield 레이어
    ///   _terrainLayer = Ground + Wall 레이어
    ///
    /// [호출 흐름]
    ///   PlayerChargeAttack.Fire()
    ///     → Instantiate(chargeProjectilePrefab)
    ///     → SealProjectile.Launch(keyData, facingDir, chargePower)
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class SealProjectile : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 감지 레이어 ──────────────────────")]

        /// <summary>
        /// 봉인 적용 대상 레이어. Enemy 레이어 선택.
        /// 명중 시 SealComponent.ApplySeal() 호출.
        /// </summary>
        [Tooltip("봉인 적용 대상 레이어. Enemy 레이어 선택.")]
        [SerializeField] private LayerMask _sealLayer;

        /// <summary>
        /// 방패 레이어. EnemyShield 레이어 선택.
        /// 감지 시 방패 막힘 피드백 + 소멸.
        /// </summary>
        [Tooltip("방패 레이어. EnemyShield 레이어 선택. 감지 시 막힘 피드백 + 소멸.")]
        [SerializeField] private LayerMask _shieldLayer;

        /// <summary>
        /// 지형 레이어. Ground + Wall 레이어 선택.
        /// 충돌 시 즉시 소멸.
        /// </summary>
        [Tooltip("지형 레이어. Ground + Wall 레이어 선택.")]
        [SerializeField] private LayerMask _terrainLayer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 런타임 상태
        // ──────────────────────────────────────────

        private KeyDataSO _keyData;
        private bool _isActive;
        private Coroutine _lifetimeCoroutine;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _rigid2D.gravityScale = 0f;
        }

        private void OnDestroy()
        {
            DOTween.Kill(transform);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — PlayerChargeAttack 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 투사체 발사.
        /// PlayerChargeAttack.Fire() 에서 Instantiate 직후 호출.
        ///
        /// [파라미터]
        ///   keyData    : 현재 장착된 열쇠 데이터 (봉인 수치 포함)
        ///   direction  : 발사 방향 (+1 = 오른쪽, -1 = 왼쪽)
        ///   chargePower: 차징 비율 0~1 (속도/크기 비율)
        /// </summary>
        public void Launch(KeyDataSO keyData, Vector2 direction, float chargePower)
        {
            if (keyData == null)
            {
                Debug.LogError("[SealProjectile] KeyDataSO 가 null 입니다.");
                Destroy(gameObject);
                return;
            }

            _keyData = keyData;
            _isActive = true;

            // 크기 — chargePower 에 비례해서 커짐
            float scale = keyData.sealProjectileScale * Mathf.Lerp(0.7f, 1.3f, chargePower);
            transform.localScale = Vector3.one * scale;

            // 스프라이트 방향 반전
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = direction.x < 0f;

            // 속도 — chargePower 에 비례
            float speed = keyData.sealProjectileSpeed * Mathf.Lerp(0.6f, 1.4f, chargePower);
            _rigid2D.linearVelocity = direction * speed;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            // DOTween 발사 임팩트
            transform.DOPunchScale(
                    Vector3.one * 0.25f * chargePower,
                    duration: 0.15f,
                    vibrato: 3,
                    elasticity: 0.5f)
                .SetEase(Ease.OutQuart);

            _lifetimeCoroutine =
                StartCoroutine(LifetimeRoutine(keyData.sealProjectileLifetime));
        }

        // ══════════════════════════════════════════════════════
        // 충돌 감지
        // ══════════════════════════════════════════════════════

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive) return;

            int otherLayer = 1 << other.gameObject.layer;

            // ── EnemyShield → 방패 막힘 ──────────────────────
            if (_shieldLayer.value != 0 && (_shieldLayer.value & otherLayer) != 0)
            {
                HandleShieldBlocked(other);
                Expire();
                return;
            }

            // ── Enemy → 봉인 적용 ────────────────────────────
            if ((_sealLayer.value & otherLayer) != 0)
            {
                HandleEnemyHit(other);
                Expire();
                return;
            }

            // ── 지형 → 소멸 ──────────────────────────────────
            if (_terrainLayer.value != 0 && (_terrainLayer.value & otherLayer) != 0)
            {
                Expire();
            }
        }

        // ══════════════════════════════════════════════════════
        // 방패 막힘 처리
        // ══════════════════════════════════════════════════════

        private void HandleShieldBlocked(Collider2D shieldCol)
        {
            var shieldSr = shieldCol.GetComponent<SpriteRenderer>();

            transform.DOPunchScale(
                    Vector3.one * 0.3f,
                    duration: 0.1f,
                    vibrato: 4,
                    elasticity: 0.2f)
                .SetEase(Ease.OutQuart);

            HitFeedback.PlayerAttackBlocked(
                shieldSr,
                shieldCol.transform,
                transform,
                _rigid2D.linearVelocity.normalized);

            Debug.Log($"[SealProjectile] 방패에 막힘: {shieldCol.name}");
        }

        // ══════════════════════════════════════════════════════
        // 봉인 적용 처리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Enemy 레이어 명중 시 SealComponent.ApplySeal(KeyDataSO) 호출.
        /// SealComponent 없는 적은 봉인 미적용.
        /// </summary>
        private void HandleEnemyHit(Collider2D other)
        {
            var sealComp = other.GetComponentInParent<SealComponent>();

            if (sealComp != null)
            {
                sealComp.ApplySeal(_keyData);
                Debug.Log($"[SealProjectile] 봉인 적용 → {other.name} / {_keyData.sealType}");
            }
            else
            {
                Debug.Log($"[SealProjectile] {other.name} 에 SealComponent 없음. 봉인 미적용.");
            }
        }

        // ══════════════════════════════════════════════════════
        // 소멸
        // ══════════════════════════════════════════════════════

        private IEnumerator LifetimeRoutine(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);
            if (_isActive) Expire();
        }

        public void Expire()
        {
            if (!_isActive) return;
            _isActive = false;

            if (_rigid2D != null)
                _rigid2D.linearVelocity = Vector2.zero;

            if (_lifetimeCoroutine != null)
            {
                StopCoroutine(_lifetimeCoroutine);
                _lifetimeCoroutine = null;
            }

            Destroy(gameObject);
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_keyData == null) return;
            Gizmos.color = new Color(
                _keyData.sealColor.r,
                _keyData.sealColor.g,
                _keyData.sealColor.b,
                0.4f);
            Gizmos.DrawSphere(transform.position, 0.15f * _keyData.sealProjectileScale);
        }
#endif
    }
}