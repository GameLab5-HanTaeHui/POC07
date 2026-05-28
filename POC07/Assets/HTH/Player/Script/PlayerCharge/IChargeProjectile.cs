// ============================================================
// IChargeProjectile.cs  v1.0
// 차징 투사체 인터페이스
//
// [역할]
//   PlayerChargeAttack 이 투사체 구현체를 알지 못해도
//   Launch() 하나로 발사 가능하도록 추상화.
//   추후 다양한 투사체 구현체(열쇠형, 에너지형 등) 연결 가능.
//
// [구현 대상]
//   ChargeProjectile.cs (추후 구현)
//
// [사용 흐름]
//   PlayerChargeAttack.Fire()
//     → Instantiate(chargeProjectilePrefab)
//     → GetComponent<IChargeProjectile>().Launch(direction, power)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 차징 투사체 인터페이스. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [구현 시 필수]
    ///   Launch() : 발사 방향 + 차징 비율로 투사체 초기화 및 이동 시작.
    ///
    /// [충돌 처리 — 구현체 책임]
    ///   Enemy 레이어 → 자물쇠 기능 잠금 (LockComponent 연동)
    ///   Ground/Wall 레이어 → 즉시 소멸
    /// ────────────────────────────────────────────────────
    /// </summary>
    public interface IChargeProjectile
    {
        /// <summary>
        /// 투사체 발사.
        /// </summary>
        /// <param name="direction">발사 방향 (정규화된 벡터)</param>
        /// <param name="chargePower">
        /// 차징 비율 (0~1).
        /// 0 = 최소 차징 / 1 = 최대 차징.
        /// 투사체 속도, 크기, 효과 등에 활용.
        /// </param>
        void Launch(Vector2 direction, float chargePower);
    }
}