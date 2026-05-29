// ============================================================
// SealProjectile.cs  v1.1
// 봉인 열쇠 투사체 컴포넌트
//
// [v1.1 변경]
//   ① EnemySealComponent → SealComponent 로 교체
//   ② EnemyShield 레이어 감지 시 방패 막힘 처리 추가
//       _shieldLayer 필드 추가.
//       OnTriggerEnter2D 에서 EnemyShield 감지 시
//       HitFeedback.PlayerAttackBlocked() + Expire().
//   ③ 지형(Ground/Wall) 충돌 시 소멸 추가
//       _terrainLayer 필드 추가.
//
// [충돌 분기 — v1.1]
//   EnemyShield 레이어 → 방패 막힘 피드백 + Expire()
//   Enemy 레이어       → SealComponent.ApplySeal() + Expire()
//   지형 레이어        → Expire()
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
    /// 봉인 열쇠 투사체. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [Prefab 구조]
    ///   SealProjectile (Prefab 루트)   Layer: PlayerHitbox
    ///   ├── [SealProjectile]
    ///   ├── [Rigidbody2D]    GravityScale=0
    ///   ├── [CircleCollider2D] isTrigger=ON / radius=0.15
    ///   └── [SpriteRenderer]
    ///
    /// [Inspector 연결]
    ///   _sealLayer   = Enemy 레이어
    ///   _shieldLayer = EnemyShield 레이어
    ///   _terrainLayer = Ground + Wall 레이어
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
        [Tooltip("지형 레이어. Ground + Wall 레이어 선택. 충돌 시 즉시 소멸.")]
        [SerializeField] private LayerMask _terrainLayer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 런타임 상태
        // ──────────────────────────────────────────

        private SealDataSO _sealData;
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
        // 외부 API — SealKeyWeapon 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 투사체 발사.
        /// SealKeyWeapon.FireProjectile() 에서 Instantiate 직후 호출.
        /// </summary>
        public void Launch(SealDataSO data, float direction)
        {
            if (data == null)
            {
                Debug.LogError("[SealProjectile] SealDataSO 가 null 입니다.");
                Destroy(gameObject);
                return;
            }

            _sealData = data;
            _isActive = true;

            if (_spriteRenderer != null && data.projectileSprite != null)
                _spriteRenderer.sprite = data.projectileSprite;

            transform.localScale = Vector3.one * data.projectileScale;

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = direction < 0f;

            _rigid2D.linearVelocity = new Vector2(direction * data.projectileSpeed, 0f);

            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(data.projectileLifetime));
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

        /// <summary>
        /// 방패에 막혔을 때 처리.
        /// HitFeedback.PlayerAttackBlocked() 호출.
        /// </summary>
        private void HandleShieldBlocked(Collider2D shieldCol)
        {
            var shieldSr = shieldCol.GetComponent<SpriteRenderer>();

            // 투사체 막힘 임팩트
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
        /// Enemy 레이어 명중 시 SealComponent.ApplySeal() 호출.
        /// SealComponent 없는 적(더미 등)은 봉인 미적용.
        /// </summary>
        private void HandleEnemyHit(Collider2D other)
        {
            SealComponent sealComponent =
                other.GetComponentInParent<SealComponent>();

            if (sealComponent != null)
            {
                sealComponent.ApplySeal(_sealData);
                Debug.Log($"[SealProjectile] 봉인 적용 → {other.name} / {_sealData.sealType}");
            }
            else
            {
                Debug.Log($"[SealProjectile] {other.name} 에 SealComponent 없음. 봉인 미적용.");
            }
        }

        // ══════════════════════════════════════════════════════
        // 소멸 처리
        // ══════════════════════════════════════════════════════

        private IEnumerator LifetimeRoutine(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);
            if (_isActive)
            {
                Debug.Log("[SealProjectile] 수명 만료 → 소멸");
                Expire();
            }
        }

        private void Expire()
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
            if (_sealData == null) return;
            Gizmos.color = new Color(
                _sealData.sealColor.r,
                _sealData.sealColor.g,
                _sealData.sealColor.b,
                0.4f);
            Gizmos.DrawSphere(transform.position, 0.15f * _sealData.projectileScale);
        }
#endif
    }
}