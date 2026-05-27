// ============================================================
// PlayerMover.cs  v1.3
// 플레이어 이동 패키지 — 이동 / 점프 / 대쉬 물리 컴포넌트
//
// [v1.3 변경 — InputManager 통합]
//   MovementInput 참조를 InputManager 로 교체.
//   이벤트 구독 대상: InputManager.OnMove / OnJump / OnDash
//   그 외 물리 로직 변경 없음.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 플레이어 이동 / 점프 / 대쉬 물리 전담 컴포넌트. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [이 파일이 하는 것]
    ///   - InputManager 이벤트(OnMove / OnJump / OnDash)를 구독
    ///   - Rigidbody2D 기반 물리 이동 처리
    ///   - 수치는 MovementSettings SO 에서 읽음
    ///   - Animator 는 전혀 알지 못함 (MovementAnimator 담당)
    ///
    /// [Inspector 필수 연결]
    ///   _settings    : MovementSettings SO
    ///   _groundCheck : 발 아래 빈 오브젝트 Transform
    ///   _trailRenderer : 대쉬 잔상 (선택)
    ///
    /// [외부에서 읽을 수 있는 상태]
    ///   IsGrounded      : 현재 접지 여부
    ///   IsDashing       : 현재 대쉬 중 여부
    ///   FacingDirection : 바라보는 방향 (1 = 오른쪽, -1 = 왼쪽)
    ///   MoveInput       : 현재 수평 입력값
    ///
    /// [MovementAnimator 가 구독하는 이벤트]
    ///   OnDashStarted  : 대쉬 시작 순간 1회 발행
    ///   OnDoubleJumped : 2단 점프 순간 1회 발행
    /// ────────────────────────────────────────────────────
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary>
        /// 이동 수치 설정 ScriptableObject.
        /// Project 창 우클릭 → Create → KEY → Movement Settings 로 생성.
        /// </summary>
        [Tooltip("MovementSettings SO. 필수 연결.")]
        [SerializeField] private MovementSettings _settings;

        /// <summary>
        /// 지면 감지 기준점 Transform.
        /// 플레이어 발 아래에 위치한 빈 오브젝트를 연결.
        /// </summary>
        [Tooltip("발 아래 지면 감지 기준점. 빈 오브젝트 연결.")]
        [SerializeField] private Transform _groundCheck;

        [Header("── 선택 연결 ──────────────────────")]

        /// <summary>
        /// 대쉬 잔상 TrailRenderer.
        /// 미연결 시 잔상 없이 대쉬만 동작.
        /// </summary>
        [Tooltip("대쉬 잔상 TrailRenderer. 미연결 시 잔상 없음.")]
        [SerializeField] private TrailRenderer _trailRenderer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조 (자동 취득)
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 내부 상태 — 이동
        // ──────────────────────────────────────────

        /// <summary> 수평 이동 입력값. OnMove 수신 시 갱신. -1 ~ 1. </summary>
        private float _moveInput;

        /// <summary>
        /// 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽.
        /// FlipSprite() 에서 이동 입력 기반으로 갱신.
        /// </summary>
        private float _facingDirection = 1f;

        /// <summary> 현재 대쉬 Tween. 벽 감지 시 Kill() 로 중단. </summary>
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
        // 이벤트 — MovementAnimator 가 구독
        // ──────────────────────────────────────────

        /// <summary>
        /// 대쉬가 시작되는 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("Dash") 처리.
        /// </summary>
        public event System.Action OnDashStarted;

        /// <summary>
        /// 2단 점프가 실행되는 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("DoubleJump") 처리.
        /// </summary>
        public event System.Action OnDoubleJumped;

        // ──────────────────────────────────────────
        // 프로퍼티 — 외부 읽기
        // ──────────────────────────────────────────

        /// <summary> 현재 접지 여부. </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary> 현재 대쉬 중 여부. </summary>
        public bool IsDashing => _isDashing;

        /// <summary> 현재 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽. </summary>
        public float FacingDirection => _facingDirection;

        /// <summary> 현재 이동 입력값. MovementAnimator.Speed 계산에 사용. </summary>
        public float MoveInput => _moveInput;

        /// <summary> 연결된 Settings SO. 외부에서 수치 읽기용. </summary>
        public MovementSettings Settings => _settings;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 컴포넌트 취득 + 초기 설정.
        /// </summary>
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

        /// <summary>
        /// InputManager 이벤트 구독.
        /// Start 에서 구독 — Awake 에서 InputManager 싱글턴 초기화 완료 보장.
        /// </summary>
        private void Start()
        {
            if (InputManager.Instance == null)
            {
                Debug.LogError("[PlayerMover] InputManager 가 없습니다. " +
                               "Player 오브젝트에 InputManager 컴포넌트를 추가하세요.");
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

        /// <summary>
        /// InputManager.OnMove 수신 → _moveInput 저장.
        /// </summary>
        private void HandleMove(float value) => _moveInput = value;

        /// <summary>
        /// InputManager.OnJump 수신 → 버퍼 설정 후 CanJump 체크.
        /// </summary>
        private void HandleJump()
        {
            _jumpBufferTimer = _settings.JumpBufferTime;
            if (CanJump()) ExecuteJump();
        }

        /// <summary>
        /// InputManager.OnDash 수신 → 쿨타임 / 대쉬 중 체크 후 Dash().
        /// </summary>
        private void HandleDash()
        {
            if (_dashCooldownTimer > 0f || _isDashing) return;
            Dash();
        }

        // ══════════════════════════════════════════════════════
        // 착지 / 이탈 감지
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 착지/이탈 전환을 감지하고 점프 횟수/코요테 타이머를 처리.
        ///
        /// [착지 (justLanded)]
        ///   조건: 이전 프레임 공중 → 현재 프레임 지면
        ///   처리: _remainingJumps 리셋, 버퍼링 점프 실행.
        ///
        /// [지면 이탈 (justLeftGround)]
        ///   조건: 이전 프레임 지면 → 현재 프레임 공중 + 하강 중
        ///   처리: 코요테 타이머 설정, _remainingJumps = MaxJumpCount - 1.
        /// </summary>
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

        /// <summary>
        /// 수평 이동 velocity.x 적용. y 는 중력에 맡긴다.
        /// 대쉬 중(_isDashing)에는 건너뜀.
        /// </summary>
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

        /// <summary>
        /// 점프 가능 여부. 접지 / 코요테 타임 / 남은 횟수 중 하나라도 충족 시 true.
        /// </summary>
        private bool CanJump()
            => _isGrounded || _coyoteTimer > 0f || _remainingJumps > 0;

        /// <summary>
        /// 점프 실행.
        /// isDouble = 공중(접지 아님) + 코요테 만료 = 2단 점프.
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

            if (isDouble) OnDoubleJumped?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 대쉬
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 대쉬 실행. DOTween 으로 DashDuration 초 동안 이동.
        /// OnUpdate 에서 Raycast 벽 감지.
        /// </summary>
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

        /// <summary>
        /// 대쉬 종료. OnComplete / 벽 감지 양쪽에서 호출.
        /// !_isDashing 체크로 중복 호출 방지.
        /// </summary>
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

        /// <summary>
        /// OverlapCircle 로 지면 접촉 여부 판정.
        /// _groundCheck 미연결 시 false 고정.
        /// </summary>
        private void CheckGrounded()
        {
            if (_groundCheck == null) { _isGrounded = false; return; }
            _isGrounded = Physics2D.OverlapCircle(
                _groundCheck.position,
                _settings.GroundCheckRadius,
                _settings.GroundLayer);
        }

        /// <summary>
        /// 이동 방향에 따라 스프라이트를 좌우 반전.
        /// flipX 방식 — 자식 Transform 무영향.
        /// </summary>
        private void FlipSprite()
        {
            if (_moveInput > 0f) { _facingDirection = 1f; _spriteRenderer.flipX = false; }
            else if (_moveInput < 0f) { _facingDirection = -1f; _spriteRenderer.flipX = true; }
        }

        /// <summary>
        /// 코요테 / 버퍼 / 대쉬 쿨타임 타이머를 매 프레임 감소.
        /// </summary>
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
                $"Coyote: {_coyoteTimer:F2}s");
#endif
        }
    }
}