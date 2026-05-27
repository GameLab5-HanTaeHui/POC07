// ============================================================
// MovementAnimator.cs  v1.1
// 플레이어 이동 패키지 — Animator 파라미터 동기화 컴포넌트
//
// [독립 패키지]
//   namespace : PlayerMovement (HOSE 종속 없음)
//   의존 대상 : PlayerMover (같은 오브젝트)
//
// [v1.1 변경 — Animator 책임 완전 통합]
//   PlayerMover 에서 분리된 Trigger 처리를 이 파일로 이전.
//   모든 Animator.StringToHash 는 이 파일 하나에서만 관리.
//
//   추가:
//     _hashDash        — "Dash" Trigger
//     _hashDoubleJump  — "DoubleJump" Trigger
//     PlayerMover.OnDashStarted   구독 → SetTrigger("Dash")
//     PlayerMover.OnDoubleJumped  구독 → SetTrigger("DoubleJump")
//
//   설계 원칙:
//     PlayerMover  = 물리 이동만 담당. Animator 를 전혀 알지 못함.
//     MovementAnimator = 모든 Animator 파라미터를 단독 관리.
//
// [Animator 파라미터 완전 목록 — 이 파일이 유일한 관리 지점]
//   Speed       (Float)  : Mathf.Abs(MoveInput) — 매 프레임 Update
//   IsGrounded  (Bool)   : IsGrounded 값        — 매 프레임 Update
//   IsFiring    (Bool)   : SetFiring() 외부 호출  — 매 프레임 Update
//   Dash        (Trigger): PlayerMover.OnDashStarted 이벤트 수신 시 1회
//   DoubleJump  (Trigger): PlayerMover.OnDoubleJumped 이벤트 수신 시 1회
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 모든 Animator 파라미터를 관리하는 단독 컴포넌트. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [이 파일이 하는 것]
    ///   - 모든 Animator.StringToHash 선언 및 캐싱
    ///   - Update 매 프레임: Speed / IsGrounded / IsFiring SetFloat/SetBool
    ///   - OnDashStarted 수신 시 1회: SetTrigger("Dash")
    ///   - OnDoubleJumped 수신 시 1회: SetTrigger("DoubleJump")
    ///
    /// [이 파일이 하지 않는 것]
    ///   - 물리 이동 / 점프 판정 (PlayerMover 담당)
    ///   - 입력 수신 (MovementInput 담당)
    ///
    /// ────────────────────────────────────────────────────
    /// [StringToHash 캐싱 이유]
    ///   Animator.SetFloat("Speed") 는 매 프레임 문자열 비교 발생.
    ///   StringToHash 로 int 를 미리 계산해두면 int 비교만 하므로 훨씬 빠름.
    ///   static readonly 로 클래스당 1회만 계산됨.
    ///
    /// ────────────────────────────────────────────────────
    /// [Trigger vs Bool/Float 발행 방식이 다른 이유]
    ///   Float/Bool  : 매 프레임 최신 상태를 덮어쓰는 방식 (Update).
    ///   Trigger     : 이벤트 발생 순간 1회만 호출해야 함.
    ///                 Update 에서 매 프레임 SetTrigger 하면
    ///                 클립이 매 프레임 재시작되는 버그 발생.
    ///                 → 이벤트 구독으로 정확히 1회만 발행.
    ///
    /// ────────────────────────────────────────────────────
    /// [IsFiring 연동 — 선택]
    ///   분사 등 전투 상태를 Attack Layer 에 전달할 때
    ///   외부(PlayerCombat 등)에서 SetFiring(bool) 호출.
    ///   전투 시스템이 없는 프로젝트는 호출하지 않으면 됨 (false 고정).
    ///
    /// ────────────────────────────────────────────────────
    /// [Animator 없는 경우]
    ///   Awake() 에서 Animator = null 이면 컴포넌트 자체를 비활성.
    ///   이동/점프/대쉬 동작에는 영향 없음.
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public class MovementAnimator : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerMover _mover;
        private Animator _animator;

        // ──────────────────────────────────────────
        // Animator 해시 캐싱
        // ──────────────────────────────────────────
        // 이 파일이 모든 Animator 파라미터 이름의 유일한 관리 지점.
        // 파라미터 이름 변경 시 이곳만 수정하면 됨.
        // ⚠️ Animator Controller 의 파라미터명과 대소문자까지 완전 일치 필수.
        //    불일치 시 Unity 는 에러 없이 조용히 무시함.

        /// <summary>
        /// "Speed" 파라미터 해시.
        /// 이동 블렌드 트리용. 0 = 정지 / 1 = 최대 속도.
        /// Update() 에서 매 프레임 SetFloat.
        /// </summary>
        private static readonly int _hashSpeed = Animator.StringToHash("Speed");

        /// <summary>
        /// "IsGrounded" 파라미터 해시.
        /// 지상/공중 State 전환용. true = 지면 접촉 중.
        /// Update() 에서 매 프레임 SetBool.
        /// </summary>
        private static readonly int _hashIsGrounded = Animator.StringToHash("IsGrounded");

        /// <summary>
        /// "IsFiring" 파라미터 해시.
        /// Attack Layer 제어용. 외부 SetFiring() 으로 설정.
        /// Update() 에서 매 프레임 SetBool.
        /// </summary>
        private static readonly int _hashIsFiring = Animator.StringToHash("IsFiring");

        /// <summary>
        /// "Dash" 트리거 해시.
        ///
        /// [Trigger 파라미터란?]
        ///   Float/Bool 과 달리 일회성 신호.
        ///   SetTrigger() 호출 시 Animator 가 해당 전환을 1회 소비 후 초기화.
        ///   매 프레임 호출하면 클립이 재시작되므로 반드시 1회만 호출.
        ///
        /// PlayerMover.OnDashStarted 이벤트 수신 시 1회 SetTrigger.
        /// </summary>
        private static readonly int _hashDash = Animator.StringToHash("Dash");

        /// <summary>
        /// "DoubleJump" 트리거 해시.
        ///
        /// [1단 점프와 구분이 필요한 이유]
        ///   1단 점프 : IsGrounded = false 전환 → Animator 가 자동 처리.
        ///   2단 점프 : 이미 공중이라 IsGrounded 가 바뀌지 않음.
        ///              → 트리거로 명시적으로 알려야 DoubleJump 클립 재생.
        ///
        /// PlayerMover.OnDoubleJumped 이벤트 수신 시 1회 SetTrigger.
        /// </summary>
        private static readonly int _hashDoubleJump = Animator.StringToHash("DoubleJump");

        // ──────────────────────────────────────────
        // 외부 제어 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// IsFiring Animator Bool 값.
        /// SetFiring() 으로 외부(전투 시스템)에서 설정.
        /// 이동 패키지 자체는 이 값을 변경하지 않는다.
        /// </summary>
        private bool _isFiring;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 컴포넌트 취득 + Animator 확인.
        /// Animator 미부착 시 이 컴포넌트 비활성 — 이동 동작에 영향 없음.
        /// </summary>
        private void Awake()
        {
            _mover = GetComponent<PlayerMover>();
            _animator = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning("[MovementAnimator] Animator 가 없습니다. " +
                                 "Animator 파라미터 동기화가 비활성됩니다.");
                enabled = false;
            }
        }

        /// <summary>
        /// Start 에서 PlayerMover 이벤트 구독.
        ///
        /// [왜 Start 인가?]
        ///   PlayerMover 는 Awake 에서 초기화 완료.
        ///   Start 는 모든 Awake 완료 후 실행 → null 참조 없이 안전하게 구독.
        ///
        /// [구독 이벤트]
        ///   OnDashStarted  → HandleDashStarted()  → SetTrigger("Dash")
        ///   OnDoubleJumped → HandleDoubleJumped() → SetTrigger("DoubleJump")
        /// </summary>
        private void Start()
        {
            if (_mover == null) return;
            _mover.OnDashStarted += HandleDashStarted;
            _mover.OnDoubleJumped += HandleDoubleJumped;
        }

        /// <summary>
        /// 매 프레임 Float / Bool 파라미터 갱신.
        ///
        /// [Float / Bool 은 매 프레임 Update 에서 처리하는 이유]
        ///   상태가 언제 바뀔지 모르므로 항상 최신값을 덮어써야 함.
        ///   이벤트 방식이면 변경 순간을 놓칠 수 있음.
        ///
        /// [Trigger 는 Update 에서 처리하지 않는 이유]
        ///   매 프레임 SetTrigger 하면 클립이 매 프레임 재시작됨.
        ///   이벤트 구독으로 발생 순간 1회만 호출 (Start 에서 구독).
        /// </summary>
        private void Update()
        {
            _animator.SetFloat(_hashSpeed, Mathf.Abs(_mover.MoveInput));
            _animator.SetBool(_hashIsGrounded, _mover.IsGrounded);
            _animator.SetBool(_hashIsFiring, _isFiring);
        }

        /// <summary>
        /// 구독 해제. 메모리 누수 방지.
        /// </summary>
        private void OnDestroy()
        {
            if (_mover == null) return;
            _mover.OnDashStarted -= HandleDashStarted;
            _mover.OnDoubleJumped -= HandleDoubleJumped;
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러 — Trigger 발행
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// PlayerMover.OnDashStarted 수신 → "Dash" Trigger 1회 발행.
        ///
        /// [이 구조의 이점]
        ///   PlayerMover 는 Animator 를 모른다.
        ///   대쉬 물리가 시작됐다는 이벤트를 보내면
        ///   MovementAnimator 가 "그렇군, Dash 트리거를 쏴야겠다" 고 결정한다.
        ///   나중에 Animator 가 없는 프로젝트에서도 PlayerMover 수정 불필요.
        /// </summary>
        private void HandleDashStarted()
        {
            _animator.SetTrigger(_hashDash);
        }

        /// <summary>
        /// PlayerMover.OnDoubleJumped 수신 → "DoubleJump" Trigger 1회 발행.
        ///
        /// [1단 점프는 왜 여기서 처리 안 하는가?]
        ///   1단 점프 시 IsGrounded 가 true → false 로 전환된다.
        ///   Update() 에서 매 프레임 SetBool(_hashIsGrounded, ...) 으로 갱신하므로
        ///   Animator 가 IsGrounded = false 전환을 감지하여 Jump State 로 자동 전환.
        ///   별도 이벤트 없이 자동 처리됨.
        ///
        ///   2단 점프는 이미 공중이라 IsGrounded 가 바뀌지 않으므로
        ///   트리거로 명시적으로 알려야 DoubleJump State 로 전환됨.
        /// </summary>
        private void HandleDoubleJumped()
        {
            _animator.SetTrigger(_hashDoubleJump);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// IsFiring 파라미터를 설정한다.
        ///
        /// [호출 예시 — 전투 시스템에서]
        ///   GetComponent&lt;MovementAnimator&gt;().SetFiring(true);  // 발사 시작
        ///   GetComponent&lt;MovementAnimator&gt;().SetFiring(false); // 발사 중단
        ///
        ///   또는 PlayerMovementFacade 경유:
        ///   PlayerMovementFacade.Instance.SetFiring(true);
        ///
        /// [왜 이동 패키지에 IsFiring 이 있는가?]
        ///   Animator Controller 는 이동 레이어와 Attack 레이어를 함께 관리한다.
        ///   Animator 접근 창구를 MovementAnimator 하나로 통합하면
        ///   여러 시스템이 각자 Animator 를 직접 건드리지 않아도 됨.
        ///   전투 시스템이 없는 프로젝트는 이 메서드를 호출하지 않으면 됨.
        /// </summary>
        public void SetFiring(bool isFiring) => _isFiring = isFiring;
    }
}