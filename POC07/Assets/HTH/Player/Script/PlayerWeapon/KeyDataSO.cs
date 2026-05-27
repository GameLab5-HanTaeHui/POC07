// ============================================================
// KeyDataSO.cs  v1.1
// 열쇠 무기 데이터 ScriptableObject
//
// [v1.1 변경]
//   무기 스윙 이동 수치 섹션 추가.
//   swingDistance  : 공격 시 Weapon 오브젝트가 앞으로 이동할 거리
//   swingDuration  : 앞으로 뻗는 데 걸리는 시간
//   returnDuration : 원점으로 복귀하는 데 걸리는 시간
//   WeaponMover 가 이 수치를 읽어 DOTween 으로 처리.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 무기 데이터 ScriptableObject. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [사용 흐름]
    ///   1. Project 창에서 열쇠별 SO 에셋 생성
    ///   2. KeyInventoryDataSO.ownedKeys 리스트에 등록
    ///   3. 인게임 획득 시 KeyInventoryDataSO.AcquireKey() 호출
    ///   4. 교체 시 WeaponKeyController.EquipKey(data) 호출
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

        /// <summary> 콤보 윈도우 시간 (초). </summary>
        [Tooltip("다음 콤보 입력 허용 시간 (초). 권장: 0.6~1.0.")]
        [Range(0.3f, 2.0f)]
        [SerializeField] public float comboWindowTime = 0.8f;

        /// <summary> 히트박스 활성 유지 시간 (초). </summary>
        [Tooltip("히트박스 활성 유지 시간 (초). 권장: 0.1~0.2.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] public float hitboxDuration = 0.15f;

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
        /// 모든 콤보 동일한 거리 사용.
        /// </summary>
        [Tooltip("공격 시 무기 앞 이동 거리. 권장: 0.3~0.8.")]
        [Min(0f)]
        [SerializeField] public float swingDistance = 0.5f;

        /// <summary>
        /// 앞으로 뻗는 시간 (초).
        /// Ease.OutQuart — 빠르게 치고 나가는 느낌.
        /// hitboxDuration 보다 짧거나 같게 설정 권장.
        /// </summary>
        [Tooltip("무기가 앞으로 뻗는 시간 (초). hitboxDuration 이하 권장: 0.05~0.15.")]
        [Range(0.02f, 0.3f)]
        [SerializeField] public float swingDuration = 0.08f;

        /// <summary>
        /// 원점으로 복귀하는 시간 (초).
        /// Ease.InQuart — 천천히 당겨지는 느낌.
        /// hitboxDuration 이후 복귀 시작.
        /// </summary>
        [Tooltip("무기가 원점으로 복귀하는 시간 (초). 권장: 0.1~0.25.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] public float returnDuration = 0.15f;

        /// <summary>
        /// 공중 공격 시 Weapon 아래 이동 거리 (로컬 Y, units).
        /// 아래 내리찍기이므로 Y 축 음수 방향으로 이동.
        /// </summary>
        [Tooltip("공중 공격 아래 이동 거리. 권장: 0.3~0.7.")]
        [Min(0f)]
        [SerializeField] public float airSwingDistance = 0.4f;

        // ──────────────────────────────────────────
        // 비주얼 (스프라이트 완성 후 연결)
        // ──────────────────────────────────────────

        [Header("── 비주얼 (스프라이트 완성 후 연결) ──────────────────────")]

        /// <summary>
        /// 열쇠 아이콘 스프라이트. UI 인벤토리 슬롯에 표시.
        /// 미연결 시 빈 슬롯.
        /// </summary>
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
        // 헬퍼
        // ──────────────────────────────────────────

        /// <summary>
        /// 콤보 인덱스의 데미지 배율 반환.
        /// 범위 초과 시 마지막 배율 반환.
        /// </summary>
        public float GetComboMultiplier(int comboIndex)
        {
            if (comboMultipliers == null || comboMultipliers.Length == 0) return 1f;
            int clamped = Mathf.Clamp(comboIndex, 0, comboMultipliers.Length - 1);
            return comboMultipliers[clamped];
        }
    }
}