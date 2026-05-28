// ============================================================
// EnemyKnight.cs  v1.2
// 기사형 적 — Guard 봉인 연동
//
// [v1.2 변경 — Guard 봉인 체크 추가]
//   TakeDamage() 의 정면/후면 판단 직전에
//   Guard 봉인 활성 여부 체크.
//
//   [Guard 봉인 활성 시]
//     방패 판정을 건너뜀 → 정면 공격도 EnemyBase.TakeDamage() 정상 처리.
//     시각적으로 파란 방패 플래시 없음 → 방패가 내려간 상태.
//     자물쇠 미해제 상태에도 적용 (방패 봉인 = 방패 불능).
//
//   [Guard 봉인 해제 후]
//     자동으로 방패 판정 복귀.
//     자물쇠 해제(_isLockUnlocked) 상태와는 독립.
//
//   [설계 의도]
//     Guard 봉인이 방패를 "잠근다".
//     기사는 방어 무기(방패)를 스스로 들 수 없는 상태.
//     플레이어는 이 틈에 정면 공략 + 자물쇠 해제 가능.
//
// [v1.1 변경]
//   KnightAI 참조 → EnemyAI 참조로 교체.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 적. EnemyBase 상속. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [TakeDamage 처리 흐름 — v1.2]
    ///   자물쇠 해제 후
    ///     → EnemyBase.TakeDamage() (정상 피격)
    ///
    ///   자물쇠 미해제
    ///     → Guard 봉인 활성?
    ///         Yes → 방패 무시 → EnemyBase.TakeDamage() (정면도 피격)
    ///         No  → IsFrontalAttack() 체크
    ///                 정면 → 방패 막힘 (파란 플래시)
    ///                 후면 → 자물쇠 피격
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemyKnight : EnemyBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 자물쇠 연결 ──────────────────────")]

        /// <summary>
        /// 등 뒤 자물쇠 LockComponent.
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("등 뒤 LockComponent. 미연결 시 자동 탐색.")]
        [SerializeField] private LockComponent _backLock;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        /// <summary> EnemyAI 참조 — FacingDirection 읽기용. </summary>
        private EnemyAI _enemyAI;

        /// <summary>
        /// 봉인 컴포넌트 참조.
        /// Guard 봉인 체크에 사용.
        /// Awake 에서 자동 취득. 없어도 동작.
        /// </summary>
        private EnemySealComponent _sealComponent;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 여부.
        /// false = 방패/자물쇠 판단 활성
        /// true  = EnemyBase 정상 피격 처리
        /// </summary>
        private bool _isLockUnlocked;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();

            _enemyAI = GetComponent<EnemyAI>();
            _sealComponent = GetComponent<EnemySealComponent>();

            if (_backLock == null)
                _backLock = GetComponentInChildren<LockComponent>();

            if (_backLock == null)
                Debug.LogWarning("[EnemyKnight] LockComponent 를 찾을 수 없습니다.");
        }

        private void Start()
        {
            if (_backLock != null)
            {
                _backLock.OnLockUnlocked += HandleLockUnlocked;
                _backLock.OnLockHit += HandleLockHit;
            }
        }

        private void OnDestroy()
        {
            if (_backLock != null)
            {
                _backLock.OnLockUnlocked -= HandleLockUnlocked;
                _backLock.OnLockHit -= HandleLockHit;
            }
        }

        // ══════════════════════════════════════════════════════
        // IDamageable override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 기사형 피격 처리. (v1.2)
        ///
        /// [처리 분기]
        ///   1. 자물쇠 해제 후
        ///      → EnemyBase.TakeDamage() 정상 처리
        ///
        ///   2. 자물쇠 미해제 + Guard 봉인 활성
        ///      → 방패 무시 → EnemyBase.TakeDamage() 정상 처리
        ///      (정면 공격도 피격됨)
        ///
        ///   3. 자물쇠 미해제 + Guard 봉인 비활성
        ///      → IsFrontalAttack() 판단
        ///         정면 → 방패 막힘 + 파란 플래시
        ///         후면 → 자물쇠 피격
        /// </summary>
        public new void TakeDamage(DamageInfo info)
        {
            // ① 자물쇠 해제 후 → 정상 피격
            if (_isLockUnlocked)
            {
                base.TakeDamage(info);
                return;
            }

            // ② Guard 봉인 활성 → 방패 무시하고 정상 피격
            bool guardSealed = _sealComponent != null
                && _sealComponent.IsSealedAction(SealType.Guard);

            if (guardSealed)
            {
                Debug.Log("[EnemyKnight] Guard 봉인 활성 → 방패 무시 피격!");
                base.TakeDamage(info);
                return;
            }

            // ③ 방패 / 자물쇠 판단
            if (IsFrontalAttack(info.Direction))
            {
                // 정면 → 방패 막힘
                Debug.Log("[EnemyKnight] 방패로 막힘!");
                StartCoroutine(ShieldFlashRoutine());
            }
            else
            {
                // 후면 → 자물쇠 피격
                Debug.Log("[EnemyKnight] 후면 공격 → 자물쇠 피격");
                _backLock?.TakeDamage(info);
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        private void HandleLockUnlocked()
        {
            _isLockUnlocked = true;
            Debug.Log("[EnemyKnight] 자물쇠 해제 → 약점 노출!");

            if (_spriteRenderer != null)
                _spriteRenderer.color = new Color(1f, 0.4f, 0.4f, 1f);
        }

        private void HandleLockHit(int current, int required)
        {
            Debug.Log($"[EnemyKnight] 자물쇠 피격 {current}/{required}");
        }

        // ══════════════════════════════════════════════════════
        // EnemyBase override
        // ══════════════════════════════════════════════════════

        protected override void OnDamaged(DamageInfo info) { }

        // ══════════════════════════════════════════════════════
        // 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 방향이 정면 공격인지 판단.
        ///
        /// [판단 공식]
        ///   dot(EnemyFacingDir, AttackDir) &lt; 0 = 정면 공격
        ///   적이 오른쪽(1)을 보고 있고 공격 방향이 오른쪽(1)이면
        ///   dot = 1 × 1 = 1 > 0 → 후면 공격.
        ///   적이 오른쪽(1)을 보고 있고 공격 방향이 왼쪽(-1)이면
        ///   dot = 1 × -1 = -1 &lt; 0 → 정면 공격.
        /// </summary>
        /// <param name="attackDir">공격 방향 벡터 (DamageInfo.Direction)</param>
        /// <returns>정면 공격이면 true</returns>
        private bool IsFrontalAttack(Vector2 attackDir)
        {
            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 0;// _facingDirection;
            float dot = facingDir * attackDir.x;
            return dot < 0f;
        }

        /// <summary>
        /// 방패 막힘 파란 플래시 코루틴.
        /// </summary>
        private IEnumerator ShieldFlashRoutine()
        {
            if (_spriteRenderer == null) yield break;

            _spriteRenderer.color = new Color(0.3f, 0.5f, 1f, 1f);
            yield return new WaitForSeconds(0.12f);
            _spriteRenderer.color = Color.white;
        }
    }
}