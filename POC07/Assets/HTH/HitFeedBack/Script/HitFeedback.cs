// ============================================================
// HitFeedback.cs  v2.0
// 피격 피드백 통합 유틸리티 — DOTween + 파티클 연동
//
// [v2.0 변경]
//   파티클 연동 추가.
//   HitFeedbackConfig SO 를 Init() 으로 주입.
//   각 피격 상황에 맞는 파티클을 위치/방향에 맞게 Instantiate.
//   파티클 Prefab 미연결 시 DOTween 만으로 동작 (하위 호환 유지).
//
//   SpawnParticle() 유틸리티 추가:
//     position / direction / scale 을 받아 Prefab Instantiate.
//     ParticleSystem 자동 Play 후 duration + maxLifetime 뒤 자동 Destroy.
//
// [피드백 종류]
//   ① EnemyHitPlayer    : 적 → 플레이어 피격
//       파티클: fxHitEnemy (흰+노랑 스파크)
//       DOTween: 빨간 플래시 + PunchPosition + PunchScale
//
//   ② PlayerHitLock     : 플레이어 → 자물쇠 피격
//       파티클: fxHitLock (파랑+흰) + progress 에 따라 크기 증가
//       DOTween: 노랑/빨강 플래시 + PunchScale
//
//   ③ LockUnlocked      : 자물쇠 해제 (신규)
//       파티클: fxUnlockLock (금색 폭발)
//       DOTween: PunchScale (기존 LockComponent 에서 이동)
//
//   ④ PlayerHitEnemy    : 플레이어 → 적 본체 피격
//       파티클: fxHitEnemy (흰+노랑 스파크, 피격 방향으로)
//       DOTween: 흰→빨 플래시 + PunchPosition + PunchScale
//
//   ⑤ PlayerAttackBlocked : 방패 막힘
//       파티클: fxBlockedShield (파란색)
//       DOTween: 방패 파랑 플래시 + ShakePosition + 무기 반발
//
//   ⑥ SealApplied       : 봉인 적용 (신규 — SealComponent 에서 호출)
//       파티클: fxSealApplied (파랑+보라 링)
//       DOTween: 파랑 플래시 + ShakeScale
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 피격 피드백 통합 유틸리티. (v2.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [초기화 방법]
    ///   HitFeedbackInitializer 컴포넌트를 씬에 배치하고
    ///   HitFeedbackConfig 에셋을 연결.
    ///   → 씬 시작 시 Init() 자동 호출.
    ///
    /// [파티클 없이 사용]
    ///   Config 미연결 시 DOTween 만으로 동작 (하위 호환).
    ///
    /// [사용법]
    ///   HitFeedback.EnemyHitPlayer(sr, transform, dir);
    ///   HitFeedback.PlayerHitLock(sr, transform, progress, color);
    ///   HitFeedback.LockUnlocked(sr, transform);
    ///   HitFeedback.PlayerHitEnemy(sr, transform, dir);
    ///   HitFeedback.PlayerAttackBlocked(shieldSr, shieldTr, weaponTr, dir);
    ///   HitFeedback.SealApplied(sr, transform);
    /// ────────────────────────────────────────────────────
    /// </summary>
    public static class HitFeedback
    {
        // ──────────────────────────────────────────
        // 파티클 Config
        // ──────────────────────────────────────────

        /// <summary>
        /// 파티클 프리팹 설정. HitFeedbackInitializer 에서 주입.
        /// null 이면 DOTween 만 실행.
        /// </summary>
        private static HitFeedbackConfig _config;

        /// <summary>
        /// 파티클 Config 주입.
        /// HitFeedbackInitializer.Awake() 에서 호출.
        /// </summary>
        public static void Init(HitFeedbackConfig config)
        {
            _config = config;
        }

        // ══════════════════════════════════════════════════════
        // ① 적 → 플레이어 피격
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 적이 플레이어를 공격했을 때 피격 피드백.
        ///
        /// [파티클] fxHitEnemy — 피격 위치에서 방사형 스파크
        /// [DOTween] 빨간 플래시 + PunchPosition + PunchScale
        /// </summary>
        public static void EnemyHitPlayer(
            SpriteRenderer sr,
            Transform transform,
            Vector2 hitDirection)
        {
            if (sr == null || transform == null) return;

            DOTween.Kill(sr);
            DOTween.Kill(transform);

            // 파티클
            SpawnParticle(
                _config?.fxHitEnemy,
                transform.position,
                hitDirection,
                scale: 1f);

            // DOTween — 빨간 플래시
            sr.DOColor(new Color(1f, 0.2f, 0.2f, 1f), 0.05f)
              .SetEase(Ease.OutQuart)
              .OnComplete(() =>
                  sr.DOColor(Color.white, 0.15f)
                    .SetEase(Ease.InQuart));

            // PunchPosition
            Vector3 punchDir = new Vector3(hitDirection.x * 0.12f, 0.06f, 0f);
            transform.DOPunchPosition(punchDir, 0.25f, vibrato: 2, elasticity: 0.3f)
                     .SetEase(Ease.OutQuart);

            // PunchScale
            transform.DOPunchScale(Vector3.one * 0.08f, 0.2f, vibrato: 3, elasticity: 0.4f);
        }

        // ══════════════════════════════════════════════════════
        // ② 플레이어 → 자물쇠 피격
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어가 자물쇠를 공격했을 때 피격 피드백.
        ///
        /// [파티클] fxHitLock — 진행도에 비례한 크기
        /// [DOTween] 노랑/빨강 플래시 + PunchScale (+ 임박 시 PunchPosition)
        /// </summary>
        public static void PlayerHitLock(
            SpriteRenderer sr,
            Transform transform,
            float progress,
            Color originalColor)
        {
            if (sr == null || transform == null) return;

            DOTween.Kill(sr);
            DOTween.Kill(transform);

            // 파티클 — 진행도에 비례한 크기
            float particleScale = Mathf.Lerp(0.6f, 1.4f, progress)
                                  * (_config?.lockHitScaleMultiplier ?? 1f);
            SpawnParticle(
                _config?.fxHitLock,
                transform.position,
                Vector2.up,
                scale: particleScale);

            // DOTween — 플래시 색상
            Color flashColor = progress < 0.8f
                ? new Color(1f, 0.95f, 0.2f, 1f)
                : new Color(1f, 0.3f, 0.1f, 1f);

            sr.DOColor(flashColor, 0.04f)
              .SetEase(Ease.OutQuart)
              .OnComplete(() =>
                  sr.DOColor(originalColor, 0.12f)
                    .SetEase(Ease.InQuart));

            // PunchScale
            float punchStr = Mathf.Lerp(0.1f, 0.25f, progress);
            transform.DOPunchScale(Vector3.one * punchStr, 0.2f, vibrato: 4, elasticity: 0.5f);

            // 해제 임박 PunchPosition
            if (progress >= 0.8f)
            {
                transform.DOPunchPosition(
                    new Vector3(
                        Random.Range(-0.05f, 0.05f),
                        Random.Range(-0.04f, 0.04f),
                        0f),
                    0.18f, vibrato: 5, elasticity: 0.3f);
            }
        }

        // ══════════════════════════════════════════════════════
        // ③ 자물쇠 해제 (신규)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 자물쇠 해제 완료 시 피드백. (v2.0 신규)
        /// 기존 LockComponent.Unlock() 내부 DOTween 을 이 메서드로 대체.
        ///
        /// [파티클] fxUnlockLock — 금색 폭발, 가장 큰 임팩트
        /// [DOTween] 금색 플래시 + 큰 PunchScale
        /// </summary>
        public static void LockUnlocked(
            SpriteRenderer sr,
            Transform transform)
        {
            if (transform == null) return;

            DOTween.Kill(transform);

            // 파티클 — 금색 폭발
            SpawnParticle(
                _config?.fxUnlockLock,
                transform.position,
                Vector2.up,
                scale: 1.5f);

            // DOTween — 금색 플래시
            if (sr != null)
            {
                DOTween.Kill(sr);
                sr.DOColor(new Color(1f, 0.85f, 0.1f, 1f), 0.05f)
                  .SetEase(Ease.OutQuart)
                  .OnComplete(() =>
                      sr.DOColor(Color.white, 0.25f)
                        .SetEase(Ease.InQuart));
            }

            // 큰 PunchScale — 해제 순간 임팩트
            transform.DOPunchScale(
                Vector3.one * 0.45f,
                duration: 0.35f,
                vibrato: 5,
                elasticity: 0.6f);
        }

        // ══════════════════════════════════════════════════════
        // ④ 플레이어 → 적 본체 피격
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어가 적 본체를 공격했을 때 피격 피드백.
        ///
        /// [파티클] fxHitEnemy — 피격 방향으로 스파크
        /// [DOTween] 흰→빨 플래시 + PunchPosition + PunchScale
        /// </summary>
        public static void PlayerHitEnemy(
            SpriteRenderer sr,
            Transform transform,
            Vector2 hitDirection)
        {
            if (sr == null || transform == null) return;

            DOTween.Kill(sr);
            DOTween.Kill(transform);

            // 파티클 — 피격 방향으로
            SpawnParticle(
                _config?.fxHitEnemy,
                transform.position,
                hitDirection,
                scale: 1.0f);

            // DOTween — 흰→빨 플래시
            sr.DOColor(Color.white, 0f);
            sr.DOColor(new Color(1f, 0.15f, 0.15f, 1f), 0.04f)
              .SetEase(Ease.OutQuart)
              .OnComplete(() =>
                  sr.DOColor(Color.white, 0.18f)
                    .SetEase(Ease.InQuart));

            // PunchPosition — 피격 방향 반대로 밀림
            Vector3 pushDir = new Vector3(-hitDirection.x * 0.1f, 0.05f, 0f);
            transform.DOPunchPosition(pushDir, 0.22f, vibrato: 2, elasticity: 0.25f)
                     .SetEase(Ease.OutQuart);

            // PunchScale
            transform.DOPunchScale(Vector3.one * 0.07f, 0.18f, vibrato: 3, elasticity: 0.4f);
        }

        // ══════════════════════════════════════════════════════
        // ⑤ 방패 막힘
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 플레이어 공격이 방패에 막혔을 때 피격 피드백.
        ///
        /// [파티클] fxBlockedShield — 방패 위치에서 파란 파티클
        /// [DOTween] 방패 파랑 플래시 + ShakePosition / 무기 반발 PunchPosition
        /// </summary>
        public static void PlayerAttackBlocked(
            SpriteRenderer shieldSr,
            Transform shieldTransform,
            Transform weaponTransform,
            Vector2 attackDirection)
        {
            // 파티클 — 방패 위치
            if (shieldTransform != null)
            {
                SpawnParticle(
                    _config?.fxBlockedShield,
                    shieldTransform.position,
                    -attackDirection,  // 튕겨나오는 방향
                    scale: 0.8f);
            }

            // 방패 DOTween
            if (shieldTransform != null)
            {
                DOTween.Kill(shieldTransform);
                shieldTransform.DOShakePosition(
                    duration: 0.15f,
                    strength: new Vector3(0.06f, 0.04f, 0f),
                    vibrato: 8,
                    randomness: 60f);
            }

            if (shieldSr != null)
            {
                DOTween.Kill(shieldSr);
                shieldSr.DOColor(new Color(0.4f, 0.6f, 1f, 1f), 0.04f)
                        .SetEase(Ease.OutQuart)
                        .OnComplete(() =>
                            shieldSr.DOColor(Color.white, 0.12f)
                                    .SetEase(Ease.InQuart));
            }

            // 무기 반발
            if (weaponTransform != null)
            {
                DOTween.Kill(weaponTransform);
                Vector3 rebound = new Vector3(-attackDirection.x * 0.15f, 0.05f, 0f);
                weaponTransform.DOPunchPosition(rebound, 0.2f, vibrato: 3, elasticity: 0.5f)
                               .SetEase(Ease.OutQuart);
            }
        }

        // ══════════════════════════════════════════════════════
        // ⑥ 봉인 적용 (신규)
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// SealProjectile 이 적에 명중하여 봉인이 적용됐을 때 피드백. (v2.0 신규)
        /// SealComponent.ApplySeal() 에서 호출.
        ///
        /// [파티클] fxSealApplied — 파랑+보라 링 이펙트 (원형 방출)
        /// [DOTween] 파랑 플래시 + ShakeScale
        /// </summary>
        public static void SealApplied(
            SpriteRenderer sr,
            Transform transform)
        {
            if (transform == null) return;

            // 파티클 — 원형 링
            SpawnParticle(
                _config?.fxSealApplied,
                transform.position,
                Vector2.up,
                scale: 1.2f);

            // DOTween — 파랑 플래시 + ShakeScale
            if (sr != null)
            {
                DOTween.Kill(sr);
                sr.DOColor(new Color(0.3f, 0.5f, 1f, 1f), 0.06f)
                  .SetEase(Ease.OutQuart)
                  .OnComplete(() =>
                      sr.DOColor(Color.white, 0.2f)
                        .SetEase(Ease.InQuart));
            }

            if (transform != null)
            {
                DOTween.Kill(transform);
                transform.DOShakeScale(
                    duration: 0.3f,
                    strength: new Vector3(0.15f, 0.15f, 0f),
                    vibrato: 5,
                    randomness: 30f);
            }
        }

        // ══════════════════════════════════════════════════════
        // 파티클 생성 유틸리티
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 파티클 프리팹을 위치/방향/크기에 맞게 Instantiate 후 자동 Destroy.
        ///
        /// [direction 활용]
        ///   ParticleSystem.main.startRotation 에 방향 각도 적용.
        ///   피격 방향으로 파티클이 튀어나오도록.
        ///
        /// [자동 Destroy]
        ///   ParticleSystem duration + maxLifetime 만큼 대기 후 자동 파괴.
        ///   Prefab 미연결(null) 이면 아무것도 하지 않음.
        /// </summary>
        /// <param name="prefab">파티클 프리팹</param>
        /// <param name="position">생성 위치 (World Space)</param>
        /// <param name="direction">파티클 방향 (정규화 권장)</param>
        /// <param name="scale">생성 오브젝트 Scale 배율</param>
        private static void SpawnParticle(
            GameObject prefab,
            Vector3 position,
            Vector2 direction,
            float scale = 1f)
        {
            if (prefab == null) return;

            // 방향 각도 계산
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            GameObject go = Object.Instantiate(prefab, position, rotation);
            go.transform.localScale = Vector3.one * scale;

            // 자동 Destroy
            if (go.TryGetComponent<ParticleSystem>(out var ps))
            {
                float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
                Object.Destroy(go, lifetime);
            }
            else
            {
                Object.Destroy(go, 3f); // fallback
            }
        }
    }
}