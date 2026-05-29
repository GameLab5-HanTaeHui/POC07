// ============================================================
// PlayerWeaponHitboxManager.cs  v1.3
// 플레이어 무기 히트박스 관리 컴포넌트
//
// [v1.2 변경 — Enemy + EnemyLock 레이어 분리 감지]
//
//   [배경]
//     _hitLayer 에 Enemy 레이어만 있으면 Lock(EnemyLock 레이어)을 감지 못함.
//     _hitLayer 에 EnemyLock 만 있으면 본체(Enemy 레이어)를 감지 못함.
//     두 레이어 모두 감지하되, 레이어에 따라 처리를 달리해야 함.
//
//   [처리 분기]
//     EnemyLock 레이어 감지
//       → LockComponent.TakeDamage() 직접 호출
//       → Lock 해제 조건 판단은 LockComponent 내부 처리
//
//     Enemy 레이어 감지
//       → IDamageable.TakeDamage() 호출
//       → EnemyKnight 내부에서 Lock 해제 여부 판단
//          (Lock 미해제 시 → 후면 공격으로 다시 LockComponent 전달)
//          (Lock 전부 해제 시 → EnemyBase 정상 피격)
//
//   [Inspector 변경]
//     기존: _hitLayer (Enemy 단일 레이어마스크)
//     변경: _enemyLayer  (Enemy 레이어)
//           _lockLayer   (EnemyLock 레이어)
//     두 레이어를 합산해서 OverlapCollider 에 사용.
//     충돌 후 레이어 비트 연산으로 분기.
//
// [v1.3 변경 — EnemyShield 레이어 명시적 무시]
//   EnemyShield 레이어 필드 추가 (_shieldLayer).
//   CheckHit() 에서 EnemyShield 레이어 감지 시 즉시 continue.
//   방패(ShieldCollider)가 플레이어 무기 히트박스 감지 마스크에 포함되더라도
//   코드 레벨에서 명시적으로 무시.
//
//   [방패 차단 동작 원리]
//     ShieldCollider.isTrigger = OFF → 물리 충돌로 플레이어가 방패를 통과 못 함.
//     플레이어 히트박스(isTrigger=ON)와 ShieldCollider(isTrigger=OFF)는
//     OnTriggerEnter 가 발생하지 않음. (둘 다 isTrigger=OFF 여야 물리 충돌)
//     그러나 Overlap() 은 isTrigger 무관하게 모든 콜라이더를 감지할 수 있음.
//     → _shieldLayer 를 명시적으로 무시해서 확실하게 차단.
//
// [v1.1 변경]
//   히트박스 좌우 반전 처리 (OnFlipped 구독).
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
    /// 플레이어 무기 히트박스 관리 컴포넌트. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [감지 흐름 — v1.3]
    ///   OverlapCollider(_enemyLayer | _lockLayer)
    ///     → 감지된 콜라이더의 Layer 확인
    ///       EnemyShield 레이어 → 무시 (방패 차단)
    ///       EnemyLock 레이어   → LockComponent.TakeDamage()
    ///       Enemy 레이어       → IDamageable.TakeDamage()
    ///                             (EnemyKnight 내부에서 Lock 해제 여부 판단)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerWeaponHitboxManager : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // 히트박스 인덱스 상수
        // ──────────────────────────────────────────

        public const int HitboxCombo1 = 0;
        public const int HitboxCombo2 = 1;
        public const int HitboxCombo3 = 2;
        public const int HitboxAirAttack = 3;

        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트박스 콜라이더 연결 ──────────────────────")]

        [Tooltip("콤보별 히트박스. 인덱스 0=Combo1, 1=Combo2, 2=Combo3, 3=AirAttack.")]
        [SerializeField] private Collider2D[] _hitboxes;

        [Header("── 감지 레이어 설정 ──────────────────────")]

        /// <summary>
        /// 적 본체 레이어마스크.
        /// Enemy 레이어 선택.
        /// 감지 시 IDamageable.TakeDamage() 호출.
        /// EnemyKnight 내부에서 Lock 해제 여부 추가 판단.
        /// </summary>
        [Tooltip("적 본체 레이어. Enemy 레이어 선택.")]
        [SerializeField] private LayerMask _enemyLayer;

        /// <summary>
        /// 자물쇠 전용 레이어마스크.
        /// EnemyLock 레이어 선택.
        /// 감지 시 LockComponent.TakeDamage() 직접 호출.
        /// Lock 해제 조건 판단은 LockComponent 내부 처리.
        /// </summary>
        [Tooltip("자물쇠 레이어. EnemyLock 레이어 선택.")]
        [SerializeField] private LayerMask _lockLayer;

        /// <summary>
        /// 방패 레이어마스크. (v1.3 추가)
        /// EnemyShield 레이어 선택.
        /// 감지 시 아무것도 하지 않음 — 방패 차단.
        ///
        /// [왜 별도 필드인가?]
        ///   Overlap() 은 useTriggers=true 설정 시 isTrigger=OFF 콜라이더도
        ///   일부 상황에서 감지할 수 있음. 명시적으로 무시해서 확실하게 차단.
        ///   _shieldLayer 를 combinedMask 에서 제외하는 방법도 있지만,
        ///   명시적 continue 분기가 의도를 더 명확하게 표현.
        /// </summary>
        [Tooltip("방패 레이어. EnemyShield 레이어 선택. 감지 시 무시.")]
        [SerializeField] private LayerMask _shieldLayer;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private int _activeHitboxIndex = -1;
        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();
        private DamageInfo _currentDamageInfo;
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary> IDamageable 에 히트 발생 시 발행. </summary>
        public event Action<IDamageable, DamageInfo> OnHit;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            if (_hitboxes == null || _hitboxes.Length == 0)
            {
                Debug.LogWarning("[PlayerWeaponHitboxManager] 히트박스가 연결되지 않았습니다.");
                return;
            }

            DisableAllHitboxes();
        }

        private void Update()
        {
            if (_activeHitboxIndex < 0) return;
            CheckHit(_hitboxes[_activeHitboxIndex]);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        public void EnableHitbox(int hitboxIndex, DamageInfo damageInfo)
        {
            if (!IsValidIndex(hitboxIndex)) return;
            DisableAllHitboxes();
            _activeHitboxIndex = hitboxIndex;
            _currentDamageInfo = damageInfo;
            _hitboxes[hitboxIndex].enabled = true;
        }

        public void DisableAllHitboxes()
        {
            if (_hitboxes == null) return;
            foreach (var hb in _hitboxes)
                if (hb != null) hb.enabled = false;
            _activeHitboxIndex = -1;
            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 히트 감지 — v1.2 레이어 분기
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// OverlapCollider 로 Enemy + EnemyLock 레이어 모두 감지.
        /// 감지된 콜라이더의 레이어에 따라 처리 분기.
        ///
        /// [EnemyLock 레이어]
        ///   LockComponent 를 직접 찾아 TakeDamage() 호출.
        ///   자물쇠 해제 조건 판단은 LockComponent 내부에서 처리.
        ///
        /// [Enemy 레이어]
        ///   IDamageable.TakeDamage() 호출.
        ///   EnemyKnight 의 경우 내부에서 Lock 해제 여부를 판단:
        ///     Lock 미해제 + 정면 → 방패 무효
        ///     Lock 미해제 + 후면 → 자물쇠로 전달
        ///     Lock 전부 해제    → 본체 정상 피격
        /// </summary>
        private void CheckHit(Collider2D hitbox)
        {
            _overlapBuffer.Clear();

            // Enemy + EnemyLock 두 레이어 합산 감지
            LayerMask combinedMask = _enemyLayer | _lockLayer;

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(combinedMask);
            filter.useTriggers = true;

            hitbox.Overlap(filter, _overlapBuffer);

            for (int i = 0; i < _overlapBuffer.Count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (_hitTargets.Contains(col)) continue;

                int colLayer = col.gameObject.layer;

                // ── EnemyShield 레이어 → 방패 차단, 무시 ──────────────
                if ((_shieldLayer.value & (1 << colLayer)) != 0)
                {
                    Debug.Log($"[HitboxManager] 방패 감지 → 막힘 피드백");
                    // 방패 SpriteRenderer (없으면 null 전달)
                    var shieldSr = col.GetComponent<SpriteRenderer>();
                    // 공격 방향 — 현재 히트박스가 있는 방향 (Weapon localPosition.x 부호)
                    Vector2 attackDir = new Vector2(
                        hitbox.transform.position.x - col.transform.position.x, 0f).normalized;
                    HitFeedback.PlayerAttackBlocked(
                        shieldSr,
                        col.transform,           // 방패 Transform
                        hitbox.transform,        // 무기 히트박스 Transform
                        attackDir);
                    _hitTargets.Add(col);  // 이 프레임 중복 호출 방지
                    continue;
                }

                // ── EnemyLock 레이어 → LockComponent 직접 호출 ──────────
                if ((_lockLayer.value & (1 << colLayer)) != 0)
                {
                    LockComponent lock_ = col.GetComponent<LockComponent>();
                    if (lock_ != null && !lock_.IsUnlocked)
                    {
                        _hitTargets.Add(col);
                        lock_.TakeDamage(_currentDamageInfo);
                        OnHit?.Invoke(lock_, _currentDamageInfo);
                        Debug.Log($"[HitboxManager] 자물쇠 피격: {col.name}");
                    }
                    continue;
                }

                // ── Enemy 레이어 → IDamageable 호출 ──────────────────────
                if ((_enemyLayer.value & (1 << colLayer)) != 0)
                {
                    if (col.TryGetComponent<IDamageable>(out var damageable))
                    {
                        _hitTargets.Add(col);
                        damageable.TakeDamage(_currentDamageInfo);
                        OnHit?.Invoke(damageable, _currentDamageInfo);
                        Debug.Log($"[HitboxManager] 본체 피격 시도: {col.name}");
                    }
                }
            }
        }

        private bool IsValidIndex(int index)
        {
            if (_hitboxes == null || index < 0 || index >= _hitboxes.Length)
            {
                Debug.LogWarning($"[PlayerWeaponHitboxManager] 유효하지 않은 인덱스: {index}");
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
            for (int i = 0; i < _hitboxes.Length; i++)
            {
                if (_hitboxes[i] == null) continue;
                Gizmos.color = (i == _activeHitboxIndex)
                    ? new Color(1f, 0.2f, 0.2f, 0.6f)
                    : new Color(0.5f, 0.5f, 0.5f, 0.2f);
                if (_hitboxes[i] is BoxCollider2D box)
                    Gizmos.DrawCube(
                        (Vector2)box.transform.position + box.offset,
                        box.size);
            }
        }
    }
}