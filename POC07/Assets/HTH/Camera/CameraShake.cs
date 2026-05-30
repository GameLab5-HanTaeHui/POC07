// ============================================================
// CameraShake.cs  v1.0
// 카메라 흔들림 유틸리티 — 2D 사이드뷰 전용
//
// [역할]
//   Camera.main 을 DOShakePosition 으로 흔들어 타격감 표현.
//   IsEnabled bool 로 전체 on/off 제어.
//   static 메서드 — 어디서든 직접 호출 가능.
//
// [2D 사이드뷰 주의사항]
//   Z 축 강도 = 0 — 카메라가 앞뒤로 이동하지 않도록 고정.
//   X/Y 평면 흔들림만 허용.
//
// [IsEnabled 제어 방법]
//   코드: CameraShake.IsEnabled = false;
//   또는 씬의 CameraShakeSettings 컴포넌트로 Inspector 에서 제어.
//
// [사용법]
//   CameraShake.Shake(strength: 0.15f, duration: 0.1f);
//   CameraShake.Shake(CameraShake.Preset.Heavy);
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;
using DG.Tweening;

namespace KEY
{
    /// <summary>
    /// 카메라 흔들림 유틸리티. 2D 사이드뷰 전용. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [흔들림 프리셋]
    ///   Light  : 약한 흔들림 — 일반 피격 / Combo1,2
    ///   Medium : 중간 흔들림 — 강한 피격 / AirDown
    ///   Heavy  : 강한 흔들림 — 피니셔 / Combo3
    ///
    /// [IsEnabled]
    ///   true  (기본값) — 흔들림 활성
    ///   false          — 흔들림 비활성 (멀미 방지 옵션 등)
    /// ────────────────────────────────────────────────────
    /// </summary>
    public static class CameraShake
    {
        // ──────────────────────────────────────────
        // 흔들림 프리셋
        // ──────────────────────────────────────────

        public enum Preset
        {
            /// <summary> 약한 흔들림. Combo1/2, 일반 피격. </summary>
            Light,
            /// <summary> 중간 흔들림. AirDown, 자물쇠 해제. </summary>
            Medium,
            /// <summary> 강한 흔들림. Combo3 피니셔. </summary>
            Heavy,
        }

        // ──────────────────────────────────────────
        // 전역 on/off
        // ──────────────────────────────────────────

        /// <summary>
        /// 카메라 흔들림 전체 활성 여부.
        /// false 이면 Shake() 호출 즉시 리턴.
        /// CameraShakeSettings 컴포넌트에서 Inspector 로 제어 가능.
        /// </summary>
        public static bool IsEnabled { get; set; } = true;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        private static Tween _currentShake;

        // ══════════════════════════════════════════════════════
        // 외부 API — 직접 수치 지정
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 카메라 흔들림 실행.
        ///
        /// [2D 사이드뷰]
        ///   strength 의 Z = 0 으로 강제.
        ///   X/Y 평면 흔들림만 허용.
        ///
        /// [중복 처리]
        ///   현재 흔들림 진행 중이면 Kill 후 새로 시작.
        ///   더 강한 흔들림 요청 시 덮어씀.
        /// </summary>
        /// <param name="strength">흔들림 강도. X/Y 만 사용. 권장: 0.05~0.2</param>
        /// <param name="duration">흔들림 지속 시간 (초). 권장: 0.06~0.15</param>
        /// <param name="vibrato">진동 횟수. 권장: 10~20</param>
        /// <param name="randomness">방향 무작위성. 권장: 45~90</param>
        public static void Shake(
            float strength = 0.1f,
            float duration = 0.1f,
            int vibrato = 14,
            float randomness = 60f)
        {
            if (!IsEnabled) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            _currentShake?.Kill();

            // Z = 0 강제 — 2D 사이드뷰에서 카메라 전후 이동 방지
            Vector3 strengthVec = new Vector3(strength, strength * 0.6f, 0f);

            _currentShake = cam.transform
                .DOShakePosition(duration, strengthVec, vibrato, randomness)
                .SetEase(Ease.OutQuart);
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — 프리셋
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 프리셋으로 카메라 흔들림 실행.
        /// </summary>
        public static void Shake(Preset preset)
        {
            switch (preset)
            {
                case Preset.Light:
                    Shake(strength: 0.06f, duration: 0.07f, vibrato: 12);
                    break;
                case Preset.Medium:
                    Shake(strength: 0.12f, duration: 0.10f, vibrato: 14);
                    break;
                case Preset.Heavy:
                    Shake(strength: 0.18f, duration: 0.12f, vibrato: 16);
                    break;
            }
        }

        /// <summary>
        /// 진행 중인 카메라 흔들림 즉시 정지.
        /// </summary>
        public static void Stop()
        {
            _currentShake?.Kill();
        }
    }

    // ══════════════════════════════════════════════════════════
    // Inspector 제어용 컴포넌트
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// CameraShake.IsEnabled 를 Inspector 에서 제어하는 컴포넌트.
    /// GameManager 또는 SettingsManager 오브젝트에 부착.
    /// </summary>
    public class CameraShakeSettings : MonoBehaviour
    {
        [Header("── 카메라 흔들림 ──────────────────────")]

        /// <summary>
        /// 카메라 흔들림 활성 여부.
        /// 체크 해제 시 모든 Shake() 호출 무시.
        /// </summary>
        [Tooltip("카메라 흔들림 on/off. 해제 시 모든 흔들림 비활성.")]
        [SerializeField] private bool _enableCameraShake = true;

        private void Awake()
        {
            CameraShake.IsEnabled = _enableCameraShake;
        }

        private void OnValidate()
        {
            // 에디터에서 실시간 토글 반영
            CameraShake.IsEnabled = _enableCameraShake;
        }
    }
}