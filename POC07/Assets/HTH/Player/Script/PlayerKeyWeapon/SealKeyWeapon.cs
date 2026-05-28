// ============================================================
// SealKeyWeapon.cs  v1.0
// 봉인 열쇠 무기 구현체
//
// [역할]
//   PlayerWeaponBase 상속. 공격 버튼 입력 시 SealProjectile 을 발사.
//   RustyKeyWeapon 의 콤보 구조와 달리 단일 발사 + 쿨타임 구조.
//
// [PlayerWeaponBase 와의 차이점]
//   RustyKeyWeapon : KeyDataSO 수치 사용. 콤보 시스템.
//   SealKeyWeapon  : SealDataSO 수치 사용. 단일 발사 + 쿨타임.
//                    → SetKeyData() 는 사용하지 않음 (KeyDataSO 없음)
//                    → SetSealData() 를 별도로 제공
//                    → WeaponKeyController 가 캐스팅으로 호출
//
// [발사 흐름]
//   InputManager.OnAttack
//     → HandleAttackInput() (PlayerWeaponBase)
//       → Attack() or AirAttack()
//           → FireProjectile()
//               → Instantiate(SealProjectile Prefab)
//               → SealProjectile.Launch(_sealData, facingDir)
//
// [공중 공격]
//   공중에서도 동일하게 수평 발사.
//   AirAttack() 은 Attack() 과 동일 로직으로 처리.
//   추후 공중 전용 하방 발사 등 변형 가능.
//
// [쿨타임]
//   _cooldownTimer 가 0 이하일 때만 발사 가능.
//   발사 후 _sealData.cooldown 으로 리셋.
//
// [WeaponKeyController 연동]
//   WeaponKeyController._weaponEntries 에
//   keyType = KeyType.Seal / weapon = SealKeyWeapon 으로 등록.
//   ActivateWeapon() 에서 캐스팅 후 SetSealData() 호출.
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 봉인 열쇠 무기 구현체. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [콤보 없음 — 단발 발사 구조]
    ///   Attack() / AirAttack() 모두 FireProjectile() 호출.
    ///   ComboReset() 은 상태 초기화만 수행.
    ///
    /// [쿨타임 구조]
    ///   _cooldownTimer > 0 이면 발사 불가.
    ///   발사 성공 시 _cooldownTimer = _sealData.cooldown 으로 리셋.
    ///   Update() 에서 매 프레임 감산.
    ///
    /// [발사 위치]
    ///   _firePoint Transform 이 연결되어 있으면 그 위치에서 발사.
    ///   미연결 시 이 컴포넌트의 transform.position 에서 발사.
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class SealKeyWeapon : PlayerWeaponBase
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 봉인 열쇠 설정 ──────────────────────")]

        /// <summary>
        /// 봉인 투사체 Prefab.
        /// SealProjectile 컴포넌트가 부착된 Prefab 을 연결.
        /// </summary>
        [Tooltip("SealProjectile 컴포넌트가 부착된 투사체 Prefab. 필수 연결.")]
        [SerializeField] private SealProjectile _projectilePrefab;

        /// <summary>
        /// 투사체 발사 위치 Transform.
        /// Weapon 오브젝트 앞쪽에 빈 오브젝트를 배치하고 연결.
        /// 미연결 시 이 컴포넌트의 position 에서 발사.
        /// </summary>
        [Tooltip("투사체 발사 위치. 미연결 시 이 오브젝트 위치에서 발사.")]
        [SerializeField] private Transform _firePoint;

        // ──────────────────────────────────────────
        // 데이터
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 장착된 봉인 데이터 SO.
        /// SetSealData() 로 주입. KeyDataSO 대신 사용.
        /// </summary>
        private SealDataSO _sealData;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary>
        /// 발사 쿨타임 잔여 시간 (초).
        /// 0 이하일 때만 발사 가능.
        /// 발사 성공 시 _sealData.cooldown 으로 리셋.
        /// </summary>
        private float _cooldownTimer;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// Prefab 연결 검증.
        /// </summary>
        private void Awake()
        {
            if (_projectilePrefab == null)
                Debug.LogError("[SealKeyWeapon] _projectilePrefab 이 연결되지 않았습니다.");
        }

        /// <summary>
        /// 매 프레임 쿨타임 감산.
        /// </summary>
        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — WeaponKeyController 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 데이터 주입.
        /// WeaponKeyController.ActivateWeapon() 에서
        /// weapon as SealKeyWeapon 으로 캐스팅 후 호출.
        ///
        /// [KeyDataSO 를 사용하지 않는 이유]
        ///   봉인 열쇠는 콤보 타이밍, 스윙 거리 등 KeyDataSO 필드를
        ///   사용하지 않음. 독립 SO 구조로 역할 분리.
        ///   SetKeyData() 는 base 에 그대로 두되 사용하지 않음.
        /// </summary>
        /// <param name="sealData">장착할 봉인 열쇠 데이터</param>
        public void SetSealData(SealDataSO sealData)
        {
            if (sealData == null)
            {
                Debug.LogError("[SealKeyWeapon] SetSealData 에 null 이 전달됐습니다.");
                return;
            }

            _sealData = sealData;
            _cooldownTimer = 0f;

            Debug.Log($"[SealKeyWeapon] 봉인 데이터 설정: {sealData.sealKeyName} / {sealData.sealType}");
        }

        // ══════════════════════════════════════════════════════
        // PlayerWeaponBase override
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지상 공격 — 봉인 투사체 발사.
        ///
        /// [발사 가능 조건]
        ///   1. _sealData 가 주입되어 있음
        ///   2. _projectilePrefab 이 연결되어 있음
        ///   3. _cooldownTimer 가 0 이하
        /// </summary>
        protected override void Attack()
        {
            FireProjectile();
        }

        /// <summary>
        /// 공중 공격 — 지상과 동일하게 수평 발사.
        /// 추후 공중 전용 하방 발사 등 변형 시 이 함수를 수정.
        /// </summary>
        protected override void AirAttack()
        {
            FireProjectile();
        }

        /// <summary>
        /// 콤보 리셋.
        /// 봉인 열쇠는 콤보 없음 → 공격 중 플래그만 초기화.
        /// </summary>
        public override void ComboReset()
        {
            _isAttacking = false;
        }

        // ══════════════════════════════════════════════════════
        // 발사 — 내부
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 투사체 발사 실행.
        ///
        /// [발사 흐름]
        ///   1. 유효성 체크 (sealData, prefab, cooldown)
        ///   2. 발사 위치 결정 (_firePoint or 자신 위치)
        ///   3. Instantiate
        ///   4. SealProjectile.Launch(sealData, facingDirection)
        ///   5. 쿨타임 리셋
        ///
        /// [FacingDirection 취득]
        ///   PlayerMovementFacade.Instance.FacingDirection 사용.
        ///   RustyKeyWeapon 과 동일한 방식.
        /// </summary>
        private void FireProjectile()
        {
            // ① 유효성 체크
            if (_sealData == null)
            {
                Debug.LogWarning("[SealKeyWeapon] SealData 가 주입되지 않았습니다.");
                return;
            }

            if (_projectilePrefab == null)
            {
                Debug.LogError("[SealKeyWeapon] ProjectilePrefab 이 연결되지 않았습니다.");
                return;
            }

            if (_cooldownTimer > 0f)
            {
                Debug.Log($"[SealKeyWeapon] 쿨타임 중 ({_cooldownTimer:F2}초 남음)");
                return;
            }

            // ② 발사 위치 결정
            Vector3 firePos = _firePoint != null
                ? _firePoint.position
                : transform.position;

            // ③ 투사체 생성
            SealProjectile projectile = Instantiate(
                _projectilePrefab,
                firePos,
                Quaternion.identity);

            if (projectile == null)
            {
                Debug.LogError("[SealKeyWeapon] SealProjectile Instantiate 실패.");
                return;
            }

            // ④ 발사 방향 전달 및 발사
            float facingDir = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;
            projectile.Launch(_sealData, facingDir);

            // ⑤ 쿨타임 리셋
            _cooldownTimer = _sealData.cooldown;

            Debug.Log($"[SealKeyWeapon] 발사 완료 — 방향: {facingDir} / 타입: {_sealData.sealType}");
        }

        // ══════════════════════════════════════════════════════
        // PlayerWeaponBase override — 준비 조건
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 열쇠 발사 준비 조건.
        /// KeyDataSO 가 없어도 SealDataSO 가 있으면 발사 허용.
        ///
        /// PlayerWeaponBase.HandleAttackInput() 에서 이 값을 체크.
        /// false 이면 Attack() / AirAttack() 이 호출되지 않음.
        /// </summary>
        protected override bool IsReadyToFire => _sealData != null;

        // ══════════════════════════════════════════════════════
        // 프로퍼티 — 외부 읽기
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 쿨타임 잔여 시간.
        /// UI 쿨타임 게이지 표시에 사용.
        /// </summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownTimer);

        /// <summary>
        /// 현재 발사 가능 여부.
        /// UI 버튼 활성화 판단에 사용.
        /// </summary>
        public bool CanFire => _cooldownTimer <= 0f && _sealData != null;

        /// <summary>
        /// 현재 장착된 봉인 데이터.
        /// UI 봉인 타입 아이콘 표시에 사용.
        /// </summary>
        public SealDataSO SealData => _sealData;

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 발사 위치 표시
            Vector3 pos = _firePoint != null ? _firePoint.position : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pos, 0.1f);

            // 발사 방향 표시
            if (_sealData != null)
            {
                float dir = PlayerMovementFacade.Instance?.FacingDirection ?? 1f;
                Gizmos.color = _sealData.sealColor;
                Gizmos.DrawRay(pos, new Vector3(dir, 0f, 0f) * 1.5f);
            }
        }
#endif
    }
}