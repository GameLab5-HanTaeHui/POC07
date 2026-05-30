// ============================================================
// KeyDataSO.cs  v1.4
// 열쇠 무기 데이터 ScriptableObject
//
// [v1.4 변경]
//   봉인 수치 섹션 추가 — SealDataSO 제거 후 통합.
//   모든 열쇠는 S키로 봉인 투사체를 발사할 수 있음.
//   추가 필드:
//     sealType          : 봉인 종류 (Dash / Guard / Move / Attack / Jump / Ranged)
//     sealDuration      : 봉인 지속 시간 (초)
//     maxSealCount      : 동시 최대 봉인 수
//     sealProjectileSpeed    : 투사체 이동 속도
//     sealProjectileLifetime : 투사체 생존 시간
//     sealProjectileScale    : 투사체 크기
//     sealFlashInterval      : 봉인 중 깜빡임 간격
//     sealOverlaySprite      : 봉인 오버레이 스프라이트
//     sealColor              : 봉인 색상
//
// [v1.3 변경]
//   차징 공격 수치 섹션 추가.
//
// [v1.2 변경]
//   Animator 주도 콤보 타이밍 필드 추가.
//
// [v1.1 변경]
//   무기 스윙 이동 수치 섹션 추가.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 무기 데이터 ScriptableObject. (v1.4)
    ///
    /// ────────────────────────────────────────────────────
    /// [S키 봉인 투사체 흐름]
    ///   PlayerChargeAttack.Fire()
    ///     → chargeProjectilePrefab Instantiate
    ///     → SealProjectile.Launch(KeyDataSO, facingDir, chargePower)
    ///     → Enemy 명중 → SealComponent.ApplySeal(KeyDataSO)
    ///
    /// [A키 근접 공격 흐름]
    ///   RustyKeyWeapon.Attack()
    ///     → 콤보 히트박스 활성
    /// ────────────────────────────────────────────────────
    /// </summary>
    [CreateAssetMenu(
        fileName = "KeyData",
        menuName = "KEY/Key Data",
        order = 0)]
    public class KeyDataSO : ScriptableObject
    {
        // ──────────────────────────────────────────
        // 기본 정보
        // ──────────────────────────────────────────

        [Header("── 기본 정보 ──────────────────────")]

        [Tooltip("열쇠 이름. UI 및 디버그용.")]
        [SerializeField] public string keyName = "열쇠";

        [Tooltip("열쇠 타입. WeaponKeyController 가 컴포넌트 매핑에 사용.")]
        [SerializeField] public KeyType keyType;

        [Tooltip("열쇠 설명 텍스트. UI 툴팁용.")]
        [TextArea(2, 4)]
        [SerializeField] public string description;

        // ──────────────────────────────────────────
        // 전투 수치 (A키 근접)
        // ──────────────────────────────────────────

        [Header("── 전투 수치 (A키 근접) ──────────────────────")]

        [Tooltip("기본 데미지.")]
        [Min(1f)]
        [SerializeField] public float baseDamage = 10f;

        [Tooltip("최대 콤보 단계.")]
        [Min(1)]
        [SerializeField] public int comboCount = 3;

        [Tooltip("히트박스 활성 유지 시간 (초). AirAttack 에 사용.")]
        [Min(0.05f)]
        [SerializeField] public float hitboxDuration = 0.15f;

        [Tooltip("공격 상태 지속 시간. Animator 클립 길이와 동일하게.")]
        [Min(0.1f)]
        [SerializeField] public float attackStateDuration = 1.0f;

        [Tooltip("콤보 입력 허용 시작 비율. Animator ExitTime 과 동일하게.")]
        [Range(0f, 1f)]
        [SerializeField] public float comboWindowStartRatio = 0.5f;

        [Tooltip("히트박스 활성 시작 비율.")]
        [Range(0f, 1f)]
        [SerializeField] public float hitboxStartRatio = 0.1f;

        [Tooltip("히트박스 활성 종료 비율.")]
        [Range(0f, 1f)]
        [SerializeField] public float hitboxEndRatio = 0.45f;

        [Tooltip("콤보 단계별 데미지 배율.")]
        [SerializeField] public float[] comboMultipliers = { 1.0f, 1.2f, 1.5f };

        [Tooltip("공중 공격 데미지 배율.")]
        [Min(0f)]
        [SerializeField] public float airAttackMultiplier = 1.3f;

        // ──────────────────────────────────────────
        // 콤보별 스윙 수치
        // ──────────────────────────────────────────

        [Header("── 콤보별 스윙 수치 ──────────────────────")]

        /// <summary>
        /// 백스윙 거리 (units).
        /// 공격 전 무기를 반대 방향으로 당기는 준비 동작 거리.
        /// </summary>
        [Tooltip("백스윙 거리 (units). 공격 전 반대 방향으로 당기는 거리. 권장: 0.1~0.25")]
        [Min(0f)]
        [SerializeField] public float backswingDistance = 0.15f;

        /// <summary>
        /// 백스윙 지속 시간 (초).
        /// </summary>
        [Tooltip("백스윙 지속 시간 (초). 권장: 0.05~0.08")]
        [Min(0.01f)]
        [SerializeField] public float backswingDuration = 0.06f;

        /// <summary>
        /// Combo1 타격 이동 거리 (units). 가로 횡베기 X 전진.
        /// </summary>
        [Tooltip("Combo1 타격 거리 (units). 가로 횡베기 X 전진. 권장: 0.35~0.5")]
        [Min(0f)]
        [SerializeField] public float combo1AttackDistance = 0.45f;

        /// <summary>
        /// Combo1 타격 시 Z축 회전 각도 (도). 칼날 아래로 향하는 횡베기 느낌.
        /// facing 방향에 따라 부호 자동 적용됨.
        /// </summary>
        [Tooltip("Combo1 Z축 회전 (도). 횡베기 칼날 기울기. 권장: 25~40")]
        [Range(0f, 90f)]
        [SerializeField] public float combo1RotationZ = 35f;

        /// <summary>
        /// Combo2 내리찍기 Y 하향 타격 거리 (units).
        /// </summary>
        [Tooltip("Combo2 Y 하향 타격 거리 (units). 내리찍기. 권장: 0.3~0.5")]
        [Min(0f)]
        [SerializeField] public float combo2AttackDistanceY = 0.4f;

        /// <summary>
        /// Combo2 타격 시 Z축 회전 각도 (도). 내리찍기 칼날 기울기.
        /// </summary>
        [Tooltip("Combo2 Z축 회전 (도). 내리찍기 칼날 기울기. 권장: 20~35")]
        [Range(0f, 90f)]
        [SerializeField] public float combo2RotationZ = 25f;

        /// <summary>
        /// Combo3 찌르기 X 전진 타격 거리 (units). 가장 큰 전진.
        /// </summary>
        [Tooltip("Combo3 타격 거리 (units). 찌르기 피니셔 X 전진. 권장: 0.5~0.7")]
        [Min(0f)]
        [SerializeField] public float combo3AttackDistance = 0.6f;

        /// <summary>
        /// 공중 공격 Z축 회전 기본 각도 (도).
        /// AirSide / AirDown / AirUp 에 각각 부호를 다르게 적용.
        /// </summary>
        [Tooltip("공중 공격 Z축 회전 기본 각도 (도). 권장: 40~60")]
        [Range(0f, 90f)]
        [SerializeField] public float airAttackRotationZ = 50f;

        // ──────────────────────────────────────────
        // 스윙 이동 수치
        // ──────────────────────────────────────────

        [Header("── 스윙 이동 수치 ──────────────────────")]

        [Tooltip("스윙 이동 거리 (units).")]
        [Min(0f)]
        [SerializeField] public float swingDistance = 0.5f;

        [Tooltip("스윙 이동 시간 (초).")]
        [Min(0.01f)]
        [SerializeField] public float swingDuration = 0.08f;

        [Tooltip("원점 복귀 시간 (초).")]
        [Min(0.01f)]
        [SerializeField] public float returnDuration = 0.15f;

        [Tooltip("공중 스윙 이동 거리 (units).")]
        [Min(0f)]
        [SerializeField] public float airSwingDistance = 0.4f;

        // ──────────────────────────────────────────
        // S키 봉인 투사체 수치
        // ──────────────────────────────────────────

        [Header("── S키 봉인 투사체 ──────────────────────")]

        [Tooltip("최소 차징 시간 (초). 미달 시 발사 취소.")]
        [Min(0.05f)]
        [SerializeField] public float minChargeTime = 0.3f;

        [Tooltip("최대 차징 시간 (초).")]
        [Min(0.1f)]
        [SerializeField] public float maxChargeTime = 1.5f;

        [Tooltip("방향키 ↑↓ 초당 각도 변화량.")]
        [Range(1f, 100f)]
        [SerializeField] public float chargeAimAngleStep = 15f;

        [Tooltip("발사 각도 최대 범위 (도). ±범위 내로 제한.")]
        [Range(0f, 90f)]
        [SerializeField] public float chargeAimAngleRange = 60f;

        [Tooltip("봉인 투사체 Prefab. SealProjectile 컴포넌트 필요.")]
        [SerializeField] public GameObject chargeProjectilePrefab;

        [Tooltip("투사체 이동 속도 (units/s).")]
        [Min(1f)]
        [SerializeField] public float sealProjectileSpeed = 12f;

        [Tooltip("투사체 최대 생존 시간 (초).")]
        [Min(0.5f)]
        [SerializeField] public float sealProjectileLifetime = 2f;

        [Tooltip("투사체 크기 스케일.")]
        [Min(0.1f)]
        [SerializeField] public float sealProjectileScale = 1f;

        // ──────────────────────────────────────────
        // S키 봉인 기능 수치
        // ──────────────────────────────────────────

        [Header("── S키 봉인 기능 ──────────────────────")]

        /// <summary>
        /// 이 열쇠가 적용하는 봉인 종류.
        /// 투사체 명중 시 SealComponent.ApplySeal() 에 전달.
        /// </summary>
        [Tooltip("봉인 종류. 투사체 명중 시 적에게 적용되는 봉인 타입.")]
        [SerializeField] public SealType sealType = SealType.Dash;

        [Tooltip("봉인 지속 시간 (초). 만료 시 자동 해제.")]
        [Min(0.5f)]
        [SerializeField] public float sealDuration = 3f;

        [Tooltip("동시 최대 봉인 수. 초과 시 가장 오래된 봉인 제거.")]
        [Min(1)]
        [SerializeField] public int maxSealCount = 2;

        [Tooltip("봉인 중 스프라이트 깜빡임 간격 (초).")]
        [Range(0.1f, 1f)]
        [SerializeField] public float sealFlashInterval = 0.4f;

        [Tooltip("봉인 오버레이 스프라이트. 적 위에 표시되는 자물쇠 이미지.")]
        [SerializeField] public Sprite sealOverlaySprite;

        [Tooltip("봉인 색상. 오버레이 및 플래시에 사용.")]
        [SerializeField] public Color sealColor = new Color(0.3f, 0.5f, 1.0f, 1.0f);

        // ──────────────────────────────────────────
        // 비주얼
        // ──────────────────────────────────────────

        [Header("── 비주얼 ──────────────────────")]

        [Tooltip("인벤토리 UI 아이콘 스프라이트.")]
        [SerializeField] public Sprite keySprite;

        [Tooltip("AnimatorOverrideController. 스프라이트 완성 후 연결.")]
        [SerializeField] public RuntimeAnimatorController overrideController;

        // ──────────────────────────────────────────
        // 유틸리티
        // ──────────────────────────────────────────

        public float GetComboMultiplier(int comboStep)
        {
            if (comboMultipliers == null || comboMultipliers.Length == 0) return 1f;
            int idx = Mathf.Clamp(comboStep, 0, comboMultipliers.Length - 1);
            return comboMultipliers[idx];
        }

        public float HitboxStartTime => attackStateDuration * hitboxStartRatio;
        public float HitboxEndTime => attackStateDuration * hitboxEndRatio;
        public float ComboWindowStartTime => attackStateDuration * comboWindowStartRatio;
    }
}