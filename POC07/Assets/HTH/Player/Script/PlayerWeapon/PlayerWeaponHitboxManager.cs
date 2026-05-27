// ============================================================
// PlayerWeaponHitboxManager.cs  v1.0
// 플레이어 무기 히트박스 관리 컴포넌트
//
// [역할]
//   콤보 단계별 히트박스(Collider2D)를 활성/비활성하고
//   히트 감지 시 OnHit 이벤트를 발행.
//   RustyKeyWeapon 에서 공격 타이밍에 맞춰 호출.
//
// [히트박스 구조]
//   _hitboxes[0] : Combo1 히트박스
//   _hitboxes[1] : Combo2 히트박스
//   _hitboxes[2] : Combo3 히트박스
//   _hitboxes[3] : AirAttack 히트박스
//
// [히트 판정 방식]
//   히트박스 활성화 중 OverlapCollider 로 매 프레임 검사.
//   동일 타격에서 중복 피격 방지: _hitTargets HashSet 관리.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 무기 히트박스 관리 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [RustyKeyWeapon 에서의 사용 흐름]
    ///   1. 공격 시작 시 EnableHitbox(comboIndex, damageInfo) 호출
    ///   2. 이 컴포넌트가 해당 히트박스를 활성화
    ///   3. Update 에서 OverlapCollider 로 IDamageable 감지
    ///   4. 감지 시 OnHit 이벤트 발행 → IDamageable.TakeDamage() 호출
    ///   5. 공격 모션 종료 시 DisableAllHitboxes() 호출
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponHitboxManager : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 히트박스 인덱스 상수
        // ──────────────────────────────────────────

        /// <summary> Combo1 히트박스 인덱스. </summary>
        public const int HitboxCombo1 = 0;

        /// <summary> Combo2 히트박스 인덱스. </summary>
        public const int HitboxCombo2 = 1;

        /// <summary> Combo3 히트박스 인덱스. </summary>
        public const int HitboxCombo3 = 2;

        /// <summary> 공중 공격 히트박스 인덱스. </summary>
        public const int HitboxAirAttack = 3;

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트박스 콜라이더 연결 ──────────────────────")]

        /// <summary>
        /// 콤보별 히트박스 Collider2D 배열.
        /// 인덱스: 0=Combo1, 1=Combo2, 2=Combo3, 3=AirAttack
        /// Inspector 에서 순서대로 연결.
        /// </summary>
        [Tooltip("콤보별 히트박스. 인덱스 0=Combo1, 1=Combo2, 2=Combo3, 3=AirAttack.")]
        [SerializeField] private Collider2D[] _hitboxes;

        [Header("── 감지 설정 ──────────────────────")]

        /// <summary>
        /// 히트 판정 LayerMask.
        /// 적(Enemy) 레이어를 포함한 레이어만 감지.
        /// </summary>
        [Tooltip("히트 판정 레이어. Enemy 레이어 선택.")]
        [SerializeField] private LayerMask _hitLayer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 활성화된 히트박스 인덱스. -1 = 비활성.
        /// </summary>
        private int _activeHitboxIndex = -1;

        /// <summary>
        /// 현재 타격에서 이미 맞은 오브젝트 목록.
        /// 히트박스 활성 기간 동안 중복 피격 방지에 사용.
        /// DisableAllHitboxes() 호출 시 초기화.
        /// </summary>
        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();

        /// <summary>
        /// 현재 활성 히트박스에 전달할 데미지 정보.
        /// EnableHitbox() 호출 시 설정.
        /// </summary>
        private DamageInfo _currentDamageInfo;

        /// <summary>
        /// OverlapCollider 결과 임시 버퍼.
        /// 매 프레임 List 재할당 없이 재사용.
        /// </summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary>
        /// IDamageable 에 히트 발생 시 발행.
        /// 파라미터: (피격된 IDamageable, DamageInfo)
        /// 외부 시스템(이펙트, 사운드 등)에서 구독하여 추가 처리 가능.
        /// </summary>
        public event Action<IDamageable, DamageInfo> OnHit;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 초기화 — 모든 히트박스 비활성화.
        /// </summary>
        private void Awake()
        {
            if (_hitboxes == null || _hitboxes.Length == 0)
            {
                Debug.LogWarning("[PlayerWeaponHitboxManager] 히트박스가 연결되지 않았습니다.");
                return;
            }

            DisableAllHitboxes();
        }

        /// <summary>
        /// 활성화된 히트박스가 있는 경우 매 프레임 히트 감지.
        /// </summary>
        private void Update()
        {
            if (_activeHitboxIndex < 0) return;
            CheckHit(_hitboxes[_activeHitboxIndex]);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — RustyKeyWeapon 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 인덱스의 히트박스를 활성화한다.
        /// 이전 히트박스는 자동으로 비활성화.
        ///
        /// [호출 타이밍]
        ///   공격 애니메이션 이벤트 or 코루틴에서 타격 프레임 시작 시.
        /// </summary>
        /// <param name="hitboxIndex">활성화할 히트박스 인덱스 (상수 사용 권장)</param>
        /// <param name="damageInfo">이 히트에서 전달할 데미지 정보</param>
        public void EnableHitbox(int hitboxIndex, DamageInfo damageInfo)
        {
            if (!IsValidIndex(hitboxIndex)) return;

            // 이전 히트박스 비활성 + 중복 감지 대상 초기화
            DisableAllHitboxes();

            _activeHitboxIndex = hitboxIndex;
            _currentDamageInfo = damageInfo;
            _hitboxes[hitboxIndex].enabled = true;
        }

        /// <summary>
        /// 모든 히트박스를 비활성화하고 히트 대상 목록을 초기화한다.
        /// 공격 모션 종료 시 호출.
        /// </summary>
        public void DisableAllHitboxes()
        {
            if (_hitboxes == null) return;

            foreach (var hb in _hitboxes)
                if (hb != null) hb.enabled = false;

            _activeHitboxIndex = -1;
            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 히트 감지 — 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// OverlapCollider 로 히트박스와 겹치는 IDamageable 을 감지하고
        /// 아직 맞지 않은 대상에게 OnHit 이벤트 발행.
        /// </summary>
        /// <param name="hitbox">현재 활성화된 히트박스</param>
        private void CheckHit(Collider2D hitbox)
        {
            _overlapBuffer.Clear();

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_hitLayer);
            filter.useTriggers = true;

            int count = hitbox.Overlap(filter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];

                // 이미 이 타격에서 맞은 대상 스킵 (중복 피격 방지)
                if (_hitTargets.Contains(col)) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable))
                {
                    _hitTargets.Add(col);
                    damageable.TakeDamage(_currentDamageInfo);
                    OnHit?.Invoke(damageable, _currentDamageInfo);
                }
            }
        }

        /// <summary>
        /// 히트박스 인덱스 유효성 검사.
        /// </summary>
        private bool IsValidIndex(int index)
        {
            if (_hitboxes == null || index < 0 || index >= _hitboxes.Length)
            {
                Debug.LogWarning($"[PlayerWeaponHitboxManager] 유효하지 않은 히트박스 인덱스: {index}");
                return false;
            }
            return true;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_hitboxes == null) return;

            // 비활성 히트박스: 반투명 회색
            // 활성 히트박스: 빨강
            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] == null) continue;
                Gizmos.color = (i == _activeHitboxIndex)
                    ? new Color(1f, 0.2f, 0.2f, 0.6f)
                    : new Color(0.5f, 0.5f, 0.5f, 0.2f);

                if (_hitboxes[i] is BoxCollider2D box)
                {
                    Gizmos.DrawCube(
                        (Vector2)box.transform.position + box.offset,
                        box.size);
                }
            }
        }
    }
}