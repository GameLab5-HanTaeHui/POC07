// ============================================================
// WeaponSlotUI.cs  v1.1
// 무기 인벤토리 개별 슬롯 UI 컴포넌트
//
// [v1.1 변경]
//   Button 제거 — 키 바인딩으로 교체하므로 클릭 불필요.
//   _keyBindingText 추가 — 이 슬롯에 배정된 키 이름 표시.
//     예) 슬롯 0 → "1", 슬롯 4 → "Q", 슬롯 8 → "A"
//   Setup() 에 keyBindingLabel 파라미터 추가.
//   OnSlotClicked() / OnDestroy() Button 관련 코드 제거.
//
// [Hierarchy — Prefab 구조]
//   WeaponSlot (Prefab)
//   ├── [WeaponSlotUI]
//   ├── [Image]            슬롯 배경 (장착 시 색상 변경)
//   ├── Icon
//   │     └── [Image]      keySprite 표시
//   ├── KeyName
//   │     └── [TMP]        keyData.keyName
//   ├── KeyBinding         ★ 신규 — 배정된 키 이름
//   │     └── [TMP]        예) "1", "Q", "A", "Z" 등
//   └── EquippedIndicator  장착 중 강조
//         └── [Image]      장착 시 활성화
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KEY
{
    /// <summary>
    /// 무기 인벤토리 개별 슬롯 UI. (v1.1)
    ///
    /// ────────────────────────────────────────────────────
    /// [WeaponHUDController 에서의 사용 흐름]
    ///   1. Instantiate(slotPrefab, parent)
    ///   2. slot.Setup(keyData, index, inventory, keyBindingLabel)
    ///   3. 장착 변경 시 SetEquipped(bool) 으로 강조 갱신
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class WeaponSlotUI : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── UI 연결 ──────────────────────")]

        /// <summary> 열쇠 아이콘 이미지. keySprite 표시. </summary>
        [Tooltip("열쇠 아이콘 Image 컴포넌트.")]
        [SerializeField] private Image _iconImage;

        /// <summary> 열쇠 이름 텍스트. keyData.keyName 표시. </summary>
        [Tooltip("열쇠 이름 TextMeshProUGUI.")]
        [SerializeField] private TextMeshProUGUI _nameText;

        /// <summary>
        /// 배정된 키 바인딩 텍스트.
        /// Setup() 에서 전달받은 keyBindingLabel 을 표시.
        /// 예) 슬롯 0 → "1" / 슬롯 4 → "Q" / 슬롯 8 → "A"
        /// KeySwap 모드 시 플레이어가 어떤 키를 눌러야 하는지 안내.
        /// </summary>
        [Tooltip("배정된 키 이름 TextMeshProUGUI. 예) \"1\", \"Q\", \"A\"")]
        [SerializeField] private TextMeshProUGUI _keyBindingText;

        /// <summary>
        /// 장착 중 강조 표시 오브젝트.
        /// 장착 시 활성화, 미장착 시 비활성화.
        /// </summary>
        [Tooltip("장착 중 강조 표시 오브젝트 (테두리 등).")]
        [SerializeField] private GameObject _equippedIndicator;

        [Header("── 색상 설정 ──────────────────────")]

        /// <summary> 장착 중 슬롯 배경색. </summary>
        [Tooltip("장착 중 슬롯 배경색.")]
        [SerializeField] private Color _equippedColor = new Color(1f, 0.85f, 0.2f, 1f);

        /// <summary> 미장착 슬롯 배경색. </summary>
        [Tooltip("미장착 슬롯 배경색.")]
        [SerializeField] private Color _normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        // ──────────────────────────────────────────
        // 내부 참조
        // ──────────────────────────────────────────

        private Image _backgroundImage;

        // ══════════════════════════════════════════════════════
        // 초기화
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 슬롯 초기화.
        /// WeaponHUDController 가 Instantiate 직후 호출.
        /// </summary>
        /// <param name="keyData">이 슬롯에 표시할 열쇠 데이터</param>
        /// <param name="index">KeyInventoryDataSO 내 인덱스 (미사용 — 확장 대비)</param>
        /// <param name="keyBindingLabel">
        /// 이 슬롯에 배정된 키 이름.
        /// WeaponHUDController 가 슬롯 인덱스 기반으로 계산하여 전달.
        /// 예) "1", "2", "Q", "W", "A", "Z" 등
        /// </param>
        public void Setup(KeyDataSO keyData, int index, string keyBindingLabel)
        {
            _backgroundImage = GetComponent<Image>();

            // ── 아이콘 ──────────────────────
            if (_iconImage != null)
            {
                _iconImage.sprite = keyData.keySprite;
                _iconImage.color = keyData.keySprite != null
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.3f);
            }

            // ── 열쇠 이름 ──────────────────────
            if (_nameText != null)
                _nameText.text = keyData.keyName;

            // ── 키 바인딩 텍스트 ──────────────────────
            // 슬롯에 배정된 키 이름 표시. 비어있으면 "-" 표시.
            if (_keyBindingText != null)
                _keyBindingText.text = string.IsNullOrEmpty(keyBindingLabel)
                    ? "-"
                    : keyBindingLabel;

            // 초기 상태 비장착
            SetEquipped(false);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 장착 상태 표시 갱신.
        /// WeaponHUDController 가 장착 변경 시 호출.
        /// </summary>
        /// <param name="isEquipped">현재 이 슬롯이 장착 중인지 여부</param>
        public void SetEquipped(bool isEquipped)
        {
            if (_equippedIndicator != null)
                _equippedIndicator.SetActive(isEquipped);

            if (_backgroundImage != null)
                _backgroundImage.color = isEquipped ? _equippedColor : _normalColor;
        }
    }
}