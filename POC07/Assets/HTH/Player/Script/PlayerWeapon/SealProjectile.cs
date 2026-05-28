// ============================================================
// SealProjectile.cs  v1.0
// 봉인 열쇠 투사체 컴포넌트
//
// [역할]
//   SealKeyWeapon 이 발사하는 투사체.
//   FacingDirection 방향으로 직진하다가
//   Enemy 레이어 오브젝트와 충돌 시 EnemySealComponent 에 봉인 적용.
//
// [생애주기]
//   SealKeyWeapon.FireProjectile()
//     → Instantiate(SealProjectile Prefab)
//     → Launch(data, direction) 호출
//     → projectileSpeed 로 직진
//     → OnTriggerEnter2D 에서 Enemy 감지
//         → EnemySealComponent.ApplySeal(data) 호출
//         → 자기 자신 비활성(소멸 처리)
//     → projectileLifetime 초과 시 자동 소멸
//
// [오브젝트 풀링 대비]
//   Destroy 대신 gameObject.SetActive(false) 로 처리.
//   SealKeyWeapon 이 오브젝트 풀을 구현하면 Recycle() 로 재사용 가능.
//   현재 단계에서는 비활성화 후 Destroy(gameObject, delay) 로 처리.
//
// [Collider2D 설정 — Inspector]
//   CircleCollider2D isTrigger = true
//   Layer = PlayerHitbox  (기존 플레이어 무기 레이어와 동일)
//
// [감지 대상 레이어]
//   _sealLayer (Inspector 연결) = Enemy 레이어
//   OnTriggerEnter2D 에서 레이어마스크 비트 연산으로 검증.
//   CompareTag 사용 금지 — LayerMask 비트 연산 사용.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 봉인 열쇠 투사체. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [Prefab 구조]
    ///   SealProjectile (Prefab 루트)
    ///   ├── [SealProjectile]      이 컴포넌트
    ///   ├── [Rigidbody2D]         GravityScale=0 / Kinematic=false
    ///   ├── [CircleCollider2D]    isTrigger=true / radius=0.15
    ///   └── [SpriteRenderer]      투사체 스프라이트
    ///
    /// [SealKeyWeapon 에서의 사용 흐름]
    ///   var go = Instantiate(_projectilePrefab, firePos, Quaternion.identity);
    ///   var proj = go.GetComponent&lt;SealProjectile&gt;();
    ///   proj.Launch(_sealData, facingDirection);
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class SealProjectile : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 감지 설정 ──────────────────────")]

        /// <summary>
        /// 봉인 적용 대상 레이어마스크.
        /// Enemy 레이어 선택. CompareTag 금지 — 레이어 비트 연산 사용.
        /// </summary>
        [Tooltip("봉인 적용 대상 레이어. Enemy 레이어 선택.")]
        [SerializeField] private LayerMask _sealLayer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 런타임 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 적용할 봉인 데이터.
        /// Launch() 호출 시 주입.
        /// </summary>
        private SealDataSO _sealData;

        /// <summary>
        /// 투사체 활성 여부.
        /// 명중 or 수명 만료 시 false 로 설정하여 중복 처리 방지.
        /// </summary>
        private bool _isActive;

        /// <summary>
        /// 수명 타이머 코루틴.
        /// </summary>
        private Coroutine _lifetimeCoroutine;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 컴포넌트 참조 취득.
        /// </summary>
        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 투사체는 중력 영향 없이 직진
            _rigid2D.gravityScale = 0f;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — SealKeyWeapon 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 투사체 발사.
        /// Instantiate 직후 SealKeyWeapon 에서 호출.
        ///
        /// [발사 흐름]
        ///   1. 봉인 데이터 및 방향 저장
        ///   2. SpriteRenderer 에 투사체 스프라이트 적용
        ///   3. 크기 스케일 적용 (projectileScale)
        ///   4. Rigidbody2D.linearVelocity 로 직진 시작
        ///   5. 수명 타이머 코루틴 시작
        /// </summary>
        /// <param name="data">봉인 데이터 SO (속도·수명·타입 포함)</param>
        /// <param name="direction">발사 방향. 1 = 오른쪽, -1 = 왼쪽.</param>
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

            // 스프라이트 적용
            if (_spriteRenderer != null && data.projectileSprite != null)
                _spriteRenderer.sprite = data.projectileSprite;

            // 크기 스케일 적용
            transform.localScale = Vector3.one * data.projectileScale;

            // 방향에 따라 스프라이트 좌우 반전
            if (_spriteRenderer != null)
                _spriteRenderer.flipX = direction < 0f;

            // 직진 속도 설정
            _rigid2D.linearVelocity = new Vector2(direction * data.projectileSpeed, 0f);

            // 수명 타이머 시작
            _lifetimeCoroutine = StartCoroutine(LifetimeRoutine(data.projectileLifetime));
        }

        // ══════════════════════════════════════════════════════
        // 충돌 감지
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Enemy 레이어 오브젝트와 충돌 시 봉인 적용.
        ///
        /// [레이어 검증 방식]
        ///   (_sealLayer.value &amp; (1 &lt;&lt; other.gameObject.layer)) != 0
        ///   → CompareTag 금지 규칙 준수. LayerMask 비트 연산 사용.
        ///
        /// [EnemySealComponent 없는 적]
        ///   봉인을 받을 수 없는 적(더미 등)에는 봉인 미적용.
        ///   투사체는 정상적으로 소멸.
        ///
        /// [중복 명중 방지]
        ///   _isActive 플래그로 한 번만 처리.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive) return;

            // 레이어마스크 비트 연산으로 Enemy 레이어 검증
            if ((_sealLayer.value & (1 << other.gameObject.layer)) == 0) return;

            // EnemySealComponent 탐색 (루트 오브젝트에서 검색)
            EnemySealComponent sealComponent =
                other.GetComponentInParent<EnemySealComponent>();

            if (sealComponent != null)
            {
                sealComponent.ApplySeal(_sealData);
                Debug.Log($"[SealProjectile] 봉인 적용 → {other.name} / {_sealData.sealType}");
            }
            else
            {
                Debug.Log($"[SealProjectile] {other.name} 에 EnemySealComponent 없음. 봉인 미적용.");
            }

            // 명중 처리 — 소멸
            Expire();
        }

        // ══════════════════════════════════════════════════════
        // 수명 관리
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 수명 타이머 코루틴.
        /// projectileLifetime 초 후 자동 소멸.
        /// </summary>
        /// <param name="lifetime">최대 생존 시간 (초)</param>
        private IEnumerator LifetimeRoutine(float lifetime)
        {
            yield return new WaitForSeconds(lifetime);

            if (_isActive)
            {
                Debug.Log("[SealProjectile] 수명 만료 → 소멸");
                Expire();
            }
        }

        /// <summary>
        /// 투사체 소멸 처리.
        /// 명중 or 수명 만료 시 호출.
        ///
        /// [처리 순서]
        ///   1. _isActive = false → 중복 처리 방지
        ///   2. 이동 정지 (velocity = zero)
        ///   3. 코루틴 정리
        ///   4. gameObject 파괴
        ///
        /// [오브젝트 풀링 전환 시]
        ///   Destroy(gameObject) → gameObject.SetActive(false) 로 교체.
        ///   SealKeyWeapon 의 Pool.Release(this) 호출 추가.
        /// </summary>
        private void Expire()
        {
            if (!_isActive) return;

            _isActive = false;

            // 이동 즉시 정지
            if (_rigid2D != null)
                _rigid2D.linearVelocity = Vector2.zero;

            // 수명 코루틴 정리
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

            // 봉인 색상으로 투사체 범위 표시
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