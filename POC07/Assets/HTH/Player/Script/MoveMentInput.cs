// ============================================================
// MovementInput.cs  v1.0
// 플레이어 이동 패키지 — 입력 수신 컴포넌트
//
// [독립 패키지]
//   namespace : PlayerMovement (HOSE 종속 없음)
//   의존 대상 : MovementSettings SO, Unity New Input System
//
// [역할]
//   New Input System 을 직접 구독하여 이동/점프/대쉬 입력을 수신.
//   PlayerMover 에 이벤트로 전달 (직접 참조 없이 Action 으로 중계).
//
// [HOSE PlayerInputRelay 와의 차이]
//   HOSE : InputManager(싱글턴) → PlayerInputRelay → PlayerBody
//   독립 : MovementInput 이 InputSystem 직접 구독 → PlayerMover
//   외부 InputManager 없이 자체적으로 입력을 처리한다.
//
// [점프 차단 API]
//   외부(인벤토리UI 등)에서 점프를 막아야 할 때:
//     MovementInput.Instance.SetJumpBlocked(true);
//     MovementInput.Instance.SetJumpBlocked(false);
//
// [키 바인딩]
//   Inspector 에서 변경 가능한 구조로 설계.
//   현재는 코드 직접 바인딩 방식 (InputActionAsset 에셋 없이 동작).
// ============================================================

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerMovement
{
    /// <summary>
    /// 플레이어 이동 입력 수신 컴포넌트. (v1.0)
    ///
    /// [역할]
    ///   New Input System 이벤트를 직접 구독하고
    ///   이동/점프/대쉬 Action 으로 PlayerMover 에 전달.
    ///   외부 InputManager 싱글턴 없이 독립 동작.
    ///
    /// [외부 점프 차단]
    ///   인벤토리, 다이얼로그 등 점프를 막아야 하는 상황:
    ///   <code>MovementInput.BlockJump()</code>
    ///   <code>MovementInput.UnblockJump()</code>
    /// </summary>
    public class MovementInput : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector — 키 바인딩 (변경 가능)
        // ──────────────────────────────────────────

        [Header("── 키 바인딩 ──────────────────────")]

        /// <summary>
        /// 오른쪽 이동 키. 기본 D키.
        /// InputSystem 경로 문자열. 변경 가능.
        /// </summary>
        [Tooltip("오른쪽 이동 키. 기본: <Keyboard>/d")]
        [SerializeField] private string _keyMoveRight = "<Keyboard>/d";

        /// <summary> 왼쪽 이동 키. 기본 A키. </summary>
        [Tooltip("왼쪽 이동 키. 기본: <Keyboard>/a")]
        [SerializeField] private string _keyMoveLeft = "<Keyboard>/a";

        /// <summary> 오른쪽 화살표 키. 보조 이동. </summary>
        [Tooltip("오른쪽 화살표 보조 이동.")]
        [SerializeField] private string _keyMoveRightAlt = "<Keyboard>/rightArrow";

        /// <summary> 왼쪽 화살표 키. 보조 이동. </summary>
        [Tooltip("왼쪽 화살표 보조 이동.")]
        [SerializeField] private string _keyMoveLeftAlt = "<Keyboard>/leftArrow";

        /// <summary> 점프 키. 기본 Space. </summary>
        [Tooltip("점프 키. 기본: <Keyboard>/space")]
        [SerializeField] private string _keyJump = "<Keyboard>/space";

        /// <summary> 대쉬 키. 기본 LShift. </summary>
        [Tooltip("대쉬 키. 기본: <Keyboard>/leftShift")]
        [SerializeField] private string _keyDash = "<Keyboard>/leftShift";

        // ──────────────────────────────────────────
        // 내부 — InputAction
        // ──────────────────────────────────────────

        private InputActionMap _actionMap;
        private InputAction _actionMove;
        private InputAction _actionJump;
        private InputAction _actionDash;

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
        // 이벤트 — PlayerMover 가 구독
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

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InputAction 빌드 및 구독.
        /// </summary>
        private void Awake()
        {
            BuildActions();
            SubscribeActions();
            _actionMap.Enable();
        }

        /// <summary>
        /// 구독 해제 및 맵 비활성.
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeActions();
            _actionMap?.Disable();
            _actionMap?.Dispose();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — 점프 차단
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 점프 입력을 차단한다.
        /// 인벤토리, 다이얼로그 등 점프를 막아야 할 때 호출.
        ///
        /// [사용 예시]
        ///   인벤토리 열릴 때:
        ///     GetComponent&lt;MovementInput&gt;().BlockJump();
        ///   인벤토리 닫힐 때:
        ///     GetComponent&lt;MovementInput&gt;().UnblockJump();
        /// </summary>
        public void BlockJump() => _jumpBlocked = true;

        /// <summary>
        /// 점프 차단을 해제한다.
        /// </summary>
        public void UnblockJump() => _jumpBlocked = false;

        /// <summary>
        /// 현재 점프 차단 여부.
        /// </summary>
        public bool IsJumpBlocked => _jumpBlocked;

        // ══════════════════════════════════════════════════════
        // InputAction 빌드 — 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InputActionMap 을 코드로 직접 생성.
        /// Inspector 의 키 바인딩 문자열을 사용하여 동적으로 구성.
        ///
        /// [왜 코드 방식인가?]
        ///   InputActionAsset 에셋 파일 없이 동작.
        ///   패키지를 프로젝트에 복사하기만 하면 즉시 사용 가능.
        ///   키 바인딩은 Inspector 에서 변경 가능.
        ///
        /// [1DAxis Composite 주의]
        ///   1DAxis 는 float 을 반환한다. ReadValue&lt;Vector2&gt;() 로 읽으면 Exception 발생.
        ///   반드시 ReadValue&lt;float&gt;() 로 읽어야 한다.
        /// </summary>
        private void BuildActions()
        {
            _actionMap = new InputActionMap("PlayerMovement");

            // ── 이동 — 1DAxis Composite (float 반환) ──────────────────────
            // ⚠️ 1DAxis 는 float 반환 — ReadValue<float>() 사용 필수
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
        }

        /// <summary>
        /// 콜백 등록.
        ///
        /// [Move — ReadValue&lt;float&gt;() 사용 이유]
        ///   1DAxis Composite 은 float 을 반환한다.
        ///   ReadValue&lt;Vector2&gt;() 로 읽으면 InvalidOperationException 발생.
        ///   float 으로 읽은 후 OnMove(float) 로 발행한다.
        /// </summary>
        private void SubscribeActions()
        {
            // 1DAxis → float 읽기 (Vector2 아님!)
            _actionMove.performed += ctx => OnMove?.Invoke(ctx.ReadValue<float>());
            _actionMove.canceled += ctx => OnMove?.Invoke(0f);

            _actionJump.performed += _ => HandleJump();
            _actionDash.performed += _ => OnDash?.Invoke();
        }

        /// <summary>
        /// 콜백 해제.
        /// </summary>
        private void UnsubscribeActions()
        {
            if (_actionMove != null)
            {
                _actionMove.performed -= ctx => OnMove?.Invoke(ctx.ReadValue<float>());
                _actionMove.canceled -= ctx => OnMove?.Invoke(0f);
            }
            if (_actionJump != null) _actionJump.performed -= _ => HandleJump();
            if (_actionDash != null) _actionDash.performed -= _ => OnDash?.Invoke();
        }

        /// <summary>
        /// 점프 입력 처리. _jumpBlocked 체크 후 OnJump 발행.
        ///
        /// [차단 이유]
        ///   인벤토리 열람 중 Space 키가 인벤토리 조작으로 쓰일 때
        ///   점프가 발생하지 않도록 차단.
        ///   외부에서 BlockJump() 호출로 설정.
        /// </summary>
        private void HandleJump()
        {
            if (_jumpBlocked) return;
            OnJump?.Invoke();
        }
    }
}