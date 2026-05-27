// ============================================================
// PlayerMover.cs  v1.5
// 플레이어 이동 / 점프 / 대쉬 물리 컴포넌트
//
// [v1.5 변경]
//   ① 대쉬 transform.DOMove → Rigidbody2D.MovePosition 코루틴 교체
//       - 물리 레이어를 통한 이동이므로 얇은 벽 관통 방지
//       - _dashTween 필드 제거, _dashCoroutine 코루틴으로 대체
//       - 매 FixedUpdate 단위로 CastCollider → 벽 감지 후 즉시 중단
//   ② OnFlipped 이벤트 추가
//       - FlipSprite() 에서 방향이 실제로 바뀔 때만 발행
//       - PlayerWeaponMover 가 구독하여 Weapon localPosition X 반전
//
// [MovementAnimator 가 구독하는 이벤트]
//   OnJumped       : 1단 점프 실행 순간 → Jump Trigger
//   OnDoubleJumped : 2단 점프 실행 순간 → DoubleJump Trigger
//   OnDashStarted  : 대쉬 시작 순간    → Dash Trigger
//
// [PlayerWeaponMover 가 구독하는 이벤트]
//   OnFlipped      : 좌우 반전 순간 → Weapon 로컬 X 반전
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 이동 / 점프 / 대쉬 물리 전담 컴포넌트. (v1.5)
    ///
    /// ────────────────────────────────────────────────────
    /// [대쉬 물리 처리 방식 — v1.5]
    ///   Rigidbody2D.MovePosition 을 FixedUpdate 단위로 호출.
    ///   매 스텝마다 CastCollider 로 전방 벽 감지.
    ///   벽 감지 즉시 대쉬 중단 → 얇은 벽 관통 방지.
    ///
    /// [Weapon 좌우 동기화 — v1.5]
    ///   FlipSprite() 에서 방향이 바뀔 때 OnFlipped 이벤트 발행.
    ///   PlayerWeaponMover 가 이를 구독하여 Weapon localPosition X 반전.
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
        // 컴포넌트 참조 (자동 취득)
        // ──────────────────────────────────────────

        private Rigidbody2D _rigid2D;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider2D;

        // ──────────────────────────────────────────
        // 내부 상태 — 이동
        // ──────────────────────────────────────────

        /// <summary> 수평 이동 입력값. InputManager.OnMove 수신 시 갱신. </summary>
        private float _moveInput;

        /// <summary> 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽. </summary>
        private float _facingDirection = 1f;

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

        /// <summary> 대쉬 진행 중 여부. </summary>
        private bool _isDashing;

        /// <summary> 대쉬 쿨타임 잔여 시간. </summary>
        private float _dashCooldownTimer;

        /// <summary>
        /// 진행 중인 대쉬 코루틴.
        /// 벽 충돌 감지 시 StopCoroutine 으로 즉시 중단.
        /// </summary>
        private Coroutine _dashCoroutine;

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

        /// <summary>
        /// 현재 수직 속도.
        /// MovementAnimator 가 Fall 전환 조건으로 사용. 음수 = 하강 중.
        /// </summary>
        public float VelocityY => _rigid2D != null ? _rigid2D.linearVelocity.y : 0f;

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
            _collider2D = GetComponent<Collider2D>();

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
        /// </summary>
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

        /// <summary>
        /// InputManager 이벤트 구독 해제.
        /// </summary>
        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnMove -= HandleMove;
                InputManager.Instance.OnJump -= HandleJump;
                InputManager.Instance.OnDash -= HandleDash;
            }

            if (_dashCoroutine != null)
                StopCoroutine(_dashCoroutine);
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
            _dashCoroutine = StartCoroutine(DashRoutine());
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

        /// <summary>
        /// FixedUpdate 에서 수평 이동 velocity 적용 + 스프라이트 반전.
        /// 대쉬 중에는 호출되지 않음.
        /// </summary>
        private void ApplyMovement()
        {
            _rigid2D.linearVelocity = new Vector2(
                _moveInput * _settings.MoveSpeed,
                _rigid2D.linearVelocity.y);

            FlipSprite();
        }

        // ══════════════════════════════════════════════════════
        // 스프라이트 반전
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 이동 입력 방향에 따라 스프라이트를 반전.
        /// 방향이 실제로 바뀔 때만 OnFlipped 이벤트를 발행.
        ///
        /// [OnFlipped 구독자]
        ///   PlayerWeaponMover — Weapon localPosition X 부호 반전
        /// </summary>
        private void FlipSprite()
        {
            if (_moveInput == 0f) return;

            float newDir = _moveInput > 0f ? 1f : -1f;

            // 방향이 실제로 바뀐 경우에만 처리
            if (Mathf.Approximately(newDir, _facingDirection)) return;

            _facingDirection = newDir;
            _spriteRenderer.flipX = newDir < 0f;

            // ★ 구독자에게 새 방향 전달
            OnFlipped?.Invoke(_facingDirection);
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

            if (isDouble) OnDoubleJumped?.Invoke();
            else OnJumped?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 대쉬 — Rigidbody2D.MovePosition 코루틴
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 대쉬 코루틴.
        ///
        /// [transform.DOMove 를 사용하지 않는 이유]
        ///   DOMove 는 Transform 좌표를 직접 덮어쓰므로
        ///   물리 엔진(Rigidbody2D)이 충돌 계산에 관여하지 못함.
        ///   얇은 벽에서 Raycast 체크 타이밍이 맞지 않으면 관통이 발생.
        ///
        /// [MovePosition 방식]
        ///   Rigidbody2D.MovePosition 은 물리 레이어를 통한 이동이므로
        ///   Unity 물리 엔진이 Collider 간 충돌을 처리.
        ///   Continuous Collision Detection(CCD) 과 함께 작동하면
        ///   얇은 벽 관통이 방지됨.
        ///
        ///   추가로 매 FixedUpdate 단위에서 CastCollider 로 전방을
        ///   직접 체크하여 충돌 감지 즉시 대쉬 중단.
        ///
        /// [주의]
        ///   Rigidbody2D Collision Detection = Continuous 권장.
        ///   (Project Settings → Physics 2D → Default Contact Offset 확인)
        /// </summary>
        private IEnumerator DashRoutine()
        {
            // ① 대쉬 시작 설정
            _isDashing = true;
            _dashCooldownTimer = Mathf.Max(0.3f, _settings.DashCooldown);
            _rigid2D.gravityScale = _settings.DashGravityScale;
            _rigid2D.linearVelocity = Vector2.zero;

            if (_trailRenderer != null) _trailRenderer.emitting = true;

            OnDashStarted?.Invoke();

            // ② 대쉬 방향 / 스텝 계산
            Vector2 dashDir = new Vector2(_facingDirection, 0f);
            float totalDist = _settings.DashDistance;
            float elapsed = 0f;
            float duration = _settings.DashDuration;
            Vector2 startPos = _rigid2D.position;
            Vector2 targetPos = startPos + dashDir * totalDist;

            // ③ FixedUpdate 단위 이동 루프
            while (elapsed < duration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                // OutQuart 에이징 (DOTween 대체)
                float eased = 1f - Mathf.Pow(1f - t, 4f);
                Vector2 nextPos = Vector2.Lerp(startPos, targetPos, eased);
                Vector2 delta = nextPos - _rigid2D.position;

                // ── 전방 벽 감지 (CastCollider) ──────────────────
                if (_collider2D != null && delta.sqrMagnitude > 0f)
                {
                    int hitCount = _collider2D.Cast(
                        dashDir,
                        new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = _settings.DashWallLayer },
                        _castResults,
                        delta.magnitude + _settings.DashBodyWidth);

                    if (hitCount > 0)
                    {
                        // 벽 바로 앞에서 정지
                        float safe = _castResults[0].distance - _settings.DashBodyWidth;
                        if (safe > 0f)
                            _rigid2D.MovePosition(_rigid2D.position + dashDir * safe);

                        break; // 대쉬 즉시 중단
                    }
                }

                _rigid2D.MovePosition(nextPos);
            }

            EndDash();
        }

        /// <summary>
        /// CastCollider 결과 버퍼. GC 방지를 위해 필드로 선언.
        /// </summary>
        private readonly RaycastHit2D[] _castResults = new RaycastHit2D[4];

        /// <summary>
        /// 대쉬 종료 처리.
        /// DashRoutine 완료 or 벽 충돌 중단 시 모두 호출.
        /// </summary>
        private void EndDash()
        {
            if (!_isDashing) return;

            _isDashing = false;
            _rigid2D.gravityScale = _settings.GravityScale;
            _rigid2D.linearVelocity = new Vector2(
                _moveInput * _settings.MoveSpeed,
                _rigid2D.linearVelocity.y);

            if (_trailRenderer != null) _trailRenderer.emitting = false;

            _dashCoroutine = null;
        }

        // ══════════════════════════════════════════════════════
        // 지면 감지
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// GroundCheck 기준점에서 OverlapCircle 로 지면 감지.
        /// </summary>
        private void CheckGrounded()
        {
            if (_groundCheck == null) return;

            _isGrounded = Physics2D.OverlapCircle(
                _groundCheck.position,
                _settings.GroundCheckRadius,
                _settings.GroundLayer);
        }

        // ══════════════════════════════════════════════════════
        // 타이머
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 코요테 타임 / 점프 버퍼 / 대쉬 쿨타임 매 프레임 감산.
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
            if (_groundCheck == null) return;

            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(
                _groundCheck.position,
                _settings != null ? _settings.GroundCheckRadius : 0.1f);
        }
    }
}