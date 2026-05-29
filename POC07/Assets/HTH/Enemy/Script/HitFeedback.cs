// ============================================================
// HitFeedback.cs  v1.0
// 피격 피드백 통합 유틸리티 — DOTween 기반
//
// [역할]
//   게임 내 4가지 피격 상황에 대한 시각적 피드백을 담당.
//   static 메서드로 제공 — 어느 컴포넌트에서든 직접 호출 가능.
//
// [피드백 종류]
//   ① EnemyHitPlayer    : 적 → 플레이어 피격
//       빨간 플래시 + DOShakePosition (화면 흔들림 느낌)
//
//   ② PlayerHitLock     : 플레이어 → 자물쇠 피격
//       노란 펀치 스케일 + 색상 플래시
//
//   ③ PlayerHitEnemy    : 플레이어 → 적 본체 피격
//       흰→빨 플래시 + DOPunchPosition (뒤로 밀리는 느낌)
//
//   ④ PlayerAttackBlocked : 플레이어 공격 방패에 막힘
//       파란 플래시 + 반발 DOPunchPosition (공격자 방향 반대로)
//       + DOShakePosition (방패 자체 흔들림)
//
// [사용법]
//   HitFeedback.EnemyHitPlayer(spriteRenderer, transform);
//   HitFeedback.PlayerHitLock(lockSpriteRenderer, lockTransform, progress);
//   HitFeedback.PlayerHitEnemy(spriteRenderer, transform);
//   HitFeedback.PlayerAttackBlocked(shieldTransform, attackerTransform);
//
// [의존]
//   DOTween (DOTweenPro 불필요, DOTween 기본으로 동작)
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 피격 피드백 통합 유틸리티. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [설계 원칙]
    ///   - static 메서드 — MonoBehaviour 불필요, 어디서든 호출
    ///   - DOTween Kill(target) 으로 중복 실행 방지
    ///   - 색상은 OnComplete 에서 반드시 원복
    ///   - 피드백은 짧고 임팩트 있게 (0.1~0.3초)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public static class HitFeedback
    {
        // ══════════════════════════════════════════════════════
        // ① 적 → 플레이어 피격
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 적이 플레이어를 공격했을 때 피격 피드백.
        ///
        /// [효과]
        ///   - 스프라이트 빨간 플래시 (0.08초 × iFrame 횟수 — PlayerHealth 에서 직접 처리)
        ///   - Transform DOPunchPosition — 피격 방향으로 짧게 밀리는 느낌
        ///   - Transform DOShakeScale — 임팩트 크기 떨림
        ///
        /// [호출 위치]
        ///   PlayerHealth.TakeDamage() 내부에서 호출.
        /// </summary>
        /// <param name="sr">플레이어 SpriteRenderer</param>
        /// <param name="transform">플레이어 Transform</param>
        /// <param name="hitDirection">피격 방향 (공격자 → 피격자)</param>
        public static void EnemyHitPlayer(SpriteRenderer sr, Transform transform, Vector2 hitDirection)
        {
            if (sr == null || transform == null) return;

            // 기존 진행 중인 피드백 Kill
            DOTween.Kill(sr);
            DOTween.Kill(transform);

            // 스프라이트 색상 — 빨간 플래시 후 원복
            sr.DOColor(new Color(1f, 0.2f, 0.2f, 1f), 0.05f)
              .SetEase(Ease.OutQuart)
              .OnComplete(() =>
                  sr.DOColor(Color.white, 0.15f)
                    .SetEase(Ease.InQuart));

            // 피격 방향으로 짧은 밀림 (느낌만)
            Vector3 punchDir = new Vector3(hitDirection.x * 0.12f, 0.06f, 0f);
            transform.DOPunchPosition(punchDir, 0.25f, vibrato: 2, elasticity: 0.3f)
                     .SetEase(Ease.OutQuart);

            // 임팩트 스케일 떨림
            transform.DOPunchScale(Vector3.one * 0.08f, 0.2f, vibrato: 3, elasticity: 0.4f);
        }

        // ══════════════════════════════════════════════════════
        // ② 플레이어 → 자물쇠 피격
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어가 LockComponent 를 공격했을 때 피격 피드백.
        ///
        /// [효과]
        ///   - 노란 → 원래 색상 플래시 (피격 횟수 진행에 따라 강도 증가)
        ///   - DOPunchScale — 타격감 임팩트
        ///   - 해제 직전(progress >= 0.8) : 더 강한 붉은 플래시 + 큰 떨림
        ///
        /// [호출 위치]
        ///   LockComponent.TakeDamage() 내부에서 호출.
        /// </summary>
        /// <param name="sr">자물쇠 SpriteRenderer</param>
        /// <param name="transform">자물쇠 Transform</param>
        /// <param name="progress">해제 진행률 0~1 (UnlockProgress)</param>
        /// <param name="originalColor">평상시 자물쇠 색상 (Lerp 기준값)</param>
        public static void PlayerHitLock(
            SpriteRenderer sr,
            Transform transform,
            float progress,
            Color originalColor)
        {
            if (sr == null || transform == null) return;

            DOTween.Kill(sr);
            DOTween.Kill(transform);

            // 진행도에 따라 플래시 색상 변화
            // 초반: 노란 임팩트 / 후반(>=0.8): 붉은 경고
            Color flashColor = progress < 0.8f
                ? new Color(1f, 0.95f, 0.2f, 1f)   // 노랑
                : new Color(1f, 0.3f, 0.1f, 1f);    // 빨강 (해제 임박)

            // 순간 플래시 → 원래 색상 복귀
            sr.DOColor(flashColor, 0.04f)
              .SetEase(Ease.OutQuart)
              .OnComplete(() =>
                  sr.DOColor(originalColor, 0.12f)
                    .SetEase(Ease.InQuart));

            // 타격 임팩트 스케일
            float punchStr = Mathf.Lerp(0.1f, 0.25f, progress); // 진행할수록 더 크게
            transform.DOPunchScale(Vector3.one * punchStr, 0.2f, vibrato: 4, elasticity: 0.5f);

            // 해제 임박 시 위치 떨림 추가
            if (progress >= 0.8f)
            {
                transform.DOPunchPosition(
                    new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.04f, 0.04f), 0f),
                    0.18f, vibrato: 5, elasticity: 0.3f);
            }
        }

        // ══════════════════════════════════════════════════════
        // ③ 플레이어 → 적 본체 피격
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어가 적 본체를 공격했을 때 피격 피드백.
        ///
        /// [효과]
        ///   - 흰→빨 플래시 (EnemyBase 기존 코루틴 대체)
        ///   - DOPunchPosition — 피격 방향 반대로 밀리는 느낌
        ///   - DOPunchScale — 임팩트 크기 떨림
        ///
        /// [호출 위치]
        ///   EnemyBase.TakeDamage() 내부 HitFlashRoutine 대신 호출.
        ///   (EnemyBase.HitFlashRoutine 을 이 메서드로 교체)
        /// </summary>
        /// <param name="sr">적 SpriteRenderer</param>
        /// <param name="transform">적 Transform</param>
        /// <param name="hitDirection">피격 방향 (공격자 → 피격자)</param>
        public static void PlayerHitEnemy(SpriteRenderer sr, Transform transform, Vector2 hitDirection)
        {
            if (sr == null || transform == null) return;

            DOTween.Kill(sr);
            DOTween.Kill(transform);

            // 흰→빨 플래시 → 원복
            sr.DOColor(Color.white, 0f); // 즉시 흰색 리셋
            sr.DOColor(new Color(1f, 0.15f, 0.15f, 1f), 0.04f)
              .SetEase(Ease.OutQuart)
              .OnComplete(() =>
                  sr.DOColor(Color.white, 0.18f)
                    .SetEase(Ease.InQuart));

            // 피격 방향 반대로 밀림 (맞은 방향으로 뒤로 밀리는 느낌)
            Vector3 pushDir = new Vector3(-hitDirection.x * 0.1f, 0.05f, 0f);
            transform.DOPunchPosition(pushDir, 0.22f, vibrato: 2, elasticity: 0.25f)
                     .SetEase(Ease.OutQuart);

            // 임팩트 스케일
            transform.DOPunchScale(Vector3.one * 0.07f, 0.18f, vibrato: 3, elasticity: 0.4f);
        }

        // ══════════════════════════════════════════════════════
        // ④ 플레이어 공격 막힘 (방패)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 공격이 방패에 막혔을 때 피격 피드백.
        ///
        /// [효과]
        ///   방패 오브젝트:
        ///     - 파란 플래시 → 원복 (막힘 표시)
        ///     - DOShakePosition — 방패 흔들림
        ///   공격자(플레이어 무기):
        ///     - DOPunchPosition — 공격 방향 반대로 튕김 (반발감)
        ///
        /// [호출 위치]
        ///   PlayerWeaponHitboxManager.CheckHit() 에서
        ///   EnemyShield 레이어 감지 시 호출.
        ///   (현재는 continue 처리 → 이 메서드 추가)
        /// </summary>
        /// <param name="shieldSr">방패 SpriteRenderer (없으면 null 가능)</param>
        /// <param name="shieldTransform">방패 Transform</param>
        /// <param name="weaponTransform">플레이어 무기 Transform (반발 피드백)</param>
        /// <param name="attackDirection">공격 방향</param>
        public static void PlayerAttackBlocked(
            SpriteRenderer shieldSr,
            Transform shieldTransform,
            Transform weaponTransform,
            Vector2 attackDirection)
        {
            // ── 방패 피드백 ──────────────────────
            if (shieldTransform != null)
            {
                DOTween.Kill(shieldTransform);

                // 방패 흔들림 — 막힌 충격
                shieldTransform.DOShakePosition(
                    duration: 0.15f,
                    strength: new Vector3(0.06f, 0.04f, 0f),
                    vibrato: 8,
                    randomness: 60f);
            }

            if (shieldSr != null)
            {
                DOTween.Kill(shieldSr);

                // 파란 플래시 → 원복
                shieldSr.DOColor(new Color(0.4f, 0.6f, 1f, 1f), 0.04f)
                        .SetEase(Ease.OutQuart)
                        .OnComplete(() =>
                            shieldSr.DOColor(Color.white, 0.12f)
                                    .SetEase(Ease.InQuart));
            }

            // ── 무기 반발 피드백 ──────────────────────
            if (weaponTransform != null)
            {
                DOTween.Kill(weaponTransform);

                // 공격 방향 반대로 튕김
                Vector3 rebound = new Vector3(-attackDirection.x * 0.15f, 0.05f, 0f);
                weaponTransform.DOPunchPosition(rebound, 0.2f, vibrato: 3, elasticity: 0.5f)
                               .SetEase(Ease.OutQuart);
            }
        }
    }
}