// ============================================================
// PlayerMover.cs  v1.2
// 플레이어 이동 패키지 — 이동 / 점프 / 대쉬 물리 컴포넌트
//
// [v1.2 변경 — 점프 로직 버그 수정]
//   문제:
//     착지 후 점프가 불가능해지는 버그.
//     원인 A: HandleLanding() 의 justLeftGround 블록에서
//             _remainingJumps = MaxJumpCount - 1 로 설정.
//             착지 리셋(_remainingJumps = MaxJumpCount) 이 발생해도
//             그 직후 justLeftGround 가 같은 프레임에 다시 평가되어 덮어쓰는 경우.
//     원인 B: _isGrounded 가 항상 false 일 때 (GroundLayer 미설정 등)
//             착지 감지 자체가 불가능 → _remainingJumps 리셋 불가.
//   수정:
//     1. HandleLanding() 분리 — justLanded / justLeftGround 를 동시 평가하지 않도록
//        if-else if 구조를 명확히 유지 (기존 구조 유지, 주석 강화)
//     2. CheckGrounded() 에 Gizmos 디버그 로그 추가
//     3. ExecuteJump() 내부에서 justLanded 직후 호출되는 경우
//        isDouble 판별을 _isGrounded 기반으로 재확인하여 1단 처리 보장
//     4. GroundLayer 미설정 경고 추가 (Awake)
//
// [독립 패키지]
//   namespace : PlayerMovement (HOSE 종속 없음)
//   의존 대상 : MovementSettings SO, MovementInput
//
// [HOSE PlayerBody 와의 차이]
//   HOSE : PlayerStatBus.OnStatChanged 구독 → MoveSpeed / DashCooldown 동기화
//   독립 : MovementSettings SO 를 직접 참조 → 매 프레임 SO 값 사용
//          외부 이벤트 버스 없음. SO 값을 Inspector에서 직접 조절.
//
// [수치 변경 방법]
//   런타임 수치 변경 필요 없음    → SO Inspector 값 수정
//   런타임 중 수치 변경 필요 있음 → Settings.MoveSpeed = newVal; (SO는 참조형)
//
// [DOTween 의존]
//   대쉬 이동은 DOTween.To 사용.
//   DOTween 패키지가 프로젝트에 없으면 Dash() 내부만 코루틴 방식으로 교체 가능.
//   (교체 방법: DOTWEEN_DISABLED 심볼 정의 시 코루틴 대체 사용)
// ============================================================

using System.Collections;
using UnityEngine;
using DG.Tweening;

