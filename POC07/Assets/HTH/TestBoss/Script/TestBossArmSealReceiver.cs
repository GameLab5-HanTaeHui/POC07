// ============================================================
// TestBossArmSealReceiver.cs  v1.0
// 테스트 보스 팔 — 봉인 투사체 수신 컴포넌트
//
// [역할]
//   SealProjectile 이 Arm_L / Arm_R 에 명중했을 때
//   TestBossArmPart.ApplySealByProjectile() 을 호출하여
//   해당 팔을 일시적으로 기능 봉인한다.
//
// [부착 위치]
//   Arm_L 오브젝트 또는 그 자식 (Collider2D 가 있는 오브젝트).
//   Arm_R 오브젝트 또는 그 자식.
//
// [Layer 요구사항]
//   팔 오브젝트 Layer = Enemy (SealProjectile._sealLayer 에 포함)
//   팔 Collider2D IsTrigger = true
//
// [흐름]
//   SealProjectile.OnTriggerEnter2D()
//     → Enemy 레이어 감지
//     → GetComponentInParent<SealComponent>() 탐색
//         → TestBoss 루트의 SealComponent 에 ApplySeal() (보스 전체 봉인)
//     → GetComponentInParent<TestBossArmSealReceiver>() 탐색
//         → ApplySealByProjectile() 호출 (팔 개별 봉인)
//
//   [주의]
//   SealProjectile.HandleEnemyHit() 이 SealComponent 를 먼저 처리하고 Expire().
//   팔 개별 봉인은 별도 트리거 충돌로 처리되므로
//   팔 Collider2D 는 본체 Collider2D 와 독립적으로 존재해야 함.
//   또는 OnTriggerEnter2D 에서 직접 처리.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 테스트 보스 팔 봉인 투사체 수신 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [Prefab 설정]
    ///   Arm_L / Arm_R 오브젝트에 부착.
    ///   _armPart 미연결 시 GetComponentInParent 로 자동 탐색.
    ///   오브젝트 Layer = Enemy, Collider2D IsTrigger = true 필요.
    ///
    /// [SealProjectile 과의 연동]
    ///   SealProjectile 이 Enemy 레이어를 감지하여 Expire() 하기 전에
    ///   이 컴포넌트가 OnTriggerEnter2D 로 팔 봉인을 처리.
    ///   팔의 Collider2D 가 SealProjectile._sealLayer 에 포함되어야 함.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class TestBossArmSealReceiver : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 팔 부위 연결 ──────────────────────")]

        /// <summary>
        /// 이 수신기가 봉인을 전달할 TestBossArmPart.
        /// 미연결 시 GetComponentInParent 로 자동 탐색.
        /// </summary>
        [Tooltip("TestBossArmPart. 미연결 시 자동 탐색.")]
        [SerializeField] private TestBossArmPart _armPart;

        [Header("── 봉인 수치 ──────────────────────")]

        /// <summary>
        /// 봉인 투사체 적중 시 팔 봉인 지속 시간 (초).
        /// 이 시간 동안 해당 팔의 패턴이 실행 불가.
        /// TestBossDataSO 에 없으므로 Inspector 에서 직접 설정.
        /// </summary>
        [Tooltip("봉인 지속 시간 (초). 권장: 3.0~6.0.")]
        [Range(1f, 15f)]
        [SerializeField] private float _sealDuration = 4.0f;

        /// <summary>
        /// SealProjectile 감지 레이어 마스크.
        /// PlayerHitbox 레이어를 포함.
        /// </summary>
        [Tooltip("SealProjectile 감지 레이어. PlayerHitbox 레이어 선택.")]
        [SerializeField] private LayerMask _sealProjectileLayer;

        // ──────────────────────────────────────────
        // Unity 라이프사이클
        // ──────────────────────────────────────────

        private void Awake()
        {
            if (_armPart == null)
                _armPart = GetComponentInParent<TestBossArmPart>();

            if (_armPart == null)
                Debug.LogWarning("[TestBossArmSealReceiver] TestBossArmPart 를 찾을 수 없습니다.");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // SealProjectile 레이어 체크
            int layer = 1 << other.gameObject.layer;
            if ((_sealProjectileLayer.value & layer) == 0) return;

            // SealProjectile 컴포넌트 확인
            if (!other.TryGetComponent<SealProjectile>(out _)) return;

            // 이미 봉인 중이면 무시
            if (_armPart != null && _armPart.IsSealedByProjectile) return;

            // 팔 봉인 적용
            _armPart?.ApplySealByProjectile(_sealDuration);

            Debug.Log($"[TestBossArmSealReceiver] {gameObject.name} 봉인 적중 → {_sealDuration:F1}초 봉인");
        }

        // ──────────────────────────────────────────
        // Gizmos
        // ──────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            bool isSealed = _armPart != null && _armPart.IsSealedByProjectile;
            Gizmos.color = isSealed
                ? new Color(0.2f, 0.9f, 0.4f, 0.4f)
                : new Color(0.3f, 0.6f, 1.0f, 0.2f);

            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}