// ============================================================
// SealDataSO.cs  v1.0
// 봉인 열쇠 데이터 ScriptableObject
//
// [역할]
//   봉인 열쇠(SealKeyWeapon) 의 모든 수치를 보관.
//   KeyDataSO 와 별도로 존재하는 이유:
//     KeyDataSO 는 근접 콤보 타이밍(hitboxRatio, comboWindow 등)에
//     특화된 구조. 봉인 열쇠는 투사체 + 봉인 지속시간 중심이므로
//     독립 SO 로 분리하여 각자 역할을 명확히 유지.
//
// [생성 방법]
//   Project 창 우클릭 → Create → KEY → Seal Data
//
// [SealDataSO 기본값 (SealKeyData.asset 권장)]
//   sealKeyName       : 봉인 열쇠
//   sealType          : SealType.Dash
//   sealDuration      : 3.0
//   projectileSpeed   : 12.0
//   projectileLifetime: 2.0
//   maxSealCount      : 2
//   cooldown          : 1.5
//
// [로그라이크 강화 연동]
//   sealDuration 증가 → 봉인 시간 연장
//   maxSealCount 증가 → 더 많은 봉인 동시 적용
//   cooldown 감소     → 빠른 재사용
//   projectileSpeed 증가 → 투사체 빠르게
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 봉인 열쇠 수치 데이터 ScriptableObject. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [사용 흐름]
    ///   1. Project 에서 SealData 에셋 생성
    ///   2. sealType 설정 (Dash, Jump, Guard 등)
    ///   3. SealKeyWeapon._sealData 에 연결
    ///   4. SealKeyWeapon 이 발사한 SealProjectile 이 적 명중 시
    ///      EnemySealComponent.ApplySeal(this) 호출
    ///
    /// [봉인 타입별 에셋 분리 권장]
    ///   SealData_Dash.asset    돌진 봉인 전용
    ///   SealData_Guard.asset   방어 봉인 전용
    ///   → 타입마다 지속시간 / 속도를 다르게 튜닝 가능
    /// ────────────────────────────────────────────────────
    /// </summary>
    [CreateAssetMenu(
        fileName = "SealData",
        menuName = "KEY/Seal Data",
        order = 2)]
    public class SealDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────

        [Header("── 기본 정보 ──────────────────────")]

        /// <summary>
        /// 봉인 열쇠 이름. UI 표시 및 디버그용.
        /// </summary>
        [Tooltip("봉인 열쇠 이름. UI 및 디버그 표시용.")]
        [SerializeField] public string sealKeyName = "봉인 열쇠";

        /// <summary>
        /// 봉인 설명. UI 툴팁용.
        /// </summary>
        [Tooltip("봉인 설명 텍스트. UI 툴팁용.")]
        [TextArea(2, 4)]
        [SerializeField] public string description;

        /// <summary>
        /// 이 데이터가 적용하는 봉인 타입.
        /// EnemySealComponent.ApplySeal() 에서 이 값으로 봉인 종류 결정.
        ///
        /// [타입별 효과]
        ///   Dash   : 적의 돌진 / 급이동 차단
        ///   Jump   : 적의 점프 / 상승 차단
        ///   Ranged : 적의 원거리 공격 차단
        ///   Guard  : 적의 방어 / 가드 차단 → 정면 피격 허용
        ///   Move   : 적의 이동 전체 차단 (가장 강력)
        ///   Attack : 적의 모든 공격 차단
        /// </summary>
        [Tooltip("봉인 종류. EnemySealComponent 가 이 타입으로 행동 차단.")]
        [SerializeField] public SealType sealType = SealType.Dash;

        // ──────────────────────────────────────────
        // 봉인 수치
        // ──────────────────────────────────────────

        [Header("── 봉인 수치 ──────────────────────")]

        /// <summary>
        /// 봉인 지속 시간 (초).
        /// EnemySealComponent 가 이 시간 동안 행동을 차단.
        /// 타이머 만료 시 자동 해제.
        ///
        /// [같은 타입 중복 명중 시]
        ///   기존 타이머를 이 값으로 리셋 (스택 없음).
        ///   다른 타입은 동시에 적용 가능.
        ///
        /// [권장값]
        ///   Move   : 1.5~2.0 (전체 이동 차단 — 짧게 유지)
        ///   Guard  : 3.0~4.0 (방어 차단 — 공략 시간 확보)
        ///   Dash   : 3.0~5.0 (돌진 차단 — 여유 있게)
        ///   Attack : 2.0~3.0 (공격 차단 — 밸런스 주의)
        /// </summary>
        [Tooltip("봉인 지속 시간 (초). 만료 시 자동 해제. Move 봉인은 짧게 권장.")]
        [Min(0.5f)]
        [SerializeField] public float sealDuration = 3.0f;

        /// <summary>
        /// 동시에 적에게 걸 수 있는 최대 봉인 개수.
        /// 같은 타입 중복 불가. 서로 다른 타입은 이 수치까지 동시 적용.
        ///
        /// [예시]
        ///   maxSealCount = 2 이면 Dash + Guard 동시 적용 가능.
        ///   3번째 봉인 시도 시 가장 오래된 봉인 제거 후 적용.
        /// </summary>
        [Tooltip("동시 최대 봉인 수. 초과 시 가장 오래된 봉인 제거.")]
        [Min(1)]
        [SerializeField] public int maxSealCount = 2;

        // ──────────────────────────────────────────
        // 투사체 수치
        // ──────────────────────────────────────────

        [Header("── 투사체 수치 ──────────────────────")]

        /// <summary>
        /// 투사체 이동 속도 (units/s).
        /// SealProjectile 이 이 속도로 직진.
        /// </summary>
        [Tooltip("봉인 투사체 속도 (units/s). 권장: 10~15.")]
        [Min(1f)]
        [SerializeField] public float projectileSpeed = 12.0f;

        /// <summary>
        /// 투사체 최대 생존 시간 (초).
        /// 이 시간 내에 적에 명중하지 못하면 자동 소멸.
        /// </summary>
        [Tooltip("투사체 최대 생존 시간 (초). 권장: 1.5~3.0.")]
        [Min(0.5f)]
        [SerializeField] public float projectileLifetime = 2.0f;

        /// <summary>
        /// 투사체 크기 스케일.
        /// 1.0 = 기본 크기. 값이 클수록 투사체 판정 범위 증가.
        /// </summary>
        [Tooltip("투사체 크기 스케일. 1.0 = 기본. 클수록 판정 범위 증가.")]
        [Min(0.1f)]
        [SerializeField] public float projectileScale = 1.0f;

        // ──────────────────────────────────────────
        // 무기 사용 수치
        // ──────────────────────────────────────────

        [Header("── 무기 사용 수치 ──────────────────────")]

        /// <summary>
        /// 봉인 열쇠 재사용 대기 시간 (초).
        /// 발사 후 이 시간이 지나야 다시 발사 가능.
        /// </summary>
        [Tooltip("발사 후 재사용 대기 시간 (초). 권장: 1.0~2.0.")]
        [Min(0.1f)]
        [SerializeField] public float cooldown = 1.5f;

        /// <summary>
        /// 봉인 해제 시 플래시 간격 (초).
        /// EnemySealComponent 가 봉인 상태에서 깜빡임에 사용.
        /// </summary>
        [Tooltip("봉인 중 스프라이트 깜빡임 간격 (초). 권장: 0.3~0.6.")]
        [Range(0.1f, 1.0f)]
        [SerializeField] public float sealFlashInterval = 0.4f;

        // ──────────────────────────────────────────
        // 비주얼 (스프라이트 완성 후 연결)
        // ──────────────────────────────────────────

        [Header("── 비주얼 (스프라이트 완성 후 연결) ──────────────────────")]

        /// <summary>
        /// 봉인 열쇠 아이콘 스프라이트. UI 인벤토리 슬롯에 표시.
        /// </summary>
        [Tooltip("인벤토리 UI 아이콘 스프라이트. 미연결 시 빈 슬롯.")]
        [SerializeField] public Sprite keySprite;

        /// <summary>
        /// 투사체 스프라이트. SealProjectile 오브젝트에 사용.
        /// </summary>
        [Tooltip("봉인 투사체 스프라이트. SealProjectile SpriteRenderer 에 적용.")]
        [SerializeField] public Sprite projectileSprite;

        /// <summary>
        /// 봉인 오버레이 스프라이트.
        /// 적이 봉인 상태일 때 적 위에 표시되는 자물쇠 이미지.
        /// </summary>
        [Tooltip("봉인 상태 오버레이 스프라이트. 적 위에 표시되는 자물쇠 이미지.")]
        [SerializeField] public Sprite sealOverlaySprite;

        /// <summary>
        /// 봉인 색상. 봉인 상태 오버레이 및 플래시에 사용.
        /// 타입별로 다른 색상 권장.
        ///
        /// [색상 권장]
        ///   Dash   : 파란색   (0.3, 0.5, 1.0)
        ///   Jump   : 초록색   (0.3, 1.0, 0.5)
        ///   Ranged : 주황색   (1.0, 0.6, 0.2)
        ///   Guard  : 노란색   (1.0, 0.9, 0.2)
        ///   Move   : 보라색   (0.7, 0.2, 1.0)
        ///   Attack : 빨간색   (1.0, 0.2, 0.2)
        /// </summary>
        [Tooltip("봉인 색상. 오버레이 및 플래시에 사용. 타입별 구분 권장.")]
        [SerializeField] public Color sealColor = new Color(0.3f, 0.5f, 1.0f, 1.0f);
    }
}