// ============================================================
// InputManager.cs  v2.4
// 플레이어 입력 통합 관리 컴포넌트
//
// [v2.2 변경 — KeySwap 중 이동 허용]
//   이동 / 점프 / 대쉬는 KeySwap 모드 중에도 항상 작동.
//   공격만 KeySwap 모드 시 슬롯 8번으로 전환.
//
// [ActionMap 구조]
//   _inGameMap  : Move / Jump / Dash / Attack
//   _keySwapMap : SwapMode / Slot0~15
//   두 맵 동시 Enable.
//   이동 이벤트 차단 제거 — _isKeySwapMode 관계없이 항상 발행.
//   공격만 분기 유지.
//
// [키 바인딩]
//   이동 : ← →     (항상)
//   점프 : Space    (항상)
//   대쉬 : LShift   (항상)
//   공격 : A        (KeySwap OFF 시) / 슬롯 8 (KeySwap ON 시)
//   KeySwap 모드 : LCtrl 누름 유지
//   슬롯 0~3  : 1 2 3 4
//   슬롯 4~7  : Q W E R
//   슬롯 8~11 : A S D F
//   슬롯 12~15: Z X C V
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
    /// 플레이어 입력 통합 관리 컴포넌트. (v2.4)
    ///
    /// ────────────────────────────────────────────────────
    /// [KeySwap 모드 동작]
    ///   이동 / 점프 / 대쉬 : 모드 무관 항상 작동
    ///   공격(A)            : 모드 OFF → OnAttack
    ///                        모드 ON  → OnKeySwap(8)
    ///   슬롯 키            : 모드 ON 시에만 OnKeySwap(index) 발행
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 싱글턴
        // ──────────────────────────────────────────

        public static InputManager Instance { get; private set; }

        // ──────────────────────────────────────────
        // Inspector — InGame 키 바인딩
        // ──────────────────────────────────────────

        [Header("── InGame 키 바인딩 ──────────────────────")]

        [Tooltip("오른쪽 이동 키.")]
        [SerializeField] private Key _keyMoveRight = Key.RightArrow;

        [Tooltip("왼쪽 이동 키.")]
        [SerializeField] private Key _keyMoveLeft = Key.LeftArrow;

        [Tooltip("점프 키.")]
        [SerializeField] private Key _keyJump = Key.Space;

        [Tooltip("대쉬 키.")]
        [SerializeField] private Key _keyDash = Key.LeftShift;

        /// <summary>
        /// 공격 키. 기본 A.
        /// KeySwap 모드 ON → 슬롯 8번으로 전환.
        /// _keySwapSlots[8] 과 동일한 키여야 함.
        /// </summary>
        [Tooltip("공격 키. KeySwap 모드 시 슬롯 8번으로 전환.")]
        [SerializeField] private Key _keyAttack = Key.A;

        // ──────────────────────────────────────────
        // Inspector — KeySwap 키 바인딩
        // ──────────────────────────────────────────

        [Header("── KeySwap 키 바인딩 ──────────────────────")]

        [Tooltip("KeySwap 모드 키 (누름 유지).")]
        [SerializeField] private Key _keySwapMode = Key.LeftCtrl;

        /// <summary>
        /// 슬롯 키 16개. 순서: 1234 / QWER / ASDF / ZXCV
        /// 슬롯 8 (A) 은 공격키와 겸용 — InGame Attack 콜백에서 분기 처리.
        /// </summary>
        [Tooltip("KeySwap 슬롯 키 16개. 순서: 1234 / QWER / ASDF / ZXCV")]
        [SerializeField]
        private Key[] _keySwapSlots = new Key[]
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Q,      Key.W,      Key.E,      Key.R,
            Key.A,      Key.S,      Key.D,      Key.F,   // 슬롯 8=A 겸용
            Key.Z,      Key.X,      Key.C,      Key.V,
        };


        [Header("── 차징 공격 키 바인딩 ──────────────────────")]

        /// <summary>
        /// 차징 공격 키. 기본 S.
        /// 누름 유지 → 차징 시작 / 뗌 → 발사 (최소 차징 충족 시).
        /// KeySwap 슬롯 9번(S)과 겸용이므로 KeySwap 모드 중 차징 불가.
        /// </summary>
        [Tooltip("차징 공격 키. 기본: S (누름 → 차징 / 뗌 → 발사)")]
        [SerializeField] private Key _keyCharge = Key.S;

        /// <summary> 차징 조준 위. 기본 ↑. </summary>
        [Tooltip("차징 조준 위. 기본: ↑")]
        [SerializeField] private Key _keyAimUp = Key.UpArrow;

        /// <summary> 차징 조준 아래. 기본 ↓. </summary>
        [Tooltip("차징 조준 아래. 기본: ↓")]
        [SerializeField] private Key _keyAimDown = Key.DownArrow;

        // ──────────────────────────────────────────
        // InputAction — 단일 인스턴스 유지
        // ──────────────────────────────────────────

        private InputActionMap _inGameMap;
        private InputActionMap _keySwapMap;

        // InGame
        private InputAction _actionMove;
        private InputAction _actionJump;
        private InputAction _actionDash;
        private InputAction _actionAttack;

        // Charge
        private InputAction _actionCharge;
        private InputAction _actionAimUp;
        private InputAction _actionAimDown;

        // KeySwap
        private InputAction _actionSwapMode;
        private InputAction[] _actionSwapSlots;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private bool _jumpBlocked;
        private bool _moveBlocked;
        private bool _dashBlocked;
        private float _verticalInput;
        private bool _isKeySwapMode;
        private bool _isAttackHeld;

        // ──────────────────────────────────────────
        // 이벤트 — InGame (항상 발행)
        // ──────────────────────────────────────────

        /// <summary> 수평 이동 입력. KeySwap 모드 중에도 발행. </summary>
        public event Action<float> OnMove;

        /// <summary> 점프. _jumpBlocked 시 차단. KeySwap 모드 중에도 발행. </summary>
        public event Action OnJump;

        /// <summary> 대쉬. KeySwap 모드 중에도 발행. </summary>
        public event Action OnDash;

        /// <summary>
        /// 공격. KeySwap 모드 OFF 시에만 발행.
        /// KeySwap ON 시 A 키는 OnKeySwap(8) 으로 전환.
        /// </summary>
        public event Action OnAttack;

        // ──────────────────────────────────────────
        // 이벤트 — KeySwap
        // ──────────────────────────────────────────

        /// <summary>
        /// KeySwap 모드 전환 시 발행.
        /// true = 진입 / false = 해제.
        /// </summary>
        public event Action<bool> OnKeySwapModeChanged;

        /// <summary>
        /// KeySwap 모드 중 슬롯 키 입력 시 발행.
        /// 파라미터: 슬롯 인덱스 (0~15).
        /// </summary>
        public event Action<int> OnKeySwap;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 차징 시작 (S 누름).
        /// PlayerChargeAttack 이 구독하여 차징 로직 시작.
        /// KeySwap 모드 중에는 발행하지 않음.
        /// </summary>
        public event Action OnChargeStart;

        /// <summary>
        /// 차징 해제 (S 뗌).
        /// PlayerChargeAttack 이 구독하여 발사 or 취소 판단.
        /// </summary>
        public event Action OnChargeRelease;

        /// <summary>
        /// 차징 중 좌우 방향키 입력 시 발행.
        /// 파라미터: +1 = 오른쪽 / -1 = 왼쪽.
        /// PlayerChargeAttack 이 구독하여 FacingDirection 갱신.
        /// 차징 중이 아닐 때는 PlayerChargeAttack 내부에서 무시.
        /// </summary>
        public event Action<float> OnChargeFlip;

        /// <summary>
        /// 조준 방향 입력 상태.
        /// 파라미터: +1.0 = 위 누름 / -1.0 = 아래 누름 / 0.0 = 뗌.
        /// PlayerChargeAttack 이 매 프레임 값을 읽어 부드럽게 각도 변경.
        /// </summary>
        public event Action<float> OnAimAdjust;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 수직 입력값.
        /// +1 = ↑ 누름 / -1 = ↓ 누름 / 0 = 입력 없음.
        /// </summary>
        public float VerticalInput => _verticalInput;

        /// <summary> 현재 KeySwap 모드 여부. </summary>
        public bool IsKeySwapMode => _isKeySwapMode;

        /// <summary> 현재 점프 차단 여부. </summary>
        public bool IsJumpBlocked => _jumpBlocked;

        /// <summary>
        /// A키(공격) 홀드 중 여부.
        /// BossExecutionHandler 에서 처형 입력 감지에 사용.
        /// </summary>
        public bool IsAttackHeld => _isAttackHeld;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            BuildInGameMap();
            BuildKeySwapMap();

            // 두 맵 동시 Enable
            _inGameMap.Enable();
            _keySwapMap.Enable();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            _inGameMap?.Disable();
            _inGameMap?.Dispose();
            _keySwapMap?.Disable();
            _keySwapMap?.Dispose();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary> 점프 차단. </summary>
        public void BlockJump() => _jumpBlocked = true;

        /// <summary> 점프 차단 해제. </summary>
        public void UnblockJump() => _jumpBlocked = false;

        /// <summary> 이동 차단. 차징 중 호출. </summary>
        public void BlockMove() => _moveBlocked = true;

        /// <summary> 이동 차단 해제. </summary>
        public void UnblockMove() => _moveBlocked = false;

        /// <summary> 대쉬 차단. 차징 중 호출. </summary>
        public void BlockDash() => _dashBlocked = true;

        /// <summary> 대쉬 차단 해제. </summary>
        public void UnblockDash() => _dashBlocked = false;

        // ══════════════════════════════════════════════════════
        // Key enum → Input System 경로 변환
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Key enum 을 Input System 바인딩 경로 문자열로 변환.
        ///
        /// [1차] Keyboard.current 컨트롤 순회 → keyCode 일치 경로 반환
        /// [2차] 폴백 — Digit1~4 → "1"~"4", 나머지 camelCase 변환
        /// </summary>
        private static string KeyToPath(Key key)
        {
            if (Keyboard.current != null)
            {
                foreach (var control in Keyboard.current.allControls)
                {
                    if (control is UnityEngine.InputSystem.Controls.KeyControl kc
                        && kc.keyCode == key)
                        return control.path;
                }
            }

            string name = key.ToString();
            if (name.StartsWith("Digit"))
                name = name.Substring(5);

            return $"<Keyboard>/{char.ToLower(name[0]) + name.Substring(1)}";
        }

        // ══════════════════════════════════════════════════════
        // InGame ActionMap 빌드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// InGame ActionMap 생성.
        /// 이동 / 점프 / 대쉬 / 공격 모두 여기서 Action 생성.
        /// KeySwap 모드 중에도 이동 / 점프 / 대쉬는 차단하지 않음.
        /// 공격(A)만 _isKeySwapMode 분기로 슬롯 8번 전환.
        /// </summary>
        private void BuildInGameMap()
        {
            _inGameMap = new InputActionMap("InGame");

            // ── 이동 ──────────────────────
            _actionMove = _inGameMap.AddAction("Move", InputActionType.Value);
            _actionMove.AddCompositeBinding("1DAxis")
                .With("Negative", KeyToPath(_keyMoveLeft))
                .With("Positive", KeyToPath(_keyMoveRight));
            _actionMove.AddBinding("<Gamepad>/leftStick/x");

            // ── 점프 ──────────────────────
            _actionJump = _inGameMap.AddAction("Jump", InputActionType.Button);
            _actionJump.AddBinding(KeyToPath(_keyJump));
            _actionJump.AddBinding("<Gamepad>/buttonSouth");

            // ── 대쉬 ──────────────────────
            _actionDash = _inGameMap.AddAction("Dash", InputActionType.Button);
            _actionDash.AddBinding(KeyToPath(_keyDash));
            _actionDash.AddBinding("<Gamepad>/buttonEast");

            // ── 공격 ──────────────────────
            _actionAttack = _inGameMap.AddAction("Attack", InputActionType.Button);
            _actionAttack.AddBinding(KeyToPath(_keyAttack));
            _actionAttack.AddBinding("<Gamepad>/buttonWest");

            // ── 콜백 ──────────────────────

            // 이동 — _moveBlocked 시 차단
            // 차징 중이어도 OnChargeFlip 은 항상 발행 (방향 전환용)
            _actionMove.performed += ctx =>
            {
                float value = ctx.ReadValue<float>();
                if (!_moveBlocked) OnMove?.Invoke(value);

                // 차징 중 좌우 입력 → 방향 전환 이벤트 발행
                if (value != 0f) OnChargeFlip?.Invoke(value > 0f ? 1f : -1f);
            };
            _actionMove.canceled += _ => OnMove?.Invoke(0f); // 뗌은 항상 0 발행 (멈춤 보장)

            // 점프 — 항상 발행 (_jumpBlocked 는 HandleJump 내부에서 처리)
            _actionJump.performed += _ => HandleJump();

            // 대쉬 — _dashBlocked 시 차단
            _actionDash.performed += _ =>
            {
                if (!_dashBlocked) OnDash?.Invoke();
            };

            // 공격 — KeySwap 모드 시 슬롯 8번 전환
            _actionAttack.performed += _ =>
            {
                _isAttackHeld = true;
                if (_isKeySwapMode) OnKeySwap?.Invoke(8);
                else OnAttack?.Invoke();
            };

            _actionAttack.canceled += _ =>
            {
                _isAttackHeld = false;
            };

            // ── 차징 ──────────────────────
            _actionCharge = _inGameMap.AddAction("Charge", InputActionType.Button);
            _actionCharge.AddBinding(KeyToPath(_keyCharge));

            // AimUp / AimDown — Value 타입 (누름=1.0, 뗌=0.0 전달)
            // PlayerChargeAttack 이 매 프레임 값을 읽어 부드럽게 각도 조절
            _actionAimUp = _inGameMap.AddAction("AimUp", InputActionType.Value);
            _actionAimUp.AddBinding(KeyToPath(_keyAimUp));

            _actionAimDown = _inGameMap.AddAction("AimDown", InputActionType.Value);
            _actionAimDown.AddBinding(KeyToPath(_keyAimDown));

            _actionCharge.performed += _ =>
            {
                if (!_isKeySwapMode) OnChargeStart?.Invoke();
            };
            _actionCharge.canceled += _ =>
            {
                if (!_isKeySwapMode) OnChargeRelease?.Invoke();
            };

            // 누름(+1) / 뗌(0) 모두 발행 — PlayerChargeAttack 이 상태 유지
            _actionAimUp.performed += _ => { OnAimAdjust?.Invoke(+1f); _verticalInput = +1f; };
            _actionAimUp.canceled += _ => { OnAimAdjust?.Invoke(0f); if (_verticalInput > 0f) _verticalInput = 0f; };
            _actionAimDown.performed += _ => { OnAimAdjust?.Invoke(-1f); _verticalInput = -1f; };
            _actionAimDown.canceled += _ => { OnAimAdjust?.Invoke(0f); if (_verticalInput < 0f) _verticalInput = 0f; };
        }

        // ══════════════════════════════════════════════════════
        // KeySwap ActionMap 빌드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// KeySwap ActionMap 생성.
        /// SwapMode 토글 + 슬롯 15개 (슬롯 8=A 제외).
        /// 이동 / 점프 / 대쉬는 InGame ActionMap 에서 단일 관리.
        /// 별도 추가 없음 — 이중 등록 / 이중 발행 방지.
        /// </summary>
        private void BuildKeySwapMap()
        {
            _keySwapMap = new InputActionMap("KeySwap");

            // ── SwapMode 토글 ──────────────────────
            _actionSwapMode = _keySwapMap.AddAction("SwapMode", InputActionType.Button);
            _actionSwapMode.AddBinding(KeyToPath(_keySwapMode));

            _actionSwapMode.performed += _ => EnterKeySwapMode();
            _actionSwapMode.canceled += _ => ExitKeySwapMode();

            // ── 슬롯 15개 (슬롯 8=A 제외) ──────────────────────
            // 슬롯 8 (A 키) 는 _actionAttack 콜백에서 분기 처리.
            // 여기서 중복 등록하면 KeySwap ON 시 이중 발행 발생.
            _actionSwapSlots = new InputAction[_keySwapSlots.Length];

            for (int i = 0; i < _keySwapSlots.Length; i++)
            {
                if (i == 8) { _actionSwapSlots[i] = null; continue; }

                int capturedIndex = i;
                var action = _keySwapMap.AddAction($"Slot{i}", InputActionType.Button);
                action.AddBinding(KeyToPath(_keySwapSlots[i]));
                action.performed += _ =>
                {
                    if (_isKeySwapMode) OnKeySwap?.Invoke(capturedIndex);
                };

                _actionSwapSlots[i] = action;
            }
        }

        // ══════════════════════════════════════════════════════
        // KeySwap 모드 전환
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// KeySwap 모드 진입.
        /// 이동은 계속 허용. 공격만 슬롯 교체로 전환.
        /// </summary>
        private void EnterKeySwapMode()
        {
            if (_isKeySwapMode) return;
            _isKeySwapMode = true;
            OnKeySwapModeChanged?.Invoke(true);
        }

        /// <summary>
        /// KeySwap 모드 해제.
        /// </summary>
        private void ExitKeySwapMode()
        {
            if (!_isKeySwapMode) return;
            _isKeySwapMode = false;
            OnKeySwapModeChanged?.Invoke(false);
        }

        // ══════════════════════════════════════════════════════
        // 내부 핸들러
        // ══════════════════════════════════════════════════════
        private void HandleJump()
        {
            if (_jumpBlocked) return;
            OnJump?.Invoke();
        }
    }
}