// ============================================================
// MovementSettings.cs  v1.0
// 플레이어 이동 패키지 — 수치 설정 ScriptableObject
//
// [독립 패키지]
//   namespace : PlayerMovement (HOSE 종속 없음)
//   의존 대상 : 없음 (Unity 기본 API만 사용)
//
// [사용법]
//   Project 창 우클릭 → Create → PlayerMovement → Movement Settings
//   생성된 SO를 PlayerMovementFacade Inspector 의 _settings 에 연결.
//   SO 하나를 여러 캐릭터 Prefab이 공유하거나,
//   캐릭터마다 별도 SO를 만들어 수치를 다르게 설정 가능.
//
// [런타임 수치 변경]
//   외부에서 수치를 바꿔야 한다면 SO 값을 직접 수정하거나
//   PlayerMovementFacade.Settings 를 통해 접근.
//   변경 즉시 PlayerMover 가 반영 (매 프레임 SO 값을 참조).
// ============================================================

using UnityEngine;

namespace PlayerMovement
{
    /// <summary>
    /// 플레이어 이동 전체 수치를 보관하는 ScriptableObject. (v1.0)
    ///
    /// [왜 ScriptableObject인가?]
    ///   HOSE에서는 PlayerStatBus가 수치를 관리했다.
    ///   독립 패키지에서는 외부 시스템 없이 동작해야 하므로
    ///   ScriptableObject에 수치를 두고 Inspector에서 직접 조절한다.
    ///   SO는 에셋으로 저장되어 여러 씬/Prefab 에서 공유 가능하다.
    ///
    /// [생성 방법]
    ///   Project 창 우클릭 → Create → PlayerMovement → Movement Settings
    /// </summary>
    [CreateAssetMenu(
        fileName = "MovementSettings",
        menuName = "PlayerMovement/Movement Settings",
        order = 0)]
    public class MovementSettings : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 이동
        // ──────────────────────────────────────────

        [Header("── 이동 ──────────────────────")]

        /// <summary>
        /// 이동 속도 (units/s).
        /// Rigidbody2D.linearVelocity.x = moveInput * MoveSpeed.
        /// 최솟값 1.0 권장.
        /// </summary>
        [Tooltip("이동 속도 (units/s). 최솟값 1.0 권장.")]
        [Min(1f)]
        public float MoveSpeed = 5f;

        // ──────────────────────────────────────────
        // 점프
        // ──────────────────────────────────────────

        [Header("── 점프 ──────────────────────")]

        /// <summary>
        /// 점프 시 적용하는 수직 속도 (units/s).
        /// Rigidbody2D.linearVelocity.y 를 이 값으로 교체.
        /// </summary>
        [Tooltip("점프 수직 속도. 높을수록 높이 점프. 권장: 12~16.")]
        public float JumpForce = 14f;

        /// <summary>
        /// 최대 점프 횟수. 2 = 2단 점프.
        /// 착지 시 이 값으로 리셋.
        /// </summary>
        [Tooltip("최대 점프 횟수. 2 = 2단 점프.")]
        [Min(1)]
        public int MaxJumpCount = 2;

        /// <summary>
        /// 2단 점프(공중 점프) 힘 배율.
        /// JumpForce 에 이 값을 곱한다. 1.0 = 1단과 동일 높이.
        /// </summary>
        [Tooltip("2단 점프 힘 배율. 1.0 = 1단과 동일. 권장: 0.8~0.9.")]
        [Range(0.5f, 1.5f)]
        public float DoubleJumpMultiplier = 0.85f;

        /// <summary>
        /// 코요테 타임 (초).
        /// 지면에서 막 벗어난 후 이 시간 동안 점프를 허용.
        /// 플랫폼 끝에서 아슬하게 점프 가능하게 해주는 기법.
        /// </summary>
        [Tooltip("코요테 타임 (초). 지면 이탈 직후 점프 허용 시간. 권장: 0.08~0.12.")]
        [Range(0f, 0.3f)]
        public float CoyoteTime = 0.1f;

        /// <summary>
        /// 점프 버퍼링 시간 (초).
        /// 착지 직전에 점프를 눌러도 착지 순간 자동으로 점프.
        /// </summary>
        [Tooltip("점프 버퍼링 시간 (초). 착지 직전 입력 저장 시간. 권장: 0.1~0.2.")]
        [Range(0f, 0.3f)]
        public float JumpBufferTime = 0.15f;

        /// <summary>
        /// 기본 중력 스케일. Rigidbody2D.gravityScale 초기값.
        /// 대쉬 종료 시 이 값으로 복구.
        /// </summary>
        [Tooltip("기본 중력 스케일. 권장: 2.5~3.5.")]
        public float GravityScale = 3f;

        // ──────────────────────────────────────────
        // 대쉬
        // ──────────────────────────────────────────

        [Header("── 대쉬 ──────────────────────")]

        /// <summary>
        /// 대쉬 이동 거리 (units).
        /// DOTween 목표 위치 = 현재 위치 + 이동방향 * DashDistance.
        /// </summary>
        [Tooltip("대쉬 이동 거리 (units).")]
        public float DashDistance = 5f;

        /// <summary>
        /// 대쉬 지속 시간 (초).
        /// DOTween 애니메이션 duration. 짧을수록 날카로운 이동감.
        /// </summary>
        [Tooltip("대쉬 지속 시간 (초). 권장: 0.15~0.25.")]
        [Min(0.05f)]
        public float DashDuration = 0.2f;

        /// <summary>
        /// 대쉬 쿨타임 (초). 낮을수록 자주 대쉬 가능.
        /// 최솟값 0.3s 내부 보장.
        /// </summary>
        [Tooltip("대쉬 쿨타임 (초). 최솟값 0.3s. 권장: 1.5~3.0.")]
        [Min(0.3f)]
        public float DashCooldown = 2.3f;

        /// <summary>
        /// 대쉬 중 중력 스케일.
        /// 0 = 낙하 없음(수평 대쉬). 대쉬 종료 시 GravityScale 로 복구.
        /// </summary>
        [Tooltip("대쉬 중 중력 스케일. 0 = 낙하 없음.")]
        public float DashGravityScale = 0f;

        /// <summary>
        /// 플레이어 콜라이더 반너비 (units).
        /// 대쉬 벽 감지 Raycast 시작 오프셋.
        /// Collider 실제 반너비와 동일하게 설정 권장.
        /// </summary>
        [Tooltip("콜라이더 반너비 (units). Raycast 시작 오프셋. Collider 반너비와 동일 권장.")]
        public float DashBodyWidth = 0.25f;

        // ──────────────────────────────────────────
        // 지면 감지
        // ──────────────────────────────────────────

        [Header("── 지면 감지 ──────────────────────")]

        /// <summary>
        /// 지면으로 인식할 레이어마스크.
        /// Ground 레이어를 선택.
        /// </summary>
        [Tooltip("지면 레이어. Ground 레이어 선택.")]
        public LayerMask GroundLayer;

        /// <summary>
        /// 지면 감지 OverlapCircle 반경 (units).
        /// </summary>
        [Tooltip("지면 감지 반경 (units). 권장: 0.08~0.15.")]
        [Min(0.01f)]
        public float GroundCheckRadius = 0.1f;

        /// <summary>
        /// 대쉬 벽 감지 레이어마스크.
        /// Ground + Wall 레이어 포함 권장.
        /// </summary>
        [Tooltip("대쉬 중 막힐 레이어. Ground + Wall 레이어 포함 권장.")]
        public LayerMask DashWallLayer;
    }
}