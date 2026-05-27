// ============================================================
// KnightAttack.cs  v1.1
// 기사형 공격 — EnemyAttackBase 상속
//
// [v1.1 변경]
//   KnightDataSO → EnemyDataSO 참조로 교체.
//   EnemyAI.FacingDirection 참조로 공격 방향 결정.
//
// [역할]
//   근접 내려치기 단타.
//   히트박스 활성 → attackDuration → 비활성.
//   EnemyAI 가 Attack 상태 진입 시 TryAttack() 호출.
//
// [Hierarchy]
//   Enemy_Knight
//   ├── [KnightAttack]
//   └── AttackHitbox
//         └── [BoxCollider2D] isTrigger=ON
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 근접 내려치기 공격. (v1.1)
    /// </summary>
    public class EnemyKnightAttack : EnemyAttackBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 히트박스 ──────────────────────")]

        /// <summary>
        /// 공격 히트박스 Collider2D.
        /// 자식 오브젝트 AttackHitbox 의 BoxCollider2D 연결.
        /// </summary>
        [Tooltip("공격 히트박스. AttackHitbox 자식 BoxCollider2D 연결.")]
        [SerializeField] private Collider2D _hitbox;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 적 수치 SO. EnemyAI 와 공유하는 동일 에셋.
        /// SetData() 로 주입.
        /// </summary>
        private EnemyDataSO _data;

        /// <summary>
        /// FacingDirection 읽기용.
        /// </summary>
        private EnemyAI _enemyAI;

        // ──────────────────────────────────────────
        // 중복 피격 방지
        // ──────────────────────────────────────────

        private readonly HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();

            if (_hitbox == null)
                _hitbox = GetComponentInChildren<Collider2D>();

            if (_hitbox != null)
                _hitbox.enabled = false;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// EnemyDataSO 주입. EnemyAI.Start() 직후 호출.
        /// </summary>
        public void SetData(EnemyDataSO data) => _data = data;

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 근접 내려치기 실행.
        /// 히트박스 활성 → attackDuration → 비활성.
        /// </summary>
        protected override IEnumerator ExecuteAttack()
        {
            if (_data == null || _hitbox == null) yield break;

            _hitTargets.Clear();
            _hitbox.enabled = true;

            float elapsed = 0f;
            while (elapsed < _data.attackDuration)
            {
                CheckHit();
                yield return null;
                elapsed += Time.deltaTime;
            }
            _hitbox.enabled = false;
            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 히트 감지
        // ══════════════════════════════════════════════════════

        private void CheckHit()
        {
            var buffer = new List<Collider2D>();

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_data.playerLayer);
            filter.useTriggers = true;

            _hitbox.Overlap(filter, buffer);

            foreach (var col in buffer)
            {
                if (_hitTargets.Contains(col)) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable))
                {
                    _hitTargets.Add(col);

                    float dir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
                    var info = new DamageInfo(
                        attackerPosition: transform.position,
                        amount: _data.attackDamage,
                        direction: new Vector2(dir, -0.3f).normalized,
                        attackType: AttackType.Combo1
                    );

                    damageable.TakeDamage(info);
                    Debug.Log($"[KnightAttack] 플레이어 피격: {_data.attackDamage}");
                }
            }
        }
    }
}