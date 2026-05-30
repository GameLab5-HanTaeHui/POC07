// ============================================================
// EnemyKnightMeleeAttack.cs  v1.0
// 기사형 근접 1타 공격 — Dash 봉인 시 대체 공격
//
// [역할]
//   EnemyKnightChargeAttack (차징 돌진) 이 Dash 봉인으로 차단됐을 때
//   대체 공격 수단으로 사용되는 근접 1타 공격.
//
// [조건]
//   EnemyAI.OnEnterAttack() 에서
//   Dash 봉인 활성 + attackRange 안에 플레이어 → 이 공격 실행.
//   Dash 봉인 없으면 이 공격은 절대 실행되지 않음.
//
// [공격 방식]
//   별도 히트박스 오브젝트 없이 OverlapCircle 로 판정.
//   기사 위치 기준 전방 attackRange 반경 내 플레이어 감지.
//   1타 즉발 — 히트박스 활성/비활성 없이 단발 감지.
//
// [EnemyDataSO 필드]
//   meleeAttackDamage  : 근접 1타 피해량
//   meleeAttackCooldown: 쿨타임 (초)
//   meleeAttackRange   : 공격 사정거리 (units)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System.Collections;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 기사형 근접 1타 공격. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [사용 조건]
    ///   Dash 봉인 활성 중에만 EnemyAI 가 호출.
    ///   Attack 봉인이 걸리면 이 공격도 차단됨 (EnemyAI 단에서 처리).
    ///
    /// [Prefab 설정]
    ///   Enemy_Knight 에 컴포넌트만 추가.
    ///   별도 자식 오브젝트 불필요.
    ///   _data = EnemyAI.Start() 에서 SetData() 로 주입.
    ///
    /// [AttackRange 시각화]
    ///   OnDrawGizmosSelected() 에서 반경 표시.
    ///   Scene 뷰에서 공격 범위 확인 가능.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class EnemyKnightAttack : EnemyAttackBase
    {
        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        private EnemyDataSO _data;
        private EnemyAI _enemyAI;

        // ──────────────────────────────────────────
        // GC 방지 버퍼
        // ──────────────────────────────────────────

        private readonly System.Collections.Generic.List<Collider2D> _overlapBuffer
            = new System.Collections.Generic.List<Collider2D>();
        private readonly System.Collections.Generic.HashSet<Collider2D> _hitTargets
            = new System.Collections.Generic.HashSet<Collider2D>();

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _enemyAI = GetComponent<EnemyAI>();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// DataSO 주입. EnemyAI.Start() 에서 호출.
        /// </summary>
        public void SetData(EnemyDataSO data) => _data = data;

        /// <summary>
        /// 근접 공격 쿨타임 프로퍼티.
        /// EnemyAI.OnEnterAttack() 에서 TryAttack(MeleeCooldown) 으로 사용.
        /// </summary>
        public float MeleeCooldown => _data != null ? _data.meleeAttackCooldown : 2.0f;

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 근접 1타 공격 코루틴.
        ///
        /// [흐름]
        ///   ① 짧은 선딜레이 (모션 느낌)
        ///   ② OverlapCircle 로 전방 플레이어 감지
        ///   ③ IDamageable.TakeDamage() 호출
        ///   ④ 짧은 후딜레이
        /// </summary>
        protected override IEnumerator ExecuteAttack()
        {
            if (_data == null) yield break;

            _hitTargets.Clear();

            float facingDir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;

            // ① 선딜레이 — 공격 모션 느낌
            yield return new WaitForSeconds(0.2f);

            // ② 전방 반구 내 플레이어 감지
            //    OverlapCircle 으로 attackHitLayer 감지
            _overlapBuffer.Clear();

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_data.attackHitLayer);
            filter.useTriggers = true;

            // 공격 판정 위치 — 기사 위치에서 전방으로 반구 범위
            Vector2 attackOrigin = (Vector2)transform.position
                + new Vector2(facingDir * (_data.meleeAttackRange * 0.5f), 0f);

            int count = Physics2D.OverlapCircle(
                attackOrigin,
                _data.meleeAttackRange * 0.5f,
                filter,
                _overlapBuffer);

            Debug.Log($"[KnightMelee] 공격 판정 — 범위:{_data.meleeAttackRange:F1} 감지:{count}");

            // ③ 피격 처리
            foreach (var col in _overlapBuffer)
            {
                if (_hitTargets.Contains(col)) continue;

                if (col.TryGetComponent<IDamageable>(out var dmg))
                {
                    _hitTargets.Add(col);
                    dmg.TakeDamage(new DamageInfo(
                        transform.position,
                        _data.meleeAttackDamage,
                        new Vector2(facingDir, -0.2f).normalized,
                        AttackType.Combo1));

                    Debug.Log($"[KnightMelee] 플레이어 피격: {_data.meleeAttackDamage}");
                }
            }

            // ④ 후딜레이
            yield return new WaitForSeconds(0.3f);

            _hitTargets.Clear();
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (_data == null) return;

            float dir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
            Vector3 origin = transform.position
                + new Vector3(dir * (_data.meleeAttackRange * 0.5f), 0f, 0f);

            // 근접 공격 범위 — 초록 원
            Gizmos.color = new Color(0f, 1f, 0.3f, 0.4f);
            Gizmos.DrawWireSphere(origin, _data.meleeAttackRange * 0.5f);

#if UNITY_EDITOR
            UnityEditor.Handles.Label(
                origin + Vector3.up * 0.3f,
                $"근접 {_data.meleeAttackDamage:F0}dmg");
#endif
        }
    }
}