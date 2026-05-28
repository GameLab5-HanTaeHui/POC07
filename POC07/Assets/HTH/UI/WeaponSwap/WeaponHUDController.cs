// ============================================================
// WeaponHUDController.cs  v1.2
// 무기 HUD 전체 관리 컴포넌트
//
// [v1.2 변경]
//   _panelRoot 추가 — Ctrl 유지 시 패널 전체 활성, 해제 시 비활성.
//   무기 교체 완료(HandleKeyEquipped) 시 패널 자동 닫힘.
//   SetSlotContainerVisible → SetPanelVisible 로 명칭 변경.
//
// [패널 동작]
//   Ctrl 누름  → 패널 열림
//   Ctrl 뗌    → 패널 닫힘
//   무기 교체  → 패널 닫힘 (교체 완료 피드백)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KEY
{
    /// <summary>
    /// 무기 HUD 전체 관리 컴포넌트. (v1.2)
    /// </summary>
    public class WeaponHUDController : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 필수 연결 ──────────────────────")]

        /// <summary> 보유 열쇠 목록 SO. </summary>
        [Tooltip("KeyInventoryDataSO. 필수 연결.")]
        [SerializeField] private KeyInventoryDataSO _inventory;

        /// <summary> WeaponSlotUI 가 부착된 슬롯 Prefab. </summary>
        [Tooltip("WeaponSlotUI Prefab. 필수 연결.")]
        [SerializeField] private WeaponSlotUI _slotPrefab;

        /// <summary> 슬롯이 생성될 부모 Transform. </summary>
        [Tooltip("슬롯 생성 부모 Transform.")]
        [SerializeField] private Transform _slotContainer;

        [Header("── 패널 ──────────────────────")]

        /// <summary>
        /// KeySwap 패널 루트 오브젝트.
        /// Ctrl 누름 시 활성화, Ctrl 뗌 / 무기 교체 시 비활성화.
        /// SlotContainer + 안내 레이블 등을 포함한 부모 오브젝트 연결.
        /// </summary>
        [Tooltip("KeySwap 패널 루트. Ctrl 유지 시 활성화.")]
        [SerializeField] private GameObject _panelRoot;

        [Header("── 현재 장착 무기 표시 ──────────────────────")]

        /// <summary> 현재 장착 무기 아이콘. 항상 표시. </summary>
        [Tooltip("현재 장착 무기 아이콘 Image.")]
        [SerializeField] private Image _equippedIcon;

        /// <summary> 현재 장착 무기 이름. 항상 표시. </summary>
        [Tooltip("현재 장착 무기 이름 TextMeshProUGUI.")]
        [SerializeField] private TextMeshProUGUI _equippedName;

        /// <summary> 장착 무기 없을 때 기본 아이콘. </summary>
        [Tooltip("장착 무기 없을 때 기본 아이콘.")]
        [SerializeField] private Sprite _emptySprite;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private static readonly string[] _keyLabels = new string[]
        {
            "1","2","3","4",
            "Q","W","E","R",
            "A","S","D","F",
            "Z","X","C","V"
        };

        private readonly List<WeaponSlotUI> _slots = new List<WeaponSlotUI>();
        private int _equippedIndex = -1;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Start()
        {
            if (_inventory == null)
            {
                Debug.LogError("[WeaponHUDController] KeyInventoryDataSO 가 연결되지 않았습니다.");
                enabled = false;
                return;
            }

            _inventory.OnKeyAcquired += HandleKeyAcquired;
            _inventory.OnKeyEquipped += HandleKeyEquipped;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnKeySwapModeChanged += HandleKeySwapModeChanged;
                InputManager.Instance.OnKeySwap += HandleKeySwap;
            }
            else
            {
                Debug.LogWarning("[WeaponHUDController] InputManager 가 없습니다.");
            }

            InitializeSlots();

            // 시작 시 패널 닫힘 상태
            SetPanelVisible(false);
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.OnKeyAcquired -= HandleKeyAcquired;
                _inventory.OnKeyEquipped -= HandleKeyEquipped;
            }

            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnKeySwapModeChanged -= HandleKeySwapModeChanged;
                InputManager.Instance.OnKeySwap -= HandleKeySwap;
            }
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러 — 인벤토리
        // ══════════════════════════════════════════════════════

        private void HandleKeyAcquired(KeyDataSO keyData)
        {
            AddSlot(keyData, _slots.Count);
        }

        /// <summary>
        /// 열쇠 장착 완료 시 호출.
        /// 슬롯 강조 갱신 + 현재 장착 표시 갱신 + 패널 닫힘.
        /// </summary>
        private void HandleKeyEquipped(KeyDataSO keyData)
        {
            // ── 슬롯 강조 갱신 ──────────────────────
            int newIndex = -1;
            var owned = _inventory.OwnedKeys;
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] == keyData) { newIndex = i; break; }
            }

            if (_equippedIndex >= 0 && _equippedIndex < _slots.Count)
                _slots[_equippedIndex].SetEquipped(false);

            _equippedIndex = newIndex;
            if (_equippedIndex >= 0 && _equippedIndex < _slots.Count)
                _slots[_equippedIndex].SetEquipped(true);

            // ── 현재 장착 표시 갱신 ──────────────────────
            RefreshEquippedDisplay(keyData);

            // ── 무기 교체 완료 → 패널 닫힘 ──────────────────────
            SetPanelVisible(false);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러 — InputManager KeySwap
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Ctrl 누름/뗌 → 패널 열림/닫힘.
        /// </summary>
        private void HandleKeySwapModeChanged(bool isActive)
        {
            SetPanelVisible(isActive);
        }

        /// <summary>
        /// 슬롯 키 입력 → 해당 인덱스 열쇠 장착.
        /// 장착 완료 시 HandleKeyEquipped 에서 패널 자동 닫힘.
        /// </summary>
        private void HandleKeySwap(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _inventory.KeyCount)
            {
                Debug.Log($"[WeaponHUDController] 슬롯 {slotIndex} 은 비어있습니다.");
                return;
            }

            _inventory.EquipKey(slotIndex);
            // 패널 닫힘은 HandleKeyEquipped 에서 처리
        }

        // ══════════════════════════════════════════════════════
        // 슬롯 관리
        // ══════════════════════════════════════════════════════

        private void InitializeSlots()
        {
            foreach (var slot in _slots)
                if (slot != null) Destroy(slot.gameObject);
            _slots.Clear();

            var owned = _inventory.OwnedKeys;
            for (int i = 0; i < owned.Count; i++)
                AddSlot(owned[i], i);

            if (_inventory.EquippedKey != null)
                RefreshEquippedDisplay(_inventory.EquippedKey);
        }

        private void AddSlot(KeyDataSO keyData, int index)
        {
            var slot = Instantiate(_slotPrefab, _slotContainer);
            string label = index < _keyLabels.Length ? _keyLabels[index] : "-";
            slot.Setup(keyData, index, label);
            _slots.Add(slot);
        }

        // ══════════════════════════════════════════════════════
        // UI 갱신
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 패널 루트 오브젝트 활성/비활성.
        ///
        /// [호출 시점]
        ///   Ctrl 누름  → true  (HandleKeySwapModeChanged)
        ///   Ctrl 뗌    → false (HandleKeySwapModeChanged)
        ///   무기 교체  → false (HandleKeyEquipped)
        /// </summary>
        private void SetPanelVisible(bool visible)
        {
            if (_panelRoot != null)
                _panelRoot.SetActive(visible);
        }

        private void RefreshEquippedDisplay(KeyDataSO keyData)
        {
            if (keyData == null) { ClearEquippedDisplay(); return; }

            if (_equippedIcon != null)
            {
                _equippedIcon.sprite = keyData.keySprite != null ? keyData.keySprite : _emptySprite;
                _equippedIcon.color = keyData.keySprite != null ? Color.white : new Color(1f, 1f, 1f, 0.3f);
            }

            if (_equippedName != null)
                _equippedName.text = keyData.keyName;
        }

        private void ClearEquippedDisplay()
        {
            if (_equippedIcon != null)
            {
                _equippedIcon.sprite = _emptySprite;
                _equippedIcon.color = new Color(1f, 1f, 1f, 0.3f);
            }
            if (_equippedName != null)
                _equippedName.text = "없음";
        }
    }
}