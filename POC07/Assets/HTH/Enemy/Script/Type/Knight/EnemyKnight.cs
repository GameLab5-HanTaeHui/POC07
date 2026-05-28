// ============================================================
// EnemyKnight.cs  v1.3
// 기사형 적 — 방패 방어 + 자물쇠 해제 시스템
//
// [v1.3 변경 — 방어 로직 명확화]
//   [핵심 설계]
//     기사는 전방에 방패를 들고 있음.
//     자물쇠 해제 전: 정면 공격 완전 무효, 후면 → 자물쇠 피격.
//     자물쇠 해제 후: 방패 내려감 → 모든 방향 정상 피격.
//
//   [피격 분기 흐름]
//     1. 자물쇠 해제 완료   → EnemyBase.TakeDamage() (정상 피격)
//     2. Guard 봉인 활성    → 방패 무시 → EnemyBase.TakeDamage() (정면도 피격)
//     3. 정면 공격 (봉인 X) → 방패 완전 무효 (플래시 없음, 데미지 없음)
//     4. 후면 공격 (봉인 X) → 자물쇠 피격 (Lock 의 hitCount 누적)
//
//   [자물쇠 해제 후]
//     _isLockUnlocked = true → 이후 TakeDamage 는 EnemyBase 로 정상 처리.
//     사망 가능 상태로 전환.
//
// [v1.2 변경]
//   Guard 봉인 체크 추가.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 적. EnemyBase 상속. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [전투 흐름]
    ///   플레이어가 정면 공격 시도
    ///     → 방패 무효 (반응 없음)
    ///     → 플레이어는 등 뒤로 돌아가야 함
    ///
    ///   플레이어가 후면 공격 시도
    ///     → LockComponent 피격 카운트 누적
    ///     → 필요 횟수 도달 시 자물쇠 해제 → 약점 노출
    ///
    ///   자물쇠 해제 후 공격
    ///     → EnemyBase.TakeDamage() 정상 처리
    ///     → 체력 감소 → 사망 가능
    ///
    ///   Guard 봉인 활성 시
    ///     → 정면 공격도 허용 → 자물쇠 해제 시간 확보
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
        /// 3단계에서 List 로 확장 예정.
        /// </summary>
        [Tooltip("등 뒤 LockComponent. 미연결 시 자동 탐색.")]
        [SerializeField] private LockComponent _backLock;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private EnemyAI _enemyAI;
        private EnemySealComponent _sealComponent;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 자물쇠 해제 여부.
        /// false : 방패 활성 — 정면 공격 무효, 후면 → 자물쇠 피격
        /// true  : 방패 해제 — EnemyBase 정상 피격
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
                Debug.LogWarning("[EnemyKnight] LockComponent 를 찾을 수 없습니다. " +
                                 "자물쇠 없이 시작합니다.");
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
        /// 기사형 피격 처리. (v1.3)
        ///
        /// [분기 흐름]
        ///   ① 자물쇠 해제 완료
        ///      → EnemyBase.TakeDamage() 정상 처리 (체력 감소 + 사망 가능)
        ///
        ///   ② 자물쇠 미해제 + Guard 봉인 활성
        ///      → 방패 무시 → EnemyBase.TakeDamage() 정상 처리
        ///        (자물쇠가 있어도 체력이 직접 깎임. 공략 보조 수단.)
        ///
        ///   ③ 자물쇠 미해제 + Guard 봉인 없음 + 정면 공격
        ///      → 방패 완전 무효 (반응 없음 — 데미지 0, 플래시 없음)
        ///        플레이어에게 "이쪽으로는 안 된다"는 명확한 피드백.
        ///
        ///   ④ 자물쇠 미해제 + Guard 봉인 없음 + 후면 공격
        ///      → LockComponent.TakeDamage() 호출
        ///        자물쇠 피격 카운트 누적 → 해제 조건 충족 시 OnLockUnlocked 발행
        /// </summary>
        public new void TakeDamage(DamageInfo info)
        {
            // ① 자물쇠 해제 완료 → 정상 피격
            if (_isLockUnlocked)
            {
                base.TakeDamage(info);
                return;
            }

            // ② Guard 봉인 활성 → 방패 무시
            bool guardSealed = _sealComponent != null
                && _sealComponent.IsSealedAction(SealType.Guard);

            if (guardSealed)
            {
                Debug.Log("[EnemyKnight] Guard 봉인 활성 → 방패 무시 피격!");
                base.TakeDamage(info);
                return;
            }

            // ③ 정면 공격 → 방패 완전 무효 (반응 없음)
            if (IsFrontalAttack(info.Direction))
            {
                // 아무 반응 없음 — 방패가 공격을 완전히 흡수
                // 플래시나 피격 효과 없이 조용히 무시
                Debug.Log("[EnemyKnight] 정면 방패 → 공격 무효");
                return;
            }

            // ④ 후면 공격 → 자물쇠 피격
            if (_backLock != null)
            {
                Debug.Log("[EnemyKnight] 후면 공격 → 자물쇠 피격");
                _backLock.TakeDamage(info);
            }
            else
            {
                // 자물쇠 없는 기사 — 후면 공격은 정상 피격
                Debug.Log("[EnemyKnight] 후면 공격 → 자물쇠 없음, 정상 피격");
                base.TakeDamage(info);
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 자물쇠 해제 완료 수신.
        /// 이후 TakeDamage 는 EnemyBase 로 정상 처리됨.
        /// </summary>
        private void HandleLockUnlocked()
        {
            _isLockUnlocked = true;
            Debug.Log("[EnemyKnight] 자물쇠 해제 완료 → 약점 노출!");

            // 색상으로 약점 노출 피드백
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
        // 내부 — 정면/후면 판단
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 공격 방향이 기사 정면 방향과 반대인지 판단.
        ///
        /// [판단 공식]
        ///   dot(기사_바라보는방향, 공격_방향) &lt; 0 → 정면 공격
        ///
        /// [예시]
        ///   기사가 오른쪽(+1) 을 바라볼 때
        ///   공격 방향이 왼쪽(-1) → dot = (+1)×(-1) = -1 &lt; 0 → 정면 공격 (방패에 막힘)
        ///   공격 방향이 오른쪽(+1) → dot = (+1)×(+1) = 1 > 0 → 후면 공격 (자물쇠 피격)
        /// </summary>
        private bool IsFrontalAttack(Vector2 attackDir)
        {
            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            float dot = facingDir * attackDir.x;
            return dot < 0f;
        }
    }
}