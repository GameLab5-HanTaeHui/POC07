// ============================================================
// PlayerMovementFacade.cs  v1.1
// 플레이어 이동 패키지 — 외부 단일 진입점
//
// [v1.1 변경]
//   MovementInput → InputManager 참조 교체.
//   BlockJump / UnblockJump 를 InputManager.Instance 경유로 호출.
//   namespace : KEY 로 변경.
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 이동 패키지 외부 단일 진입점. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [사용법 — 외부 코드]
    ///   bool grounded = PlayerMovementFacade.Instance.IsGrounded;
    ///   bool dashing  = PlayerMovementFacade.Instance.IsDashing;
    ///   PlayerMovementFacade.Instance.SetFiring(true);
    ///
    /// [점프 차단은 InputManager 경유]
    ///   PlayerMovementFacade.Instance.BlockJump();
    ///   PlayerMovementFacade.Instance.UnblockJump();
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerMovementFacade : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 싱글턴
        // ──────────────────────────────────────────

        /// <summary>
        /// 전역 단일 인스턴스.
        /// 씬 전환 시 파괴되면 null 로 초기화.
        /// </summary>
        public static PlayerMovementFacade Instance { get; private set; }

        // ──────────────────────────────────────────
        // 내부 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerMover _mover;
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
        /// 무기 공격 방향, 투사체 방향 등에 사용.
        /// </summary>
        public float FacingDirection => _mover?.FacingDirection ?? 1f;

        /// <summary> 연결된 MovementSettings SO. </summary>
        public MovementSettings Settings => _mover?.Settings;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            _mover = GetComponent<PlayerMover>();
            _anim = GetComponent<MovementAnimator>();

            if (_mover == null)
                Debug.LogError("[PlayerMovementFacade] PlayerMover 없음.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 점프 입력을 차단한다. InputManager 경유.
        /// </summary>
        public void BlockJump() => InputManager.Instance?.BlockJump();

        /// <summary>
        /// 점프 차단을 해제한다. InputManager 경유.
        /// </summary>
        public void UnblockJump() => InputManager.Instance?.UnblockJump();

        /// <summary>
        /// 분사/공격 상태를 Animator IsFiring 파라미터에 전달한다.
        /// 전투 시스템(PlayerWeaponBase 등)에서 호출.
        /// </summary>
        public void SetFiring(bool isFiring) => _anim?.SetFiring(isFiring);
    }
}