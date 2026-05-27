// ============================================================
// KeyDataSO.cs  v1.2
// 열쇠 무기 데이터 ScriptableObject
//
// [v1.2 변경]
//   Animator 주도 콤보 시스템을 위한 타이밍 필드 추가.
//
//   attackStateDuration  : Animator 클립 총 길이 (초)
//                          RustyKeyWeapon 이 Animator 상태 시간을
//                          직접 읽지 않고 이 값으로 폴링 주기 계산.
//
//   comboWindowStartRatio : 콤보 입력 창이 열리는 시점 (0~1, 클립 진행률)
//                           Animator ExitTime 과 반드시 일치시킬 것.
//                           예: 0.5 → 클립 50% 이후 다음 콤보 입력 허용
//
//   hitboxStartRatio      : 히트박스 활성화 시점 (0~1)
//   hitboxEndRatio        : 히트박스 비활성화 시점 (0~1)
//                           두 값으로 히트박스 활성 구간을 클립과 동기화.
//                           예: start=0.15, end=0.45 → 클립 15%~45% 구간 활성
//
// [기존 hitboxDuration 유지 이유]
//   hitboxDuration 는 AirAttack 및 레거시 경로 호환용으로 유지.
//   지상 콤보는 hitboxStartRatio / hitboxEndRatio 우선 사용.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 무기 데이터 ScriptableObject. (v1.2)
    ///
    /// ────────────────────────────────────────────────────
    /// [콤보 타이밍 설계]
    ///   attackStateDuration = Animator 클립 길이와 동일하게 설정.
    ///   comboWindowStartRatio = Animator ExitTime 값과 동일하게 설정.
    ///   hitboxStartRatio / hitboxEndRatio 로 피격 판정 구간 제어.
    ///
    ///   예) 클립 1초 / ExitTime 0.5:
    ///     attackStateDuration   = 1.0
    ///     comboWindowStartRatio = 0.5  ← Animator ExitTime 과 동일
    ///     hitboxStartRatio      = 0.1  ← 클립 10% 시점에 히트박스 ON
    ///     hitboxEndRatio        = 0.45 ← 클립 45% 시점에 히트박스 OFF
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
        // Animator 주도 콤보 타이밍 (v1.2 신규)
        // ──────────────────────────────────────────

        [Header("── Animator 콤보 타이밍 (클립과 일치시킬 것) ──────────────────────")]

        /// <summary>
        /// 공격 1회 Animator 클립 총 길이 (초).
        ///
        /// [설정 방법]
        ///   Animation 창에서 PlayerAttack01.anim 의 총 길이를 확인하여 동일하게 입력.
        ///   현재 PlayerAttack01.anim m_StopTime = 1.0 → 이 값도 1.0.
        ///
        /// [용도]
        ///   RustyKeyWeapon 이 Animator.GetCurrentAnimatorStateInfo 를
        ///   매 프레임 호출하는 대신 이 값으로 절대 시간 계산.
        ///   히트박스 ON/OFF 시점을 절대 초(second)로 변환할 때 사용.
        /// </summary>
        [Tooltip("Animator 공격 클립 총 길이 (초). PlayerAttack01.anim 의 StopTime 과 동일하게 설정.")]
        [Min(0.1f)]
        [SerializeField] public float attackStateDuration = 1.0f;

        /// <summary>
        /// 콤보 입력 창이 열리는 클립 진행률 (0~1).
        ///
        /// [Animator Controller 와 반드시 일치]
        ///   Attack01 → Attack02 전환의 ExitTime 값과 동일하게 설정.
        ///   예: ExitTime = 0.5 → 이 값도 0.5.
        ///
        /// [동작]
        ///   이 비율에 도달하기 전 클릭은 _inputBuffered 에만 저장됨.
        ///   이 비율 이후 클릭 or 이미 버퍼에 있는 입력이 있으면
        ///   다음 콤보 Trigger 발행.
        /// </summary>
        [Tooltip("콤보 창 열리는 진행률. Animator Attack01→Attack02 ExitTime 과 동일하게 설정.")]
        [Range(0f, 1f)]
        [SerializeField] public float comboWindowStartRatio = 0.5f;

        /// <summary>
        /// 히트박스 활성 시작 클립 진행률 (0~1).
        ///
        /// [예시]
        ///   0.1 → 클립 10% 시점에 히트박스 ON.
        ///   공격 모션이 적에게 닿는 프레임과 맞춰서 설정.
        /// </summary>
        [Tooltip("히트박스 ON 시점 (클립 진행률). 공격 모션 타격 프레임에 맞출 것.")]
        [Range(0f, 1f)]
        [SerializeField] public float hitboxStartRatio = 0.1f;

        /// <summary>
        /// 히트박스 비활성 종료 클립 진행률 (0~1).
        ///
        /// [주의]
        ///   comboWindowStartRatio 보다 작게 설정 권장.
        ///   콤보 창이 열리기 전에 히트박스가 닫혀야
        ///   같은 공격에서 중복 피격이 방지됨.
        ///   예: hitboxEnd=0.4 / comboWindow=0.5 → 문제 없음.
        ///       hitboxEnd=0.6 / comboWindow=0.5 → 창 열린 후에도 히트박스 활성 = 중복 위험.
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
        // 무기 스윙 이동
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

        // ══════════════════════════════════════════════════════
        // 유틸리티 메서드
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 콤보 단계 인덱스에 해당하는 데미지 배율을 반환.
        /// 인덱스가 배열 범위를 초과하면 마지막 값을 반환.
        /// </summary>
        /// <param name="comboStep">0-based 콤보 단계</param>
        /// <returns>데미지 배율</returns>
        public float GetComboMultiplier(int comboStep)
        {
            if (comboMultipliers == null || comboMultipliers.Length == 0) return 1f;
            int idx = Mathf.Clamp(comboStep, 0, comboMultipliers.Length - 1);
            return comboMultipliers[idx];
        }

        /// <summary>
        /// hitboxStartRatio 를 절대 시간(초)으로 변환.
        /// </summary>
        public float HitboxStartTime => attackStateDuration * hitboxStartRatio;

        /// <summary>
        /// hitboxEndRatio 를 절대 시간(초)으로 변환.
        /// </summary>
        public float HitboxEndTime => attackStateDuration * hitboxEndRatio;

        /// <summary>
        /// comboWindowStartRatio 를 절대 시간(초)으로 변환.
        /// </summary>
        public float ComboWindowStartTime => attackStateDuration * comboWindowStartRatio;
    }
}