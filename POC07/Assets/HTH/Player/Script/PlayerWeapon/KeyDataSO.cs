// ============================================================
// KeyDataSO.cs  v1.0
// 열쇠 무기 데이터 ScriptableObject
//
// [역할]
//   열쇠 한 종류의 모든 수치·참조를 보관하는 SO.
//   WeaponKeyController 가 이 SO 를 읽어
//   무기 컴포넌트 활성화 및 수치 전달에 사용.
//
// [생성 방법]
//   Project 창 우클릭 → Create → KEY → Key Data
//
// [스프라이트 / 애니메이터]
//   현재는 keySprite / overrideController 필드만 선언.
//   스프라이트 완성 후 Inspector 에서 연결.
//   연결 전까지 null 이어도 무기 동작에는 영향 없음.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 열쇠 무기 데이터 ScriptableObject. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [사용 흐름]
    ///   1. Project 창에서 열쇠별 SO 에셋 생성
    ///      (RustyKeyData, HookKeyData 등)
    ///   2. KeyInventorySO.ownedKeys 리스트에 등록
    ///   3. 인게임 획득 시 KeyInventorySO.AcquireKey() 호출
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

        /// <summary>
        /// 열쇠 이름. UI 표시 및 디버그용.
        /// 예: "녹슨 열쇠", "갈고리 열쇠"
        /// </summary>
        [Tooltip("열쇠 이름. UI 및 디버그용.")]
        [SerializeField] public string keyName = "열쇠";

        /// <summary>
        /// 열쇠 타입. WeaponKeyController 가 이 값으로 무기 컴포넌트를 식별.
        /// </summary>
        [Tooltip("열쇠 타입. WeaponKeyController 가 컴포넌트 매핑에 사용.")]
        [SerializeField] public KeyType keyType;

        /// <summary>
        /// 열쇠 설명. UI 툴팁 등에 사용.
        /// </summary>
        [Tooltip("열쇠 설명 텍스트. UI 툴팁용.")]
        [TextArea(2, 4)]
        [SerializeField] public string description;

        // ──────────────────────────────────────────
        // 전투 수치
        // ──────────────────────────────────────────

        [Header("── 전투 수치 ──────────────────────")]

        /// <summary>
        /// 기본 데미지. RustyKeyWeapon 등 무기 컴포넌트에서
        /// 콤보 배율을 곱해 최종 데미지 계산.
        /// </summary>
        [Tooltip("기본 데미지. 무기 컴포넌트에서 배율 적용.")]
        [Min(1f)]
        [SerializeField] public float baseDamage = 10f;

        /// <summary>
        /// 최대 콤보 단계 수.
        /// 무기 컴포넌트가 이 값을 참조하여 콤보 상한 결정.
        /// </summary>
        [Tooltip("최대 콤보 단계. 무기 컴포넌트가 참조.")]
        [Min(1)]
        [SerializeField] public int comboCount = 3;

        /// <summary>
        /// 콤보 윈도우 시간 (초).
        /// 이 시간 내에 다음 공격 입력 시 다음 콤보 진행.
        /// </summary>
        [Tooltip("다음 콤보 입력 허용 시간 (초). 권장: 0.6~1.0.")]
        [Range(0.3f, 2.0f)]
        [SerializeField] public float comboWindowTime = 0.8f;

        /// <summary>
        /// 히트박스 활성 유지 시간 (초).
        /// 공격 모션 중 실제 타격 판정 유지 구간.
        /// </summary>
        [Tooltip("히트박스 활성 유지 시간 (초). 권장: 0.1~0.2.")]
        [Range(0.05f, 0.5f)]
        [SerializeField] public float hitboxDuration = 0.15f;

        // ──────────────────────────────────────────
        // 콤보 데미지 배율
        // ──────────────────────────────────────────

        [Header("── 콤보 데미지 배율 ──────────────────────")]

        /// <summary>
        /// 각 콤보 단계별 데미지 배율 배열.
        /// 인덱스 0 = Combo1, 1 = Combo2, 2 = Combo3.
        /// comboCount 와 크기를 맞춰야 함.
        /// </summary>
        [Tooltip("콤보 단계별 데미지 배율. 인덱스 0=1단, 1=2단, 2=3단(피니셔).")]
        [SerializeField] public float[] comboMultipliers = { 1.0f, 1.2f, 1.5f };

        /// <summary>
        /// 공중 공격 데미지 배율.
        /// </summary>
        [Tooltip("공중 공격 데미지 배율.")]
        [SerializeField] public float airAttackMultiplier = 1.3f;

        // ──────────────────────────────────────────
        // 비주얼 (스프라이트 완성 후 연결)
        // ──────────────────────────────────────────

        [Header("── 비주얼 (스프라이트 완성 후 연결) ──────────────────────")]

        /// <summary>
        /// 열쇠 아이콘 스프라이트. UI 인벤토리 슬롯에 표시.
        /// 스프라이트 미연결 시 UI 에서 빈 슬롯으로 표시.
        /// </summary>
        [Tooltip("인벤토리 UI 아이콘 스프라이트. 미연결 시 빈 슬롯.")]
        [SerializeField] public Sprite keySprite;

        /// <summary>
        /// AnimatorOverrideController.
        /// 이 열쇠 장착 시 Player Animator 에 적용할 클립 세트.
        /// 스프라이트 완성 후 연결 — null 이어도 무기 동작 정상.
        /// </summary>
        [Tooltip("AnimatorOverrideController. 스프라이트 완성 후 연결.")]
        [SerializeField] public AnimatorOverrideController overrideController;

        // ──────────────────────────────────────────
        // 헬퍼
        // ──────────────────────────────────────────

        /// <summary>
        /// 지정 콤보 인덱스의 데미지 배율을 반환.
        /// 인덱스가 배열 범위를 벗어나면 마지막 배율 반환.
        /// </summary>
        /// <param name="comboIndex">0-based 콤보 인덱스</param>
        public float GetComboMultiplier(int comboIndex)
        {
            if (comboMultipliers == null || comboMultipliers.Length == 0)
                return 1f;

            int clamped = Mathf.Clamp(comboIndex, 0, comboMultipliers.Length - 1);
            return comboMultipliers[clamped];
        }
    }
}