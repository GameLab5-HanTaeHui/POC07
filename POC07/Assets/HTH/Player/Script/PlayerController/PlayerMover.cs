// ============================================================
// PlayerMover.cs  v1.4
// 플레이어 이동 패키지 — 이동 / 점프 / 대쉬 물리 컴포넌트
//
// [v1.4 변경]
//   OnJumped 이벤트 추가 (1단 점프 순간 발행).
//   VelocityY 프로퍼티 추가 (MovementAnimator Fall 전환 조건용).
//   기존 OnDoubleJumped 는 유지.
//
// [MovementAnimator 가 구독하는 이벤트]
//   OnJumped       : 1단 점프 실행 순간 → Jump Trigger
//   OnDoubleJumped : 2단 점프 실행 순간 → DoubleJump Trigger
//   OnDashStarted  : 대쉬 시작 순간    → Dash Trigger
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 플레이어 이동 / 점프 / 대쉬 물리 전담 컴포넌트. (v1.4)
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary> 이동 수치 설정 ScriptableObject. </summary>
        [Tooltip("MovementSettings SO. 필수 연결.")]
        [SerializeField] private MovementSettings _settings;

        /// <summary> 지면 감지 기준점 Transform. </summary>
        [Tooltip("발 아래 지면 감지 기준점. 빈 오브젝트 연결.")]
        [SerializeField] private Transform _groundCheck;

        [Header("── 선택 연결 ──────────────────────")]

        /// <summary> 대쉬 잔상 TrailRenderer. 미연결 시 잔상 없음. </summary>
        [Tooltip("대쉬 잔상 TrailRenderer. 미연결 시 잔상 없음.")]
        [SerializeField] private TrailRenderer _trailRenderer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태 — 이동
        // ──────────────────────────────────────────

        private float _moveInput;
        private float _facingDirection = 1f;
        private Tween _dashTween;

        // ──────────────────────────────────────────
        // 내부 상태 — 점프
        // ──────────────────────────────────────────

        private bool _isGrounded;
        private bool _wasGrounded;
        private int _remainingJumps;
        private float _coyoteTimer;
        private float _jumpBufferTimer;

        // ──────────────────────────────────────────
        // 내부 상태 — 대쉬
        // ──────────────────────────────────────────

        private bool _isDashing;
        private float _dashCooldownTimer;

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// 1단 점프 실행 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("Jump") 처리.
        /// </summary>
        public event System.Action OnJumped;

        /// <summary>
        /// 2단 점프 실행 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("DoubleJump") 처리.
        /// </summary>
        public event System.Action OnDoubleJumped;

        /// <summary>
        /// 대쉬 시작 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("Dash") 처리.
        /// </summary>
        public event System.Action OnDashStarted;

        /// <summary>
        /// 좌우 방향이 실제로 바뀐 순간 1회 발행.
        /// 파라미터: 새 방향 (1 = 오른쪽, -1 = 왼쪽).
        /// PlayerWeaponMover 가 구독하여 Weapon localPosition X 부호를 반전.
        /// </summary>
        public event System.Action<float> OnFlipped;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 현재 접지 여부. </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary> 현재 대쉬 중 여부. </summary>
        public bool IsDashing => _isDashing;

        /// <summary> 현재 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽. </summary>
        public float FacingDirection => _facingDirection;

        /// <summary> 현재 이동 입력값. MovementAnimator.Speed 계산에 사용. </summary>
        public float MoveInput => _moveInput;

        /// <summary>
        /// 현재 수직 속도.
        /// MovementAnimator 가 Fall 전환 조건으로 사용.
        /// 음수 = 하강 중.
        /// </summary>
        public float VelocityY => _rigid2D != null ? _rigid2D.linearVelocity.y : 0f;

        /// <summary> 연결된 Settings SO. 외부에서 수치 읽기용. </summary>
        public MovementSettings Settings => _settings;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_settings == null)
            {
                Debug.LogError("[PlayerMover] MovementSettings SO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            if (_groundCheck == null)
                Debug.LogWarning("[PlayerMover] _groundCheck 가 연결되지 않았습니다.");

            _rigid2D.gravityScale = _settings.GravityScale;
            _remainingJumps = _settings.MaxJumpCount;

            if (_settings.GroundLayer.value == 0)
                Debug.LogWarning("[PlayerMover] GroundLayer 가 설정되지 않았습니다.");
        }

        private void Start()
        {
            if (InputManager.Instance == null)
            {
                Debug.LogError("[PlayerMover] InputManager 가 없습니다.");
                return;
            }

            InputManager.Instance.OnMove += HandleMove;
            InputManager.Instance.OnJump += HandleJump;
            InputManager.Instance.OnDash += HandleDash;
        }

        private void Update()
        {
            _wasGrounded = _isGrounded;
            CheckGrounded();
            HandleLanding();
            TickTimers();
        }

        private void FixedUpdate()
        {
            if (!_isDashing)
                ApplyMovement();
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnMove -= HandleMove;
                InputManager.Instance.OnJump -= HandleJump;
                InputManager.Instance.OnDash -= HandleDash;
            }
            _dashTween?.Kill();
        }

        // ══════════════════════════════════════════════════════
        // 입력 핸들러
        // ══════════════════════════════════════════════════════

        private void HandleMove(float value) => _moveInput = value;

        private void HandleJump()
        {
            _jumpBufferTimer = _settings.JumpBufferTime;
            if (CanJump()) ExecuteJump();
        }

        private void HandleDash()
        {
            if (_dashCooldownTimer > 0f || _isDashing) return;
            Dash();
        }

        // ══════════════════════════════════════════════════════
        // 착지 / 이탈
        // ══════════════════════════════════════════════════════

        private void HandleLanding()
        {
            bool justLanded = !_wasGrounded && _isGrounded;
            bool justLeftGround = _wasGrounded && !_isGrounded;

            if (justLanded)
            {
                _remainingJumps = _settings.MaxJumpCount;
                _coyoteTimer = 0f;

                if (_jumpBufferTimer > 0f)
                    ExecuteJump();
            }
            else if (justLeftGround && _rigid2D.linearVelocity.y <= 0f)
            {
                _coyoteTimer = _settings.CoyoteTime;
                _remainingJumps = _settings.MaxJumpCount - 1;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이동
        // ══════════════════════════════════════════════════════

        private void ApplyMovement()
        {
            _rigid2D.linearVelocity = new Vector2(
                _moveInput * _settings.MoveSpeed,
                _rigid2D.linearVelocity.y);

            FlipSprite();
        }

        // ══════════════════════════════════════════════════════
        // 점프
        // ══════════════════════════════════════════════════════

        private bool CanJump()
            => _isGrounded || _coyoteTimer > 0f || _remainingJumps > 0;

        /// <summary>
        /// 점프 실행.
        /// isDouble = 공중 + 코요테 만료 → 2단 점프.
        /// 이벤트 발행: 1단 → OnJumped / 2단 → OnDoubleJumped.
        /// </summary>
        private void ExecuteJump()
        {
            bool isDouble = !_isGrounded && _coyoteTimer <= 0f;
            float force = isDouble
                ? _settings.JumpForce * _settings.DoubleJumpMultiplier
                : _settings.JumpForce;

            _rigid2D.linearVelocity = new Vector2(_rigid2D.linearVelocity.x, force);

            if (!isDouble) _remainingJumps = _settings.MaxJumpCount - 1;
            else _remainingJumps--;

            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;

            // ★ 이벤트 발행 — MovementAnimator 가 구독
            if (isDouble) OnDoubleJumped?.Invoke();
            else OnJumped?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 대쉬
        // ══════════════════════════════════════════════════════

        private void Dash()
        {
            _isDashing = true;
            _dashCooldownTimer = Mathf.Max(0.3f, _settings.DashCooldown);
            _rigid2D.gravityScale = _settings.DashGravityScale;
            _rigid2D.linearVelocity = Vector2.zero;

            if (_trailRenderer != null) _trailRenderer.emitting = true;

            OnDashStarted?.Invoke();

            _dashTween?.Kill();
            Vector3 dashDir = new Vector3(_facingDirection, 0f, 0f);
            Vector3 targetPos = transform.position + dashDir * _settings.DashDistance;

            _dashTween = transform.DOMove(targetPos, _settings.DashDuration)
                .SetEase(Ease.OutQuart)
                .OnUpdate(() =>
                {
                    Vector2 origin = (Vector2)transform.position
                                        + new Vector2(_facingDirection * _settings.DashBodyWidth, 0f);
                    float remaining = Vector2.Distance(transform.position, targetPos);

                    RaycastHit2D hit = Physics2D.Raycast(
                        origin,
                        new Vector2(_facingDirection, 0f),
                        remaining,
                        _settings.DashWallLayer);

                    if (hit.collider != null)
                    {
                        float safeX = hit.point.x - _facingDirection * _settings.DashBodyWidth;
                        transform.position = new Vector3(safeX, transform.position.y, transform.position.z);
                        _dashTween?.Kill();
                        EndDash();
                    }

                    _rigid2D.position = transform.position;
                })
                .OnComplete(EndDash);
        }

        private void EndDash()
        {
            if (!_isDashing) return;
            _isDashing = false;
            _rigid2D.gravityScale = _settings.GravityScale;

            if (_trailRenderer != null) _trailRenderer.emitting = false;
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        private void CheckGrounded()
        {
            if (_groundCheck == null) { _isGrounded = false; return; }
            _isGrounded = Physics2D.OverlapCircle(
                _groundCheck.position,
                _settings.GroundCheckRadius,
                _settings.GroundLayer);
        }

        private void FlipSprite()
        {
            if (_moveInput == 0f) return;

            float newDir = _moveInput > 0f ? 1f : -1f;
            if (Mathf.Approximately(newDir, _facingDirection)) return;

            _facingDirection = newDir;
            _spriteRenderer.flipX = newDir < 0f;
            OnFlipped?.Invoke(_facingDirection);
        }

        /// <summary>
        /// 외부에서 방향을 강제로 설정한다.
        /// PlayerChargeAttack 이 차징 중 좌우 방향키 입력 시 호출.
        /// FlipSprite 와 동일한 처리 (스프라이트 반전 + OnFlipped 발행).
        /// OnFlipped 구독자(PlayerWeaponMover / PlayerWeaponHitboxManager)
        /// 가 자동으로 Weapon 위치 및 히트박스를 동기화함.
        /// </summary>
        /// <param name="direction">1 = 오른쪽, -1 = 왼쪽</param>
        public void ForceFlip(float direction)
        {
            float newDir = direction >= 0f ? 1f : -1f;
            if (Mathf.Approximately(newDir, _facingDirection)) return;

            _facingDirection = newDir;
            _spriteRenderer.flipX = newDir < 0f;
            OnFlipped?.Invoke(_facingDirection);
        }

        /// <summary>
        /// 이동 입력을 즉시 0으로 초기화.
        /// PlayerChargeAttack 이 차징 시작 시 호출.
        /// </summary>
        public void StopMovement()
        {
            _moveInput = 0f;
            if (_rigid2D != null)
                _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        private void TickTimers()
        {
            if (_coyoteTimer > 0f) _coyoteTimer -= Time.deltaTime;
            if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.deltaTime;
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_groundCheck == null || _settings == null) return;

            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheck.position, _settings.GroundCheckRadius);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                _groundCheck.position + Vector3.up * 3f,
                $"Grounded: {_isGrounded}\n" +
                $"Jumps left: {_remainingJumps}/{_settings.MaxJumpCount}\n" +
                $"VelocityY: {VelocityY:F2}\n" +
                $"Coyote: {_coyoteTimer:F2}s");
#endif
        }
    }
}