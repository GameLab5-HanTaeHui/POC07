// ============================================================
// KeyDataSO.cs  v1.3
// 열쇠 무기 데이터 ScriptableObject
//
// [v1.3 변경]
//   차징 공격 수치 섹션 추가.
//   minChargeTime / maxChargeTime / chargeAimAngleStep /
//   chargeAimAngleRange / chargeProjectilePrefab
//
// [v1.2 변경]
//   Animator 주도 콤보 타이밍 필드 추가.
//   attackStateDuration / comboWindowStartRatio /
//   hitboxStartRatio / hitboxEndRatio
//   헬퍼 프로퍼티: HitboxStartTime / HitboxEndTime / ComboWindowStartTime
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
    /// 열쇠 무기 데이터 ScriptableObject. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [콤보 타이밍 설계]
    ///   attackStateDuration = Animator 클립 길이와 동일하게 설정.
    ///   comboWindowStartRatio = Animator ExitTime 값과 동일하게 설정.
    ///   hitboxStartRatio / hitboxEndRatio 로 피격 판정 구간 제어.
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

        /// <summary> 열쇠 이름. UI 표시 및 디버그용. </summary>
        [Tooltip("열쇠 이름. UI 및 디버그용.")]
        [SerializeField] public string keyName = "열쇠";

        /// <summary> 열쇠 타입. WeaponKeyController 가 컴포넌트 매핑에 사용. </summary>
        [Tooltip("열쇠 타입. WeaponKeyController 가 컴포넌트 매핑에 사용.")]
        [SerializeField] public KeyType keyType;

        /// <summary> 열쇠 설명. UI 툴팁용. </summary>
        [Tooltip("열쇠 설명 텍스트. UI 툴팁용.")]
        [TextArea(2, 4)]
        [SerializeField] public string description;

        // ──────────────────────────────────────────
        // 전투 수치
        // ──────────────────────────────────────────

        [Header("── 전투 수치 ──────────────────────")]

        /// <summary>
        /// 기본 데미지.
        /// 콤보 배율을 곱해 최종 데미지 계산.
        /// </summary>
        [Tooltip("기본 데미지. 무기 컴포넌트에서 배율 적용.")]
        [Min(1f)]
        [SerializeField] public float baseDamage = 10f;

        /// <summary> 최대 콤보 단계 수. </summary>
        [Tooltip("최대 콤보 단계. 무기 컴포넌트가 참조.")]
        [Min(1)]
        [SerializeField] public int comboCount = 3;

        /// <summary>
        /// 히트박스 활성 유지 시간 (초).
        /// AirAttack 및 레거시 경로에서 사용.
        /// 지상 콤보는 hitboxStartRatio / hitboxEndRatio 우선.
        /// </summary>
        [Tooltip("히트박스 활성 유지 시간 (초). AirAttack 에 사용. 권장: 0.1~0.2.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] public float hitboxDuration = 0.15f;

        // ──────────────────────────────────────────
        // Animator 주도 콤보 타이밍 (v1.2)
        // ──────────────────────────────────────────

        [Header("── Animator 콤보 타이밍 (클립과 일치시킬 것) ──────────────────────")]

        /// <summary>
        /// 공격 1회 Animator 클립 총 길이 (초).
        /// Animation 창에서 PlayerAttack01.anim 의 총 길이 확인 후 동일하게 입력.
        /// </summary>
        [Tooltip("Animator 공격 클립 총 길이 (초). PlayerAttack01.anim StopTime 과 동일하게 설정.")]
        [Min(0.1f)]
        [SerializeField] public float attackStateDuration = 1.0f;

        /// <summary>
        /// 콤보 입력 창이 열리는 클립 진행률 (0~1).
        /// Attack01 → Attack02 전환의 Animator ExitTime 값과 반드시 일치.
        /// </summary>
        [Tooltip("콤보 창 열리는 진행률. Animator Attack01→Attack02 ExitTime 과 동일하게 설정.")]
        [Range(0f, 1f)]
        [SerializeField] public float comboWindowStartRatio = 0.5f;

        /// <summary>
        /// 히트박스 활성 시작 클립 진행률 (0~1).
        /// 공격 모션이 적에게 닿는 프레임에 맞춰 설정.
        /// </summary>
        [Tooltip("히트박스 ON 시점 (클립 진행률). 공격 모션 타격 프레임에 맞출 것.")]
        [Range(0f, 1f)]
        [SerializeField] public float hitboxStartRatio = 0.1f;

        /// <summary>
        /// 히트박스 비활성 종료 클립 진행률 (0~1).
        /// comboWindowStartRatio 보다 작게 설정 권장.
        /// </summary>
        [Tooltip("히트박스 OFF 시점 (클립 진행률). comboWindowStartRatio 보다 작게 설정 권장.")]
        [Range(0f, 1f)]
        [SerializeField] public float hitboxEndRatio = 0.45f;

        // ──────────────────────────────────────────
        // 콤보 데미지 배율
        // ──────────────────────────────────────────

        [Header("── 콤보 데미지 배율 ──────────────────────")]

        /// <summary>
        /// 콤보 단계별 데미지 배율 배열.
        /// 인덱스 0=Combo1, 1=Combo2, 2=Combo3(피니셔).
        /// comboCount 와 크기를 맞춰야 함.
        /// </summary>
        [Tooltip("콤보 단계별 데미지 배율. 0=1단, 1=2단, 2=3단.")]
        [SerializeField] public float[] comboMultipliers = { 1.0f, 1.2f, 1.5f };

        /// <summary> 공중 공격 데미지 배율. </summary>
        [Tooltip("공중 공격 데미지 배율.")]
        [SerializeField] public float airAttackMultiplier = 1.3f;

        // ──────────────────────────────────────────
        // 무기 스윙 이동 (v1.1)
        // ──────────────────────────────────────────

        [Header("── 무기 스윙 이동 ──────────────────────")]

        /// <summary>
        /// 공격 시 Weapon 오브젝트가 앞으로 이동할 거리 (로컬 X, units).
        /// FacingDirection 에 곱해져 방향 자동 처리.
        /// </summary>
        [Tooltip("공격 시 무기 앞 이동 거리. 권장: 0.3~0.8.")]
        [Min(0f)]
        [SerializeField] public float swingDistance = 0.5f;

        /// <summary>
        /// 앞으로 뻗는 시간 (초).
        /// Ease.OutQuart — 빠르게 치고 나가는 느낌.
        /// </summary>
        [Tooltip("무기가 앞으로 뻗는 시간 (초). 권장: 0.05~0.15.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] public float swingDuration = 0.08f;

        /// <summary>
        /// 원점으로 복귀하는 시간 (초).
        /// Ease.InQuart — 천천히 당겨지는 느낌.
        /// </summary>
        [Tooltip("무기가 원점으로 복귀하는 시간 (초). 권장: 0.1~0.25.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] public float returnDuration = 0.15f;

        /// <summary>
        /// 공중 공격 시 Weapon 아래 이동 거리 (로컬 Y, units).
        /// </summary>
        [Tooltip("공중 공격 아래 이동 거리. 권장: 0.3~0.7.")]
        [Min(0f)]
        [SerializeField] public float airSwingDistance = 0.4f;

        // ──────────────────────────────────────────
        // 비주얼 (스프라이트 완성 후 연결)
        // ──────────────────────────────────────────

        [Header("── 비주얼 (스프라이트 완성 후 연결) ──────────────────────")]

        /// <summary> 열쇠 아이콘 스프라이트. UI 인벤토리 슬롯에 표시. </summary>
        [Tooltip("인벤토리 UI 아이콘 스프라이트. 미연결 시 빈 슬롯.")]
        [SerializeField] public Sprite keySprite;

        /// <summary>
        /// AnimatorOverrideController.
        /// 열쇠 장착 시 Player Animator 에 적용할 클립 세트.
        /// 스프라이트 완성 후 연결.
        /// </summary>
        [Tooltip("AnimatorOverrideController. 스프라이트 완성 후 연결.")]
        [SerializeField] public AnimatorOverrideController overrideController;

        // ──────────────────────────────────────────
        // 차징 공격 (v1.3)
        // ──────────────────────────────────────────

        [Header("── 차징 공격 ──────────────────────")]

        /// <summary>
        /// 최소 차징 시간 (초).
        /// 이 시간 미달 시 S 를 떼도 발사 취소.
        /// </summary>
        [Tooltip("최소 차징 시간 (초). 미달 시 발사 취소. 권장: 0.2~0.5.")]
        [Min(0.05f)]
        [SerializeField] public float minChargeTime = 0.3f;

        /// <summary>
        /// 최대 차징 시간 (초).
        /// 이 시간 도달 시 자동 발사.
        /// 차징 비율(0~1) = 경과시간 / maxChargeTime → 투사체 위력에 연동.
        /// </summary>
        [Tooltip("최대 차징 시간 (초). 도달 시 자동 발사. 권장: 1.0~2.0.")]
        [Min(0.1f)]
        [SerializeField] public float maxChargeTime = 1.5f;

        /// <summary>
        /// ↑↓ 한 번당 각도 변화량 (도).
        /// 누를 때마다 발사 각도가 이 값만큼 증감.
        /// </summary>
        [Tooltip("방향키 ↑↓ 한 번당 각도 변화 (도). 권장: 10~20.")]
        [Range(1f, 100f)]
        [SerializeField] public float chargeAimAngleStep = 15f;

        /// <summary>
        /// 발사 각도 최대 범위 (도). ±이 값 사이로 제한.
        /// 0 = 수평 고정, 60 = 위아래 60도까지 조절 가능.
        /// </summary>
        [Tooltip("발사 각도 최대 범위 (도). ±범위 내로 제한. 권장: 45~75.")]
        [Range(0f, 90f)]
        [SerializeField] public float chargeAimAngleRange = 60f;

        /// <summary>
        /// 차징 투사체 Prefab.
        /// IChargeProjectile 인터페이스 구현체가 부착된 Prefab.
        /// null 이면 차징 발사 불가.
        /// </summary>
        [Tooltip("차징 투사체 Prefab. IChargeProjectile 구현체 필요. 추후 연결.")]
        [SerializeField] public GameObject chargeProjectilePrefab;

        // ══════════════════════════════════════════════════════
        // 유틸리티 메서드 / 프로퍼티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 콤보 단계 인덱스에 해당하는 데미지 배율을 반환.
        /// 인덱스가 배열 범위를 초과하면 마지막 값을 반환.
        /// </summary>
        public float GetComboMultiplier(int comboStep)
        {
            if (comboMultipliers == null || comboMultipliers.Length == 0) return 1f;
            int idx = Mathf.Clamp(comboStep, 0, comboMultipliers.Length - 1);
            return comboMultipliers[idx];
        }

        /// <summary> hitboxStartRatio 를 절대 시간(초)으로 변환. </summary>
        public float HitboxStartTime => attackStateDuration * hitboxStartRatio;

        /// <summary> hitboxEndRatio 를 절대 시간(초)으로 변환. </summary>
        public float HitboxEndTime => attackStateDuration * hitboxEndRatio;

        /// <summary> comboWindowStartRatio 를 절대 시간(초)으로 변환. </summary>
        public float ComboWindowStartTime => attackStateDuration * comboWindowStartRatio;
    }
}