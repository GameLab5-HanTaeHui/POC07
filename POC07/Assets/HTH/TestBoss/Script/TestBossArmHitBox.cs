// ============================================================
// TestBossArmHitbox.cs  v1.1
// 테스트 보스 팔 히트박스 — 피격 수신 컴포넌트
//
// [v1.1 변경 — IDamageable 탐색 방식 수정]
//
//   [기존 v1.0 문제]
//     other.TryGetComponent<IDamageable>() 로 탐색
//     → other = 충돌한 Collider 의 오브젝트
//     → PlayerHealth 가 Player 루트에 있어도
//       충돌 Collider 가 자식이면 탐색 실패
//     → 레이어 체크가 _playerLayer = None(0) 이면 전부 차단
//
//   [수정]
//     other.GetComponentInParent<IDamageable>() 로 변경
//     → Collider 가 어느 자식에 있어도 루트까지 역방향 탐색
//     → 보스 자신 제외: GetComponentInParent<TestBossCore>() 체크
//     → 레이어 체크 완전 제거 (보스 자신 제외로 대체)
//
//   [레이어 체크 제거 이유]
//     Physics2D Matrix 에서 EnemyAttackHit(16) ↔ Player 를 ON 으로 설정하면
//     Player 레이어 오브젝트의 Collider 만 충돌 이벤트를 받음.
//     레이어 필터는 Matrix 에서 이미 처리되므로 코드 중복 체크 불필요.
//     레이어 번호 불일치로 발생하던 피격 미동작 버그를 근본 차단.
//
// [역할]
//   Arm_L / Arm_R 오브젝트에 부착.
//   OnTriggerEnter2D 로 Player 충돌을 수신하여
//   연결된 패턴(TestBossPatternBase) 에 피격 사실을 전달.
//
// [피격 흐름]
//   PunchDown / PunchShot 의 OnActive 시작
//     → TestBossPatternBase.ExecuteWarning() 의 OnPatternStart 발행
//     → HandlePatternStart() → _isActive = true
//   팔(Arm_L/R) 이동 → 플레이어 Collider 와 겹침
//     → OnTriggerEnter2D(other)
//     → GetComponentInParent<TestBossCore>() == null (보스 자신 아님)
//     → GetComponentInParent<IDamageable>() → PlayerHealth
//     → TakeDamage() 호출
//     → _hasHitThisSwing = true (이 Active 구간 1회만 피격)
//   패턴 종료 → OnPatternEnd → _isActive = false, _hasHitThisSwing = false
//
// [Prefab 설정]
//   Arm_L 에 부착 → _pattern = PunchDown 의 TestBossPattern_PunchDown
//   Arm_R 에 부착 → _pattern = PunchShot 의 TestBossPattern_PunchShot
//   Arm_L/R Layer = EnemyAttackHit (16)
//   Arm_L/R BoxCollider2D IsTrigger = true
//   _playerLayer 필드 제거 (v1.1) — Physics2D Matrix 로 대체
//
// [Physics2D Matrix 요구사항]
//   EnemyAttackHit (16) ↔ Player 레이어 : ON 필수
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 팔 히트박스 피격 수신 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [v1.1 핵심 변경]
    ///   TryGetComponent → GetComponentInParent 로 변경
    ///   레이어 체크 제거 → Physics2D Matrix 위임
    ///   보스 자신 제외: GetComponentInParent<TestBossCore>() null 체크
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossArmHitbox : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 패턴 연결 (필수) ──────────────────────")]

        /// <summary>
        /// 이 팔을 사용하는 패턴.
        /// Arm_L → PunchDown 의 TestBossPattern_PunchDown
        /// Arm_R → PunchShot 의 TestBossPattern_PunchShot
        /// </summary>
        [Tooltip("이 팔을 사용하는 패턴 컴포넌트.")]
        [SerializeField] private TestBossPatternBase _pattern;

        /// <summary>
        /// 피격 데미지.
        /// 패턴 스크립트의 _punchDamage 와 동일하게 설정.
        /// </summary>
        [Tooltip("피격 데미지.")]
        [Min(0f)]
        [SerializeField] private float _damage = 15f;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 이 패턴이 Active 중인지 여부.
        /// 패턴이 실행 중일 때만 피격 판정.
        /// </summary>
        private bool _isActive;

        /// <summary>
        /// 이번 Active 구간에서 이미 피격했는지.
        /// 한 패턴당 1회만 피격.
        /// </summary>
        private bool _hasHitThisSwing;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Start()
        {
            if (_pattern != null)
            {
                _pattern.OnPatternStart += HandlePatternStart;
                _pattern.OnPatternEnd += HandlePatternEnd;
            }
            else
            {
                Debug.LogWarning($"[TestBossArmHitbox] {gameObject.name}: _pattern 미연결");
            }
        }

        private void OnDestroy()
        {
            if (_pattern != null)
            {
                _pattern.OnPatternStart -= HandlePatternStart;
                _pattern.OnPatternEnd -= HandlePatternEnd;
            }
        }

        // ══════════════════════════════════════════════════════
        // 패턴 이벤트 수신
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 패턴 시작 수신 — Active 판정 ON.
        /// OnPatternStart 는 Warning 시작 시 발행되므로
        /// 팔이 아직 이동 전 → 실제 충돌은 Active 구간에서 발생.
        /// </summary>
        private void HandlePatternStart(TestBossPatternBase p)
        {
            _isActive = true;
            _hasHitThisSwing = false;
        }

        /// <summary>
        /// 패턴 종료 수신 — Active 판정 OFF.
        /// </summary>
        private void HandlePatternEnd(TestBossPatternBase p)
        {
            _isActive = false;
            _hasHitThisSwing = false;
        }

        // ══════════════════════════════════════════════════════
        // 충돌 감지
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Player Collider 와 Trigger 충돌 수신.
        ///
        /// [v1.1 수정]
        ///   레이어 체크 제거 → Physics2D Matrix 에 위임
        ///   TryGetComponent → GetComponentInParent 로 변경
        ///   보스 자신 제외: GetComponentInParent<TestBossCore>() null 체크
        ///
        /// [발화 조건]
        ///   _isActive == true (패턴 진행 중)
        ///   _hasHitThisSwing == false (이 Active 구간 미피격)
        ///   other 의 루트 계층에 TestBossCore 없음 (보스 자신 아님)
        ///   other 의 루트 계층에 IDamageable 있음 (PlayerHealth)
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_isActive || _hasHitThisSwing) return;

            // ★ v1.1: 보스 자신 제외 — GetComponentInParent 로 탐색
            if (other.GetComponentInParent<TestBossCore>() != null) return;

            // ★ v1.1: IDamageable 루트 방향 탐색
            //   other = 충돌한 Collider 의 오브젝트
            //   PlayerHealth 가 Player 루트에 있어도 자식 Collider 충돌 시 탐색 가능
            IDamageable damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) return;

            // 공격 방향: 팔 → 플레이어
            Vector2 dir = ((Vector2)other.transform.position
                - (Vector2)transform.position).normalized;

            var info = new DamageInfo(
                transform.position,
                _damage,
                dir,
                AttackType.Combo1);

            damageable.TakeDamage(info);
            _hasHitThisSwing = true;

            Debug.Log($"[TestBossArmHitbox] {gameObject.name} → 플레이어 피격: -{_damage}");
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 외부에서 Active 강제 해제 (패턴 Interrupt 시).
        /// </summary>
        public void ForceDeactivate()
        {
            _isActive = false;
            _hasHitThisSwing = false;
        }
    }
}