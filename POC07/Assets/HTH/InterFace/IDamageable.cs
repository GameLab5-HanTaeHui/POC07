// ============================================================
// IDamageable.cs  v1.0
// 피격 처리 인터페이스
//
// [역할]
//   데미지를 받을 수 있는 모든 오브젝트(적, 자물쇠 등)가 구현하는 인터페이스.
//   PlayerWeaponHitboxManager 의 OnHit 이벤트에서 이 인터페이스를 통해 호출.
//
// [구현 대상]
//   - 일반 적 (EnemyBase)
//   - 자물쇠 (LockComponent) — 추후 구현
//
// [DamageInfo 구조체]
//   공격자 위치, 데미지량, 공격 방향, 공격 유형 등을 묶어서 전달.
//   나중에 자물쇠 해제 조건(방향, 공격 유형 등)을 여기에 추가.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    // ──────────────────────────────────────────
    // 공격 유형 열거형
    // ──────────────────────────────────────────

    /// <summary>
    /// 공격 유형.
    /// 자물쇠 해제 조건 판별에 사용 (추후 확장).
    ///
    /// [확장 예시]
    ///   Combo1, Combo2, Combo3 — 콤보 단계별 구분
    ///   AirAttack              — 공중 공격
    ///   추후 열쇠 종류별 유형 추가 가능
    /// </summary>
    public enum AttackType
    {
        /// <summary> 1단 콤보 공격. </summary>
        Combo1,

        /// <summary> 2단 콤보 공격. </summary>
        Combo2,

        /// <summary> 3단 콤보 피니셔. </summary>
        Combo3,

        /// <summary> 공중 수평 공격. (기존 AirAttack — 하위 호환 유지) </summary>
        AirAttack,

        /// <summary> 공중 하향 내리찍기. ↓ + 좌우 입력. </summary>
        AirAttackDown,

        /// <summary> 공중 상향 공격. ↑ + 좌우 입력. </summary>
        AirAttackUp,
    }


    // ──────────────────────────────────────────
    // 데미지 정보 구조체
    // ──────────────────────────────────────────

    /// <summary>
    /// 타격 시 전달되는 데미지 정보 묶음.
    ///
    /// [사용처]
    ///   PlayerWeaponHitboxManager.OnHit 이벤트 → IDamageable.TakeDamage(info)
    ///
    /// [자물쇠 해제 연동 예시 (추후)]
    ///   LockComponent.TakeDamage(info) 내부에서
    ///   info.AttackType == AttackType.AirAttack 등으로 해제 조건 판별.
    /// </summary>
    public struct DamageInfo
    {
        /// <summary> 공격자(플레이어) 월드 위치. 넉백 방향 계산에 사용. </summary>
        public Vector2 AttackerPosition;

        /// <summary> 데미지량. </summary>
        public float Amount;

        /// <summary> 공격 방향 (정규화된 벡터). </summary>
        public Vector2 Direction;

        /// <summary> 공격 유형. 자물쇠 해제 조건 판별용. </summary>
        public AttackType AttackType;

        /// <summary>
        /// DamageInfo 생성자.
        /// </summary>
        /// <param name="attackerPosition">공격자 위치</param>
        /// <param name="amount">데미지량</param>
        /// <param name="direction">공격 방향 (정규화)</param>
        /// <param name="attackType">공격 유형</param>
        public DamageInfo(Vector2 attackerPosition, float amount, Vector2 direction, AttackType attackType)
        {
            AttackerPosition = attackerPosition;
            Amount = amount;
            Direction = direction;
            AttackType = attackType;
        }
    }

    // ──────────────────────────────────────────
    // IDamageable 인터페이스
    // ──────────────────────────────────────────

    /// <summary>
    /// 데미지를 받을 수 있는 오브젝트가 구현하는 인터페이스.
    ///
    /// [구현 예시]
    /// <code>
    ///   public class EnemyBase : MonoBehaviour, IDamageable
    ///   {
    ///       public void TakeDamage(DamageInfo info)
    ///       {
    ///           _hp -= info.Amount;
    ///           ApplyKnockback(info.Direction);
    ///       }
    ///
    ///       public bool IsDead => _hp <= 0f;
    ///   }
    /// </code>
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 데미지를 받는다.
        /// </summary>
        /// <param name="info">공격자 위치, 데미지량, 방향, 유형을 담은 구조체.</param>
        void TakeDamage(DamageInfo info);

        /// <summary>
        /// 현재 사망(파괴) 여부.
        /// </summary>
        bool IsDead { get; }
    }
}