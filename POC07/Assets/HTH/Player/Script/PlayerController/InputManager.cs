// ============================================================
// InputManager.cs  v1.0
// 플레이어 입력 통합 관리 컴포넌트
//
// [역할]
//   플레이어가 사용하는 모든 키 입력을 하나의 컴포넌트에서 관리.
//   기존 MovementInput(이동/점프/대쉬) + WeaponInput(공격) 병합.
//   New Input System 을 직접 구독하여 이벤트로 각 시스템에 전달.
//
// [구독 대상]
//   PlayerMover        : OnMove / OnJump / OnDash
//   PlayerWeaponBase   : OnAttack / OnAirAttack (공중 여부는 외부에서 판별)
//
// [점프 차단 API]
//   외부(인벤토리 UI 등)에서 점프를 막아야 할 때:
//     InputManager.Instance.BlockJump();
//     InputManager.Instance.UnblockJump();
//
// [키 바인딩]
//   Inspector 에서 변경 가능한 구조.
//   InputActionAsset 에셋 없이 코드 직접 바인딩 방식으로 동작.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KEY
{
    /// <summary>
    /// 플레이어 입력 통합 관리 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [이 파일이 하는 것]
    ///   - 이동 / 점프 / 대쉬 / 공격 입력을 단일 컴포넌트에서 수신
    ///   - 각 입력을 이벤트(Action)로 발행하여 관련 시스템에 전달
    ///   - 점프 차단(BlockJump) API 제공
    ///
    /// [이 파일이 하지 않는 것]
    ///   - 물리 이동 처리 (PlayerMover 담당)
    ///   - 무기 공격 처리 (PlayerWeaponBase 담당)
    ///   - 공중/지상 판별 (PlayerMover.IsGrounded 를 외부에서 참조)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 싱글턴
        // ──────────────────────────────────────────

        /// <summary>
        /// 전역 단일 인스턴스.
        /// 씬 전환 시 Player 가 파괴되면 null 로 초기화됨.
        /// DontDestroyOnLoad 사용 금지 — 씬마다 새로 생성.
        /// </summary>
        public static InputManager Instance { get; private set; }

        // ──────────────────────────────────────────
        // Inspector — 이동 키 바인딩
        // ──────────────────────────────────────────

        [Header("── 이동 키 바인딩 ──────────────────────")]

        /// <summary> 오른쪽 이동 키. 기본 D키. </summary>
        [Tooltip("오른쪽 이동 키. 기본: <Keyboard>/d")]
        [SerializeField] private string _keyMoveRight = "<Keyboard>/d";

        /// <summary> 왼쪽 이동 키. 기본 A키. </summary>
        [Tooltip("왼쪽 이동 키. 기본: <Keyboard>/a")]
        [SerializeField] private string _keyMoveLeft = "<Keyboard>/a";

        /// <summary> 오른쪽 화살표 보조 이동. </summary>
        [Tooltip("오른쪽 화살표 보조 이동.")]
        [SerializeField] private string _keyMoveRightAlt = "<Keyboard>/rightArrow";

        /// <summary> 왼쪽 화살표 보조 이동. </summary>
        [Tooltip("왼쪽 화살표 보조 이동.")]
        [SerializeField] private string _keyMoveLeftAlt = "<Keyboard>/leftArrow";

        /// <summary> 점프 키. 기본 Space. </summary>
        [Tooltip("점프 키. 기본: <Keyboard>/space")]
        [SerializeField] private string _keyJump = "<Keyboard>/space";

        /// <summary> 대쉬 키. 기본 LShift. </summary>
        [Tooltip("대쉬 키. 기본: <Keyboard>/leftShift")]
        [SerializeField] private string _keyDash = "<Keyboard>/leftShift";

        // ──────────────────────────────────────────
        // Inspector — 무기 키 바인딩
        // ──────────────────────────────────────────

        [Header("── 무기 키 바인딩 ──────────────────────")]

        /// <summary>
        /// 공격 키. 기본 마우스 좌클릭.
        /// 지상/공중 여부는 PlayerMover.IsGrounded 로 판별.
        /// </summary>
        [Tooltip("공격 키. 기본: <Mouse>/leftButton")]
        [SerializeField] private string _keyAttack = "<Mouse>/leftButton";

        // ──────────────────────────────────────────
        // 내부 — InputAction
        // ──────────────────────────────────────────

        private InputActionMap _actionMap;

        /// <summary> 수평 이동 입력 Action. 1DAxis Composite (float 반환). </summary>
        private InputAction _actionMove;

        /// <summary> 점프 버튼 Action. </summary>
        private InputAction _actionJump;

        /// <summary> 대쉬 버튼 Action. </summary>
        private InputAction _actionDash;

        /// <summary> 공격 버튼 Action. </summary>
        private InputAction _actionAttack;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 점프 차단 플래그.
        /// true 이면 OnJump 이벤트를 발행하지 않는다.
        /// BlockJump() / UnblockJump() 로 제어.
        /// </summary>
        private bool _jumpBlocked;

        // ──────────────────────────────────────────
        // 이벤트 — 이동 (PlayerMover 구독)
        // ──────────────────────────────────────────

        /// <summary>
        /// 수평 이동 입력값 변경 시 발행.
        /// float: -1(왼쪽) ~ 1(오른쪽). 0 = 입력 없음.
        /// </summary>
        public event Action<float> OnMove;

        /// <summary>
        /// 점프 버튼 pressed 시 발행.
        /// _jumpBlocked == true 이면 발행하지 않음.
        /// </summary>
        public event Action OnJump;

        /// <summary>
        /// 대쉬 버튼 pressed 시 발행.
        /// </summary>
        public event Action OnDash;

        // ──────────────────────────────────────────
        // 이벤트 — 무기 (PlayerWeaponBase 구독)
        // ──────────────────────────────────────────

        /// <summary>
        /// 공격 버튼 pressed 시 발행.
        /// 지상/공중 판별은 구독 측(PlayerWeaponBase)에서
        /// PlayerMovementFacade.Instance.IsGrounded 로 수행.
        /// </summary>
        public event Action OnAttack;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 싱글턴 설정 + InputAction 빌드 및 활성화.
        /// </summary>
        private void Awake()
        {
            // ── 싱글턴 보장 ──────────────────────
            // Destroy(gameObject) 가 아닌 Destroy(this) 로 컴포넌트만 제거.
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            BuildActions();
            SubscribeActions();
            _actionMap.Enable();
        }

        /// <summary>
        /// 구독 해제 및 InputActionMap 정리.
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            UnsubscribeActions();
            _actionMap?.Disable();
            _actionMap?.Dispose();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — 점프 차단
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 점프 입력을 차단한다.
        ///
        /// [사용 예시]
        ///   인벤토리 열릴 때:
        ///     InputManager.Instance.BlockJump();
        ///   인벤토리 닫힐 때:
        ///     InputManager.Instance.UnblockJump();
        /// </summary>
        public void BlockJump() => _jumpBlocked = true;

        /// <summary>
        /// 점프 차단을 해제한다.
        /// </summary>
        public void UnblockJump() => _jumpBlocked = false;

        /// <summary> 현재 점프 차단 여부. </summary>
        public bool IsJumpBlocked => _jumpBlocked;

        // ══════════════════════════════════════════════════════
        // InputAction 빌드 — 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InputActionMap 을 코드로 직접 생성.
        /// Inspector 의 키 바인딩 문자열을 사용하여 동적으로 구성.
        ///
        /// [1DAxis Composite 주의]
        ///   1DAxis 는 float 을 반환한다.
        ///   반드시 ReadValue&lt;float&gt;() 로 읽어야 한다.
        /// </summary>
        private void BuildActions()
        {
            _actionMap = new InputActionMap("PlayerInput");

            // ── 이동 — 1DAxis Composite (float 반환) ──────────────────────
            _actionMove = _actionMap.AddAction("Move", InputActionType.Value);
            _actionMove.AddCompositeBinding("1DAxis")
                .With("Negative", _keyMoveLeft)
                .With("Positive", _keyMoveRight);
            _actionMove.AddCompositeBinding("1DAxis")
                .With("Negative", _keyMoveLeftAlt)
                .With("Positive", _keyMoveRightAlt);
            _actionMove.AddBinding("<Gamepad>/leftStick/x");

            // ── 점프 ──────────────────────
            _actionJump = _actionMap.AddAction("Jump", InputActionType.Button);
            _actionJump.AddBinding(_keyJump);
            _actionJump.AddBinding("<Gamepad>/buttonSouth");

            // ── 대쉬 ──────────────────────
            _actionDash = _actionMap.AddAction("Dash", InputActionType.Button);
            _actionDash.AddBinding(_keyDash);
            _actionDash.AddBinding("<Gamepad>/buttonEast");

            // ── 공격 ──────────────────────
            _actionAttack = _actionMap.AddAction("Attack", InputActionType.Button);
            _actionAttack.AddBinding(_keyAttack);
            _actionAttack.AddBinding("<Gamepad>/buttonWest");
        }

        /// <summary>
        /// 이벤트 콜백 등록.
        ///
        /// [Move — ReadValue&lt;float&gt;() 사용 이유]
        ///   1DAxis Composite 은 float 을 반환한다.
        ///   ReadValue&lt;Vector2&gt;() 로 읽으면 InvalidOperationException 발생.
        /// </summary>
        private void SubscribeActions()
        {
            _actionMove.performed += ctx => OnMove?.Invoke(ctx.ReadValue<float>());
            _actionMove.canceled += _ => OnMove?.Invoke(0f);
            _actionJump.performed += _ => HandleJump();
            _actionDash.performed += _ => OnDash?.Invoke();
            _actionAttack.performed += _ => OnAttack?.Invoke();
        }

        /// <summary>
        /// 이벤트 콜백 해제.
        /// 람다를 변수로 저장하지 않는 간이 해제 방식 — Dispose 로 최종 정리.
        /// </summary>
        private void UnsubscribeActions()
        {
            _actionMove?.Disable();
            _actionJump?.Disable();
            _actionDash?.Disable();
            _actionAttack?.Disable();
        }

        /// <summary>
        /// 점프 입력 처리. _jumpBlocked 체크 후 OnJump 발행.
        /// </summary>
        private void HandleJump()
        {
            if (_jumpBlocked) return;
            OnJump?.Invoke();
        }
    }
}