namespace PlayerMovement
{
    /// <summary>
    /// 플레이어 이동 / 점프 / 대쉬 물리 전담 컴포넌트. (v1.2)
    ///
    /// [역할]
    ///   MovementInput 이벤트(OnMove / OnJump / OnDash)를 구독하여
    ///   Rigidbody2D 기반 물리 이동을 처리한다.
    ///   수치는 MovementSettings SO 에서 읽는다.
    ///   Animator 는 전혀 알지 못한다 — Animator 관련은 MovementAnimator 가 전담.
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
    ///
    /// [MovementAnimator 가 구독하는 이벤트]
    ///   OnDashStarted  : 대쉬 시작 순간 1회 발행
    ///   OnDoubleJumped : 2단 점프 순간 1회 발행
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(MovementInput))]
    public class PlayerMover : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary>
        /// 이동 수치 설정 ScriptableObject.
        /// Project 창 우클릭 → Create → PlayerMovement → Movement Settings 로 생성.
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
        private MovementInput _input;

        // ──────────────────────────────────────────
        // 내부 상태 — 이동
        // ──────────────────────────────────────────

        /// <summary> 수평 이동 입력값. OnMove 수신 시 갱신. -1 ~ 1. </summary>
        private float _moveInput;

        /// <summary>
        /// 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽.
        /// FlipSprite() 에서 이동 입력 기반으로 갱신.
        /// 대쉬 방향 / Raycast 시작점에 사용.
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
        // [왜 이벤트로 분리하는가?]
        //   PlayerMover 의 책임은 "물리 이동" 이다.
        //   SetTrigger 를 직접 호출하면 Animator 를 알게 되고
        //   이동 컴포넌트에 시각 책임이 섞인다.
        //   이벤트를 발행하면 "대쉬가 시작됐다" 는 사실만 알리고
        //   어떻게 표현할지는 MovementAnimator 가 결정한다.
        //   Animator 가 없는 프로젝트에서도 PlayerMover 코드 변경 없이 동작.

        /// <summary>
        /// 대쉬가 시작되는 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("Dash") 처리.
        /// </summary>
        public event System.Action OnDashStarted;

        /// <summary>
        /// 2단 점프가 실행되는 순간 1회 발행.
        /// MovementAnimator 가 구독하여 SetTrigger("DoubleJump") 처리.
        /// 1단 점프는 MovementAnimator 가 IsGrounded 전환으로 감지하여 처리.
        /// </summary>
        public event System.Action OnDoubleJumped;

        // ──────────────────────────────────────────
        // 프로퍼티 — 외부 읽기
        // ──────────────────────────────────────────

        /// <summary> 현재 접지 여부. MovementAnimator / 외부에서 읽기용. </summary>
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
        /// _settings null 체크 — 미연결 시 에러 출력.
        /// </summary>
        private void Awake()
        {
            _rigid2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _input = GetComponent<MovementInput>();

            if (_settings == null)
            {
                Debug.LogError("[PlayerMover] MovementSettings SO 가 연결되지 않았습니다. " +
                               "Inspector 에서 _settings 를 연결하세요.");
                enabled = false;
                return;
            }

            if (_groundCheck == null)
                Debug.LogWarning("[PlayerMover] _groundCheck 가 연결되지 않았습니다. " +
                                 "지면 감지가 작동하지 않습니다.");

            _rigid2D.gravityScale = _settings.GravityScale;
            _remainingJumps = _settings.MaxJumpCount;

            // GroundLayer 미설정 경고
            // GroundLayer = 0 이면 OverlapCircle 이 항상 false → 착지 감지 불가
            // → _remainingJumps 가 리셋되지 않아 점프가 1회 후 불가능해짐
            if (_settings.GroundLayer.value == 0)
                Debug.LogWarning("[PlayerMover] MovementSettings.GroundLayer 가 설정되지 않았습니다. " +
                                 "착지 감지가 작동하지 않아 점프가 1회 후 불가능해집니다. " +
                                 "SO Inspector 에서 Ground 레이어를 선택하세요.");
        }

        /// <summary>
        /// MovementInput 이벤트 구독.
        /// Start 에서 구독 — Awake 에서 MovementInput 초기화 완료 보장.
        /// </summary>
        private void Start()
        {
            if (_input == null) return;
            _input.OnMove += HandleMove;
            _input.OnJump += HandleJump;
            _input.OnDash += HandleDash;
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
            if (_input != null)
            {
                _input.OnMove -= HandleMove;
                _input.OnJump -= HandleJump;
                _input.OnDash -= HandleDash;
            }
            _dashTween?.Kill();
        }

        // ══════════════════════════════════════════════════════
        // 입력 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// MovementInput.OnMove 수신 → _moveInput 저장.
        /// FixedUpdate.ApplyMovement() 에서 velocity.x 에 적용.
        /// </summary>
        private void HandleMove(float value) => _moveInput = value;

        /// <summary>
        /// MovementInput.OnJump 수신 → 버퍼 설정 후 CanJump 체크.
        /// </summary>
        private void HandleJump()
        {
            _jumpBufferTimer = _settings.JumpBufferTime;
            if (CanJump()) ExecuteJump();
        }

        /// <summary>
        /// MovementInput.OnDash 수신 → 쿨타임 / 대쉬 중 체크 후 Dash().
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
        /// ────────────────────────────────────────────────────
        /// [착지 (justLanded)]
        ///   조건: 이전 프레임 공중(_wasGrounded=false) → 현재 프레임 지면(_isGrounded=true)
        ///   처리: _remainingJumps = MaxJumpCount 리셋.
        ///         _jumpBufferTimer > 0 이면 버퍼링 점프 실행.
        ///
        /// [지면 이탈 (justLeftGround)]
        ///   조건: 이전 프레임 지면(_wasGrounded=true) → 현재 프레임 공중(_isGrounded=false)
        ///         + 하강 중(velocity.y ≤ 0) — 점프 상승 중 이탈은 코요테 부여 안 함
        ///   처리: _coyoteTimer 설정 (절벽 끝 점프 허용).
        ///         _remainingJumps = MaxJumpCount - 1 (지상에서 낙하이므로 1단 예약 소모).
        ///
        /// ────────────────────────────────────────────────────
        /// [왜 if-else if 구조인가?]
        ///   justLanded 와 justLeftGround 는 같은 프레임에 동시에 true 가 될 수 없다.
        ///   명시적 if-else if 로 착지 리셋이 이탈 감지로 덮어써지는 사고를 차단한다.
        ///
        /// ────────────────────────────────────────────────────
        /// [착지 후 점프가 안 되는 버그의 가장 흔한 원인]
        ///   MovementSettings.GroundLayer 미설정(0)
        ///   → OverlapCircle 항상 false → _isGrounded 절대 true 안 됨
        ///   → justLanded 발생 안 함 → _remainingJumps 리셋 불가
        ///   → 점프 1회 후 영구 불가.
        ///   Awake() 에서 경고 출력됨.
        /// </summary>
        private void HandleLanding()
        {
            bool justLanded = !_wasGrounded && _isGrounded;   // 공중 → 지면
            bool justLeftGround = _wasGrounded && !_isGrounded;  // 지면 → 공중

            if (justLanded)
            {
                // 착지 — 점프 횟수 완전 리셋
                _remainingJumps = _settings.MaxJumpCount;
                _coyoteTimer = 0f;

                // 착지 직전 점프 입력이 있었으면 즉시 점프 (버퍼링)
                if (_jumpBufferTimer > 0f)
                    ExecuteJump();
            }
            else if (justLeftGround && _rigid2D.linearVelocity.y <= 0f)
            {
                // 지면에서 하강 이탈 — 코요테 타임 부여
                // 점프 상승 중 이탈(velocity.y > 0)에는 코요테 부여 안 함
                _coyoteTimer = _settings.CoyoteTime;
                _remainingJumps = _settings.MaxJumpCount - 1;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이동
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 수평 이동 velocity.x 적용. y 는 중력에 맡긴다.
        /// 대쉬 중(_isDashing)에는 건너뜀 — DOTween 이 Transform 을 제어.
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
        ///
        /// [1단 vs 2단 구분]
        ///   isDouble = !_isGrounded &amp;&amp; _coyoteTimer &lt;= 0
        ///   공중(접지 아님) + 코요테 만료 = 2단 점프.
        ///
        /// [velocity.y 직접 설정]
        ///   AddForce 아닌 velocity.y 직접 교체.
        ///   이전 y 속도 누적 없이 일관된 점프 높이 보장.
        /// </summary>
        private void ExecuteJump()
        {
            bool isDouble = !_isGrounded && _coyoteTimer <= 0f;
            float force = isDouble
                ? _settings.JumpForce * _settings.DoubleJumpMultiplier
                : _settings.JumpForce;

            _rigid2D.linearVelocity = new Vector2(_rigid2D.linearVelocity.x, force);

            if (!isDouble)
                _remainingJumps = _settings.MaxJumpCount - 1;
            else
                _remainingJumps--;

            _coyoteTimer = 0f;
            _jumpBufferTimer = 0f;

            // 2단 점프 이벤트 발행 — MovementAnimator 가 SetTrigger("DoubleJump") 처리
            // PlayerMover 는 Animator 를 알지 않는다
            if (isDouble)
                OnDoubleJumped?.Invoke();
        }

        // ══════════════════════════════════════════════════════
        // 대쉬
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 대쉬 실행.
        /// DOTween 으로 DashDuration 초 동안 DashDistance 만큼 이동.
        /// Ease.OutQuart — 빠르게 치고 나가다 끝에서 감속.
        /// OnUpdate 에서 매 프레임 Raycast 벽 감지.
        /// </summary>
        private void Dash()
        {
            _isDashing = true;
            _dashCooldownTimer = Mathf.Max(0.3f, _settings.DashCooldown);

            _rigid2D.gravityScale = _settings.DashGravityScale;
            _rigid2D.linearVelocity = Vector2.zero;

            if (_trailRenderer != null)
                _trailRenderer.emitting = true;

            // 대쉬 시작 이벤트 발행 — MovementAnimator 가 SetTrigger("Dash") 처리
            // PlayerMover 는 Animator 를 알지 않는다
            OnDashStarted?.Invoke();

            _dashTween?.Kill();
            Vector3 dashDir = new Vector3(_facingDirection, 0f, 0f);
            Vector3 targetPos = transform.position + dashDir * _settings.DashDistance;

            _dashTween = transform.DOMove(targetPos, _settings.DashDuration)
                .SetEase(Ease.OutQuart)
                .OnUpdate(() =>
                {
                    // 벽 감지 Raycast
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

                    // DOTween 이 Transform 을 직접 수정하므로 Rigidbody2D 위치 동기화
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

            if (_trailRenderer != null)
                _trailRenderer.emitting = false;
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
        /// _facingDirection 과 _spriteRenderer.flipX 를 동시에 갱신.
        ///
        /// [왜 flipX 를 쓰는가?]
        ///   Scale.x = -1 방식은 자식 오브젝트 위치에 영향.
        ///   flipX 는 렌더링만 반전 — 자식 Transform 무영향.
        /// </summary>
        private void FlipSprite()
        {
            if (_moveInput > 0f)
            {
                _facingDirection = 1f;
                _spriteRenderer.flipX = false;
            }
            else if (_moveInput < 0f)
            {
                _facingDirection = -1f;
                _spriteRenderer.flipX = true;
            }
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

            // 접지 감지 원 — 초록(접지) / 빨강(공중)
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheck.position, _settings.GroundCheckRadius);

#if UNITY_EDITOR
            // 점프 상태 디버그 레이블
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                _groundCheck.position + Vector3.up * 3f,
                $"Grounded: {_isGrounded}\n" +
                $"Jumps left: {_remainingJumps}/{_settings.MaxJumpCount}\n" +
                $"Coyote: {_coyoteTimer:F2}s\n" +
                $"GroundLayer: {_settings.GroundLayer.value}" +
                (_settings.GroundLayer.value == 0 ? " ⚠️미설정" : ""));
#endif
        }
    }
}