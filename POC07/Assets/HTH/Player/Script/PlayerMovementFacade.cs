// ============================================================
// PlayerMovementFacade.cs  v1.0
// 플레이어 이동 패키지 — 외부 단일 진입점
//
// [독립 패키지]
//   namespace : PlayerMovement (HOSE 종속 없음)
//   의존 대상 : PlayerMover, MovementInput, MovementAnimator
//
// [역할]
//   외부 코드가 이동 패키지에 접근하는 유일한 창구.
//   PlayerMovementFacade.Instance 하나로 모든 이동 상태 조회 가능.
//
// [싱글턴 주의]
//   Player 는 씬마다 새로 생성되는 오브젝트.
//   DontDestroyOnLoad 사용 금지.
//   씬 전환 시 파괴 → OnDestroy 에서 Instance = null 초기화.
//   다음 씬에서 Awake() 에 의해 재설정.
//
//   중복 Instance 발생 시:
//   Destroy(gameObject) ❌ — Player 오브젝트 전체 삭제
//   Destroy(this)       ✅ — 컴포넌트만 제거 (Player 오브젝트 유지)
// ============================================================

using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// 플레이어 이동 패키지 외부 단일 진입점. (v1.0)
    ///
    /// [사용법 — 외부 코드]
    ///   // 이동 중인지 확인
    ///   bool isMoving = PlayerMovementFacade.Instance.IsMoving;
    ///
    ///   // 접지 여부 확인
    ///   bool grounded = PlayerMovementFacade.Instance.IsGrounded;
    ///
    ///   // 점프 차단 (인벤토리 열릴 때)
    ///   PlayerMovementFacade.Instance.BlockJump();
    ///
    ///   // 분사 중 Animator 파라미터 전달 (전투 시스템에서 호출)
    ///   PlayerMovementFacade.Instance.SetFiring(true);
    ///
    /// [Facade 패턴]
    ///   외부는 PlayerMover / MovementInput / MovementAnimator 를 몰라도 된다.
    ///   이 클래스 하나로 필요한 모든 기능에 접근한다.
    /// </summary>
    public class PlayerMovementFacade : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 싱글턴
        // ──────────────────────────────────────────

        /// <summary>
        /// 전역 단일 인스턴스.
        /// 씬 전환 시 Player 가 파괴되면 null 로 초기화됨.
        /// </summary>
        public static PlayerMovementFacade Instance { get; private set; }

        // ──────────────────────────────────────────
        // 내부 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerMover _mover;
        private MovementInput _input;
        private MovementAnimator _anim;

        // ──────────────────────────────────────────
        // 프로퍼티 — 외부 읽기
        // ──────────────────────────────────────────

        /// <summary> 현재 접지 여부. </summary>
        public bool IsGrounded => _mover != null && _mover.IsGrounded;

        /// <summary> 현재 대쉬 중 여부. </summary>
        public bool IsDashing => _mover != null && _mover.IsDashing;

        /// <summary> 현재 이동 중 여부 (MoveInput != 0). </summary>
        public bool IsMoving => _mover != null && Mathf.Abs(_mover.MoveInput) > 0.05f;

        /// <summary>
        /// 현재 바라보는 방향. 1 = 오른쪽, -1 = 왼쪽.
        /// 스프라이트 반전, 투사체 방향 등에 사용.
        /// </summary>
        public float FacingDirection => _mover?.FacingDirection ?? 1f;

        /// <summary>
        /// 연결된 MovementSettings SO.
        /// 외부에서 수치를 읽거나 런타임에 수정할 때 사용.
        /// </summary>
        public MovementSettings Settings => _mover?.Settings;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // ── 싱글턴 보장 ──────────────────────
            // Destroy(gameObject) 가 아닌 Destroy(this) 로 컴포넌트만 제거.
            // Player 오브젝트 전체를 날리면 Hierarchy 에서 사라지는 버그 발생.
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // ── 내부 컴포넌트 취득 ──────────────────────
            _mover = GetComponent<PlayerMover>();
            _input = GetComponent<MovementInput>();
            _anim = GetComponent<MovementAnimator>();

            if (_mover == null) Debug.LogError("[PlayerMovementFacade] PlayerMover 없음. Player 오브젝트에 부착 필요.");
            if (_input == null) Debug.LogError("[PlayerMovementFacade] MovementInput 없음. Player 오브젝트에 부착 필요.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 점프 입력을 차단한다.
        ///
        /// [사용 예시]
        ///   인벤토리 UI 열릴 때:
        ///     PlayerMovementFacade.Instance.BlockJump();
        ///   인벤토리 UI 닫힐 때:
        ///     PlayerMovementFacade.Instance.UnblockJump();
        /// </summary>
        public void BlockJump() => _input?.BlockJump();

        /// <summary>
        /// 점프 차단을 해제한다.
        /// </summary>
        public void UnblockJump() => _input?.UnblockJump();

        /// <summary>
        /// 분사/공격 상태를 Animator IsFiring 파라미터에 전달한다.
        ///
        /// [사용 예시]
        ///   전투 시스템(PlayerCombat 등)에서:
        ///     PlayerMovementFacade.Instance.SetFiring(true);   // 발사 시작
        ///     PlayerMovementFacade.Instance.SetFiring(false);  // 발사 중단
        ///
        ///   이동 패키지 없이 전투만 쓰는 경우 호출하지 않아도 됨.
        /// </summary>
        public void SetFiring(bool isFiring) => _anim?.SetFiring(isFiring);
    }
}