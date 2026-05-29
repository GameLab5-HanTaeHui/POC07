// ============================================================
// EnemyKnightAttack.cs  v1.3
// 기사형 근접 내려치기 — attackHitLayer 적용 + GC 방지
//
// [v1.2 변경]
//   ① attackHitLayer 사용
//       기존: _data.playerLayer (EnemySensor 탐지용 레이어)
//       변경: _data.attackHitLayer (공격 히트박스 전용 레이어)
//       → 역할 분리. playerLayer 는 EnemySensor 에서만 사용.
//       → Physics 2D Matrix: EnemyAttackHit ↔ Player 충돌 ON 필요.
//
//   ② _overlapBuffer 필드화 (GC 방지)
//       기존: 매 CheckHit() 호출 시 new List<Collider2D>() 생성.
//       변경: 필드로 선언하고 Clear() 후 재사용.
//
// [v1.1 변경]
//   KnightDataSO → EnemyDataSO 참조로 교체.
//
// [역할]
//   근접 내려치기 단타.
//   히트박스 활성 → attackDuration → 비활성.
//   EnemyAI 가 Attack 상태 진입 시 TryAttack() 호출.
//
// [피격 연결 경로]
//   CheckHit() → ContactFilter2D(attackHitLayer)
//     → PlayerHealth.TakeDamage(info)
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
    /// 기사형 근접 내려치기 공격. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [공격 흐름]
    ///   TryAttack() → ExecuteAttack()
    ///     히트박스 ON → attackDuration 동안 CheckHit() 매 프레임
    ///     → 히트박스 OFF → OnAttackFinished 발행 → EnemyAI Chase 복귀
    ///
    /// [레이어 설정 필수]
    ///   AttackHitbox Layer    = EnemyAttackHit
    ///   EnemyDataSO.attackHitLayer = Player 레이어
    ///   Physics 2D Matrix: EnemyAttackHit ↔ Player = ON
    /// ────────────────────────────────────────────────────
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
        /// 미연결 시 Awake 에서 자동 탐색.
        /// </summary>
        [Tooltip("공격 히트박스. AttackHitbox 자식 BoxCollider2D 연결.")]
        [SerializeField] private Collider2D _hitbox;

        // ──────────────────────────────────────────
        // 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 적 수치 SO. EnemyAI.Start() 에서 SetData() 로 주입.
        /// </summary>
        private EnemyDataSO _data;

        /// <summary>
        /// FacingDirection 읽기용.
        /// </summary>
        private EnemyAI _enemyAI;

        // ──────────────────────────────────────────
        // GC 방지 버퍼
        // ──────────────────────────────────────────

        /// <summary>
        /// OverlapCollider 결과 재사용 버퍼.
        /// 매 CheckHit() 에서 Clear() 후 재사용 → GC 할당 방지.
        /// </summary>
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>();

        /// <summary>
        /// 현재 공격에서 이미 피격된 콜라이더.
        /// 같은 공격에서 중복 피격 방지.
        /// ExecuteAttack 시작/종료 시 Clear().
        /// </summary>
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
        /// EnemyDataSO 주입. EnemyAI.Start() 에서 호출.
        /// </summary>
        public void SetData(EnemyDataSO data) => _data = data;

        // ══════════════════════════════════════════════════════
        // EnemyAttackBase 구현
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 근접 내려치기 실행 코루틴.
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

        /// <summary>
        /// 히트박스와 겹치는 Player 레이어 콜라이더 감지.
        /// 중복 피격 방지 후 PlayerHealth.TakeDamage() 호출.
        ///
        /// [레이어 변경 — v1.2]
        ///   _data.playerLayer → _data.attackHitLayer
        ///   공격 히트박스 감지는 attackHitLayer 전용.
        ///   playerLayer 는 EnemySensor 탐지에서만 사용.
        /// </summary>
        private void CheckHit()
        {
            _overlapBuffer.Clear();

            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(_data.attackHitLayer);
            filter.useTriggers = true;

            _hitbox.Overlap(filter, _overlapBuffer);

            foreach (var col in _overlapBuffer)
            {
                if (_hitTargets.Contains(col)) continue;

                if (col.TryGetComponent<IDamageable>(out var damageable))
                {
                    _hitTargets.Add(col);

                    float dir = _enemyAI != null ? _enemyAI.FacingDirection : 1f;
                    var info = new DamageInfo(
                        attackerPosition: transform.position,
                        amount: _data.attackDamage,
                        direction: new Vector2(dir, -0.2f).normalized,
                        attackType: AttackType.Combo1
                    );

                    damageable.TakeDamage(info);
                    Debug.Log($"[KnightAttack] 플레이어 피격: {_data.attackDamage}");
                }
            }
        }
    }
}