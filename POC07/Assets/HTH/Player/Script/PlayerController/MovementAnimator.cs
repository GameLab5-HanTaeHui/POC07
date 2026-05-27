// ============================================================
// MovementAnimator.cs  v2.0
// 플레이어 이동 + 무기 콤보 Animator 파라미터 통합 동기화
//
// [v2.0 변경 — Animator 파라미터 전면 개편]
//   추가 파라미터:
//     VelocityY     (Float)   : 수직 속도 → Fall 전환 조건
//     Jump          (Trigger) : 1단 점프 진입
//     AttackCombo1  (Trigger) : 지상 1단 콤보 진입
//     AttackCombo2  (Trigger) : 지상 2단 콤보 진입 (윈도우 내)
//     AttackCombo3  (Trigger) : 지상 3단 콤보 진입 (윈도우 내)
//     AirAttack     (Trigger) : 공중 공격 진입
//
//   추가 이벤트 구독:
//     PlayerMover.OnJumped          → Jump Trigger
//     RustyKeyWeapon.OnCombo1Started → AttackCombo1 Trigger
//     RustyKeyWeapon.OnCombo2Started → AttackCombo2 Trigger
//     RustyKeyWeapon.OnCombo3Started → AttackCombo3 Trigger
//     RustyKeyWeapon.OnAirAttackStarted → AirAttack Trigger
//
//   기존 유지:
//     Speed / IsGrounded / IsFiring (매 프레임 Update)
//     Dash / DoubleJump (이벤트 Trigger)
//
// [파라미터 전체 목록 — 이 파일이 유일한 관리 지점]
//   Float   : Speed, VelocityY
//   Bool    : IsGrounded, IsFiring
//   Trigger : Jump, DoubleJump, Dash,
//             AttackCombo1, AttackCombo2, AttackCombo3, AirAttack
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 이동 + 무기 콤보 Animator 파라미터 통합 동기화 컴포넌트. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [이 파일이 하는 것]
    ///   - 모든 Animator.StringToHash 선언 및 캐싱
    ///   - Update 매 프레임: Speed / VelocityY / IsGrounded / IsFiring
    ///   - PlayerMover 이벤트 구독: Jump / DoubleJump / Dash Trigger
    ///   - PlayerWeaponBase 이벤트 구독: AttackCombo1/2/3 / AirAttack Trigger
    ///
    /// [Fall 전환 구조]
    ///   PlayerJump → (VelocityY < -0.1) → PlayerFall
    ///   코드는 VelocityY 를 매 프레임 SetFloat 만 하고
    ///   전환 조건 설정은 Animator Controller 에서 수행.
    ///
    /// [콤보 전환 구조]
    ///   AnyState → (AttackCombo1) → PlayerAttack01
    ///   PlayerAttack01 → (AttackCombo2 + ExitTime 0.5) → PlayerAttack02
    ///   PlayerAttack02 → (AttackCombo3 + ExitTime 0.5) → PlayerAttack03
    ///   PlayerAttack01/02/03 → ExitTime 1.0 → PlayerIdle
    ///   PlayerFall → (AirAttack) → PlayerAirAttack → ExitTime → PlayerFall
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(PlayerMover))]
    public class MovementAnimator : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Animator 해시 캐싱 — 이동
        // ──────────────────────────────────────────

        /// <summary> "Speed" Float — Mathf.Abs(MoveInput). 매 프레임 갱신. </summary>
        private static readonly int _hashSpeed = Animator.StringToHash("Speed");

        /// <summary>
        /// "VelocityY" Float — Rigidbody2D.velocity.y.
        /// Fall 전환 조건: VelocityY &lt; -0.1
        /// Animator Controller 에서 PlayerJump → PlayerFall 전환에 사용.
        /// </summary>
        private static readonly int _hashVelocityY = Animator.StringToHash("VelocityY");

        /// <summary> "IsGrounded" Bool — 지상/공중 판별. 매 프레임 갱신. </summary>
        private static readonly int _hashIsGrounded = Animator.StringToHash("IsGrounded");

        /// <summary> "IsFiring" Bool — 공격 상태. SetFiring() 외부 호출. </summary>
        private static readonly int _hashIsFiring = Animator.StringToHash("IsFiring");

        /// <summary>
        /// "Jump" Trigger — 1단 점프 진입.
        /// PlayerMover.OnJumped 수신 시 1회 SetTrigger.
        ///
        /// [1단 점프에 Trigger 가 필요한 이유]
        ///   기존 IsGrounded=false 만으로는 점프 버튼을 눌렀는지
        ///   낙하로 공중이 된 건지 구별 불가.
        ///   Jump Trigger 로 점프 의도를 명시적으로 전달.
        /// </summary>
        private static readonly int _hashJump = Animator.StringToHash("Jump");

        /// <summary>
        /// "DoubleJump" Trigger — 2단 점프 진입.
        /// PlayerMover.OnDoubleJumped 수신 시 1회 SetTrigger.
        /// </summary>
        private static readonly int _hashDoubleJump = Animator.StringToHash("DoubleJump");

        /// <summary>
        /// "Dash" Trigger — 대쉬 진입.
        /// PlayerMover.OnDashStarted 수신 시 1회 SetTrigger.
        /// </summary>
        private static readonly int _hashDash = Animator.StringToHash("Dash");

        // ──────────────────────────────────────────
        // Animator 해시 캐싱 — 콤보 공격
        // ──────────────────────────────────────────

        /// <summary>
        /// "AttackCombo1" Trigger — 1단 콤보 진입.
        /// PlayerWeaponAnimator 가 RustyKeyWeapon.OnCombo1Started 를 받아 여기로 전달.
        ///
        /// [Animator Controller 전환 설정]
        ///   AnyState → PlayerAttack01 조건: AttackCombo1 (IsGrounded=true 추가 권장)
        /// </summary>
        private static readonly int _hashAttackCombo1 = Animator.StringToHash("AttackCombo1");

        /// <summary>
        /// "AttackCombo2" Trigger — 2단 콤보 진입.
        ///
        /// [Animator Controller 전환 설정]
        ///   PlayerAttack01 → PlayerAttack02
        ///   조건: AttackCombo2 / HasExitTime=true / ExitTime=0.5
        ///   (클립 50% 이후 입력 감지 → 콤보 윈도우 구현)
        /// </summary>
        private static readonly int _hashAttackCombo2 = Animator.StringToHash("AttackCombo2");

        /// <summary>
        /// "AttackCombo3" Trigger — 3단 콤보(피니셔) 진입.
        ///
        /// [Animator Controller 전환 설정]
        ///   PlayerAttack02 → PlayerAttack03
        ///   조건: AttackCombo3 / HasExitTime=true / ExitTime=0.5
        /// </summary>
        private static readonly int _hashAttackCombo3 = Animator.StringToHash("AttackCombo3");

        /// <summary>
        /// "AirAttack" Trigger — 공중 공격 진입.
        ///
        /// [Animator Controller 전환 설정]
        ///   AnyState → PlayerAirAttack
        ///   조건: AirAttack (IsGrounded=false 추가 권장)
        /// </summary>
        private static readonly int _hashAirAttack = Animator.StringToHash("AirAttack");

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerMover _mover;
        private Animator _animator;

        /// <summary>
        /// 현재 구독 중인 무기 컴포넌트.
        /// SetWeapon() 으로 교체.
        /// </summary>
        private PlayerWeaponBase _currentWeapon;

        // ──────────────────────────────────────────
        // 외부 제어 상태
        // ──────────────────────────────────────────

        /// <summary> IsFiring Animator Bool 값. SetFiring() 으로 외부에서 설정. </summary>
        private bool _isFiring;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _mover = GetComponent<PlayerMover>();
            _animator = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning("[MovementAnimator] Animator 가 없습니다. 비활성화합니다.");
                enabled = false;
            }
        }

        private void Start()
        {
            if (_mover == null) return;

            // ── 이동 이벤트 구독 ──
            _mover.OnJumped += HandleJumped;
            _mover.OnDoubleJumped += HandleDoubleJumped;
            _mover.OnDashStarted += HandleDashStarted;
        }

        private void OnDestroy()
        {
            if (_mover != null)
            {
                _mover.OnJumped -= HandleJumped;
                _mover.OnDoubleJumped -= HandleDoubleJumped;
                _mover.OnDashStarted -= HandleDashStarted;
            }

            UnsubscribeWeapon(_currentWeapon);
        }

        /// <summary>
        /// 매 프레임 Float / Bool 파라미터 갱신.
        /// Trigger 는 이벤트 구독으로 1회만 처리.
        /// </summary>
        private void Update()
        {
            _animator.SetFloat(_hashSpeed, Mathf.Abs(_mover.MoveInput));
            _animator.SetFloat(_hashVelocityY, _mover.VelocityY);
            _animator.SetBool(_hashIsGrounded, _mover.IsGrounded);
            _animator.SetBool(_hashIsFiring, _isFiring);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러 — 이동 Trigger
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// PlayerMover.OnJumped → "Jump" Trigger 1회 발행.
        /// </summary>
        private void HandleJumped() => _animator.SetTrigger(_hashJump);

        /// <summary>
        /// PlayerMover.OnDoubleJumped → "DoubleJump" Trigger 1회 발행.
        /// </summary>
        private void HandleDoubleJumped() => _animator.SetTrigger(_hashDoubleJump);

        /// <summary>
        /// PlayerMover.OnDashStarted → "Dash" Trigger 1회 발행.
        /// </summary>
        private void HandleDashStarted() => _animator.SetTrigger(_hashDash);

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러 — 콤보 Trigger
        // ══════════════════════════════════════════════════════

        /// <summary> RustyKeyWeapon.OnCombo1Started → "AttackCombo1" Trigger. </summary>
        private void HandleCombo1() => _animator.SetTrigger(_hashAttackCombo1);

        /// <summary> RustyKeyWeapon.OnCombo2Started → "AttackCombo2" Trigger. </summary>
        private void HandleCombo2() => _animator.SetTrigger(_hashAttackCombo2);

        /// <summary> RustyKeyWeapon.OnCombo3Started → "AttackCombo3" Trigger. </summary>
        private void HandleCombo3() => _animator.SetTrigger(_hashAttackCombo3);

        /// <summary> RustyKeyWeapon.OnAirAttackStarted → "AirAttack" Trigger. </summary>
        private void HandleAirAttack() => _animator.SetTrigger(_hashAirAttack);

        // ══════════════════════════════════════════════════════
        // 무기 교체 — 이벤트 재구독
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 무기 컴포넌트를 교체하고 이벤트를 재구독한다.
        /// PlayerWeaponController.ActivateWeapon() 에서 호출.
        ///
        /// [PlayerWeaponAnimator 와의 분리]
        ///   Animator Trigger 발행 → 이 컴포넌트(MovementAnimator)
        ///   Weapon 오브젝트 스윙 이동 → PlayerWeaponAnimator
        ///   둘 다 무기 이벤트를 구독하되 역할이 분리됨.
        /// </summary>
        public void SetWeapon(PlayerWeaponBase newWeapon)
        {
            UnsubscribeWeapon(_currentWeapon);
            _currentWeapon = newWeapon;
            SubscribeWeapon(_currentWeapon);
        }

        private void SubscribeWeapon(PlayerWeaponBase weapon)
        {
            if (weapon is RustyKeyWeapon rusty)
            {
                rusty.OnCombo1Started += HandleCombo1;
                rusty.OnCombo2Started += HandleCombo2;
                rusty.OnCombo3Started += HandleCombo3;
                rusty.OnAirAttackStarted += HandleAirAttack;
            }
            // 추후 HookKeyWeapon 등 추가 시 else if 로 확장
        }

        private void UnsubscribeWeapon(PlayerWeaponBase weapon)
        {
            if (weapon is RustyKeyWeapon rusty)
            {
                rusty.OnCombo1Started -= HandleCombo1;
                rusty.OnCombo2Started -= HandleCombo2;
                rusty.OnCombo3Started -= HandleCombo3;
                rusty.OnAirAttackStarted -= HandleAirAttack;
            }
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// IsFiring 파라미터를 설정한다.
        /// PlayerMovementFacade.SetFiring() 경유로 호출.
        /// </summary>
        public void SetFiring(bool isFiring) => _isFiring = isFiring;
    }
}