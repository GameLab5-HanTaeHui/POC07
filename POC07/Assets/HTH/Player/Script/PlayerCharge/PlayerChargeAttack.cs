// ============================================================
// PlayerChargeAttack.cs  v1.3
// 플레이어 차징 공격 — 상태 관리 / 각도 조절 / 투사체 발사
//
// [v1.3 변경]
//   ④ 차징 중 좌우 방향키로 발사 방향 전환
//       OnChargeFlip 이벤트 구독.
//       차징 중 방향키 입력 시 _facingOverride 갱신.
//       GetFireDirection 에서 _facingOverride 우선 사용.
//       PlayerMover.FacingDirection 은 이동 차단 중이므로 갱신 안 됨 —
//       별도 필드로 관리.
//
// [v1.2 변경]
//   ① 차징 중 이동 / 대쉬 / 점프 전면 차단
//       BlockMove() + BlockDash() + BlockJump() 동시 호출.
//       EndCharge() 에서 UnblockMove/Dash/Jump 동시 해제.
//       이동 velocity 즉시 0 으로 강제 정지.
//
//   ② 최대 차징 자동 발사 제거
//       maxChargeTime 참조 및 자동 발사 로직 삭제.
//       플레이어가 S 를 뗄 때만 발사 (minChargeTime 충족 시).
//
//   ③ 각도 조절 방식 변경: Trigger → 누름 유지 연속 변화
//       OnAimAdjust 파라미터 int → float 으로 변경.
//       +1.0 = ↑ 누름 / -1.0 = ↓ 누름 / 0.0 = 뗌.
//       _aimInput 에 저장 후 Update 에서 매 프레임 _aimAngle 갱신.
//       chargeAimAngleStep 을 초당 각도 속도로 사용 (×Time.deltaTime).
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 플레이어 차징 공격 컴포넌트. (v1.3)
    ///
    /// ────────────────────────────────────────────────────
    /// [차징 흐름]
    ///   S 누름  → 이동/점프/대쉬 차단 + 차징 타이머 시작 + AimLine 표시
    ///   ↑↓ 누름 → 누름 유지 동안 매 프레임 _aimAngle 연속 변화
    ///   S 뗌    → minChargeTime 충족 시 발사 / 미충족 시 취소
    ///
    /// [각도 조절]
    ///   chargeAimAngleStep = 초당 각도 변화량 (degrees/sec)
    ///   ±chargeAimAngleRange 범위 클램프
    ///
    /// [차단 목록]
    ///   InputManager.BlockMove()  이동 차단
    ///   InputManager.BlockDash()  대쉬 차단
    ///   InputManager.BlockJump()  점프 차단
    ///   velocity.x = 0           물리 이동 즉시 정지
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class PlayerChargeAttack : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 연결 ──────────────────────")]

        /// <summary>
        /// ChargeAimLine 컴포넌트.
        /// 미연결 시 자식에서 자동 탐색.
        /// </summary>
        [Tooltip("ChargeAimLine. 미연결 시 자동 탐색.")]
        [SerializeField] private ChargeAimLine _aimLine;

        /// <summary>
        /// 투사체 생성 위치 Transform.
        /// 미연결 시 이 컴포넌트의 position 에서 발사.
        /// </summary>
        [Tooltip("투사체 생성 위치. 미연결 시 이 오브젝트 위치.")]
        [SerializeField] private Transform _firePoint;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        private PlayerWeaponController _weaponController;
        private PlayerMover _playerMover;
        private Rigidbody2D _rigid2D;

        // ──────────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────────

        /// <summary> 현재 차징 중 여부. </summary>
        private bool _isCharging;

        /// <summary>
        /// 현재 조준 각도 (도).
        /// 0 = 수평 / 양수 = 위 / 음수 = 아래.
        /// </summary>
        private float _aimAngle;

        /// <summary>
        /// 현재 조준 입력 방향.
        /// +1.0 = ↑ 누름 / -1.0 = ↓ 누름 / 0.0 = 입력 없음.
        /// OnAimAdjust 이벤트 수신 시 갱신. Update 에서 매 프레임 _aimAngle 에 적용.
        /// </summary>
        private float _aimInput;

        /// <summary> 현재 차징 경과 시간. </summary>
        private float _chargeTimer;

        /// <summary>
        /// 차징 중 방향키로 설정된 발사 방향.
        /// +1 = 오른쪽 / -1 = 왼쪽.
        /// 차징 시작 시 PlayerMover.FacingDirection 으로 초기화.
        /// 이동이 차단된 상태에서도 방향키로 독립 갱신 가능.
        /// </summary>
        private float _facingOverride = 1f;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // PlayerWeaponController 는 자식 Weapon 오브젝트에 있으므로 InChildren 사용
            _weaponController = GetComponentInChildren<PlayerWeaponController>();
            _playerMover = GetComponent<PlayerMover>();
            _rigid2D = GetComponent<Rigidbody2D>();

            if (_aimLine == null)
                _aimLine = GetComponentInChildren<ChargeAimLine>();
        }

        private void Start()
        {
            if (InputManager.Instance == null)
            {
                Debug.LogError("[PlayerChargeAttack] InputManager 가 없습니다.");
                enabled = false;
                return;
            }

            InputManager.Instance.OnChargeStart += HandleChargeStart;
            InputManager.Instance.OnChargeRelease += HandleChargeRelease;
            InputManager.Instance.OnAimAdjust += HandleAimAdjust;
            InputManager.Instance.OnChargeFlip += HandleChargeFlip;
        }

        private void OnDestroy()
        {
            if (InputManager.Instance != null)
            {
                InputManager.Instance.OnChargeStart -= HandleChargeStart;
                InputManager.Instance.OnChargeRelease -= HandleChargeRelease;
                InputManager.Instance.OnAimAdjust -= HandleAimAdjust;
                InputManager.Instance.OnChargeFlip -= HandleChargeFlip;
            }
        }

        private void Update()
        {
            if (!_isCharging) return;

            // ── 차징 타이머 ──────────────────────
            _chargeTimer += Time.deltaTime;

            // ── 조준 각도 연속 변화 ──────────────────────
            // _aimInput 이 0 이 아닌 동안 매 프레임 각도 갱신
            if (_aimInput != 0f)
            {
                KeyDataSO data = GetCurrentKeyData();
                if (data != null)
                {
                    _aimAngle = Mathf.Clamp(
                        _aimAngle + _aimInput * data.chargeAimAngleStep * Time.deltaTime,
                        -data.chargeAimAngleRange,
                         data.chargeAimAngleRange);

                    _aimLine?.UpdateAim(GetFireDirection(data));
                }
            }

            // ── 차징 비율 AimLine 피드백 ──────────────────────
            KeyDataSO keyData = GetCurrentKeyData();
            if (keyData != null)
            {
                float ratio = Mathf.Clamp01(_chargeTimer / keyData.maxChargeTime);
                _aimLine?.UpdateCharge(ratio);
            }

            // ── 이동 차단 유지 — velocity.x 지속 0 ──────────────────────
            if (_rigid2D != null)
                _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);
        }

        // ══════════════════════════════════════════════════════
        // 이벤트 핸들러
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// S 누름 — 차징 시작.
        /// </summary>
        private void HandleChargeStart()
        {
            if (_isCharging) return;

            if (_weaponController == null)
            {
                Debug.LogError("[PlayerChargeAttack] PlayerWeaponController 를 찾을 수 없습니다.");
                return;
            }

            PlayerWeaponBase weapon = _weaponController.CurrentWeapon;
            if (weapon == null)
            {
                Debug.LogWarning("[PlayerChargeAttack] CurrentWeapon 이 null 입니다. " +
                                 "KeyInventoryDataSO._defaultKeys 에 열쇠가 등록되어 있는지 확인하세요.");
                return;
            }

            KeyDataSO data = weapon.KeyData;
            if (data == null)
            {
                Debug.LogWarning("[PlayerChargeAttack] CurrentWeapon.KeyData 가 null 입니다.");
                return;
            }

            if (data.chargeProjectilePrefab == null)
            {
                Debug.LogWarning($"[PlayerChargeAttack] '{data.keyName}' 의 chargeProjectilePrefab 이 " +
                                 "연결되지 않았습니다.");
                return;
            }

            // ── 차징 시작 ──────────────────────
            _isCharging = true;
            _chargeTimer = 0f;
            _aimAngle = 0f;
            _aimInput = 0f;
            _facingOverride = _playerMover != null ? _playerMover.FacingDirection : 1f;

            // 이동 / 점프 / 대쉬 전면 차단
            var input = InputManager.Instance;
            input.BlockMove();
            input.BlockJump();
            input.BlockDash();

            // 물리 이동 즉시 정지
            if (_rigid2D != null)
                _rigid2D.linearVelocity = new Vector2(0f, _rigid2D.linearVelocity.y);

            // 조준선 표시
            _aimLine?.Show(GetFireDirection(data));

            Debug.Log("[PlayerChargeAttack] 차징 시작");
        }

        /// <summary>
        /// S 뗌 — 최소 차징 충족 시 발사, 미충족 시 취소.
        /// </summary>
        private void HandleChargeRelease()
        {
            if (!_isCharging) return;

            KeyDataSO data = GetCurrentKeyData();
            if (data == null) { EndCharge(); return; }

            if (_chargeTimer >= data.minChargeTime)
            {
                float ratio = Mathf.Clamp01(_chargeTimer / data.maxChargeTime);
                Fire(data, ratio);
            }
            else
            {
                Debug.Log($"[PlayerChargeAttack] 차징 취소 — {_chargeTimer:F2}s / 최소 {data.minChargeTime}s");
                EndCharge();
            }
        }

        /// <summary>
        /// ↑↓ 입력 상태 수신.
        /// +1.0 = ↑ 누름 / -1.0 = ↓ 누름 / 0.0 = 뗌.
        /// _aimInput 에 저장. 실제 각도 변경은 Update 에서 처리.
        /// </summary>
        private void HandleAimAdjust(float direction)
        {
            _aimInput = direction;
        }

        /// <summary>
        /// 차징 중 좌우 방향키 입력 수신.
        /// +1 = 오른쪽 / -1 = 왼쪽.
        /// 차징 중이 아니면 무시.
        /// PlayerMover.ForceFlip() 호출 → OnFlipped 이벤트 발행
        ///   → PlayerWeaponMover / PlayerWeaponHitboxManager 자동 동기화.
        /// _facingOverride 도 동시 갱신 → GetFireDirection 에서 방향 반영.
        /// </summary>
        private void HandleChargeFlip(float direction)
        {
            if (!_isCharging) return;

            // PlayerMover.ForceFlip — 스프라이트 반전 + OnFlipped 발행
            // OnFlipped 구독자(PlayerWeaponMover, PlayerWeaponHitboxManager)가
            // Weapon localPosition.x 및 Hitbox 위치를 자동 동기화
            _playerMover?.ForceFlip(direction);

            // _facingOverride 갱신 — GetFireDirection 에서 발사 방향에 반영
            _facingOverride = direction >= 0f ? 1f : -1f;

            _firePoint.localPosition = new Vector3(Mathf.Abs(_firePoint.localPosition.x) * direction, 
                _firePoint.localPosition.y, _firePoint.localPosition.z);

            // AimLine 방향 즉시 갱신
            KeyDataSO data = GetCurrentKeyData();
            if (data != null)
                _aimLine?.UpdateAim(GetFireDirection(data));
        }

        // ══════════════════════════════════════════════════════
        // 발사 / 종료
        // ══════════════════════════════════════════════════════

        private void Fire(KeyDataSO data, float chargePower)
        {
            if (data.chargeProjectilePrefab == null) { EndCharge(); return; }

            Vector3 firePos = _firePoint != null
                ? _firePoint.position
                : transform.position;

            var go = Instantiate(data.chargeProjectilePrefab, firePos, Quaternion.identity);

            // ── SealProjectile 경로 (봉인 투사체) ──────────────────────
            var sealProjectile = go.GetComponent<SealProjectile>();
            if (sealProjectile != null)
            {
                // SealKeyWeapon 에서 SealDataSO 취득
                SealDataSO sealData = null;
                if (_weaponController?.CurrentWeapon is SealKeyWeapon sealWeapon)
                    sealData = sealWeapon.SealData;

                if (sealData != null)
                {
                    sealProjectile.Launch(sealData, _facingOverride);
                    Debug.Log($"[PlayerChargeAttack] 봉인 발사 — 각도:{_aimAngle:F1}° 방향:{_facingOverride}");
                }
                else
                {
                    Debug.LogError("[PlayerChargeAttack] SealData 가 null 입니다.");
                    Destroy(go);
                }

                EndCharge();
                return;
            }

            // ── IChargeProjectile 경로 (추후 확장용) ───────────────────
            var projectile = go.GetComponent<IChargeProjectile>();
            if (projectile != null)
            {
                projectile.Launch(GetFireDirection(data), chargePower);
                Debug.Log($"[PlayerChargeAttack] 발사 — 각도:{_aimAngle:F1}° 파워:{chargePower:F2}");
            }
            else
            {
                Debug.LogError("[PlayerChargeAttack] SealProjectile / IChargeProjectile 구현체가 없습니다.");
                Destroy(go);
            }

            EndCharge();
        }

        /// <summary>
        /// 차징 상태 종료.
        /// 모든 차단 해제 + AimLine 숨김 + 상태 초기화.
        /// </summary>
        private void EndCharge()
        {
            _isCharging = false;
            _chargeTimer = 0f;
            _aimInput = 0f;

            // 이동 / 점프 / 대쉬 차단 해제
            var input = InputManager.Instance;
            if (input != null)
            {
                input.UnblockMove();
                input.UnblockJump();
                input.UnblockDash();
            }

            _aimLine?.Hide();
        }

        // ══════════════════════════════════════════════════════
        // 보조
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 현재 발사 방향 벡터 계산.
        /// FacingDirection 기준으로 _aimAngle 만큼 회전.
        /// </summary>
        private Vector2 GetFireDirection(KeyDataSO data)
        {
            // 차징 중에는 _facingOverride 우선 사용
            // (이동 차단 중 PlayerMover.FacingDirection 이 갱신 안 되므로)
            float facing = _isCharging
                ? _facingOverride
                : (_playerMover != null ? _playerMover.FacingDirection : 1f);
            Vector2 baseDir = new Vector2(facing, 0f);

            float angleRad = _aimAngle * facing * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            return new Vector2(
                baseDir.x * cos - baseDir.y * sin,
                baseDir.x * sin + baseDir.y * cos
            ).normalized;
        }

        /// <summary> 현재 장착 무기의 KeyDataSO 반환. </summary>
        private KeyDataSO GetCurrentKeyData()
        {
            return _weaponController?.CurrentWeapon?.KeyData;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

        private void OnDrawGizmosSelected()
        {
            if (!_isCharging) return;

            Gizmos.color = Color.yellow;
            Vector3 pos = _firePoint != null ? _firePoint.position : transform.position;
            Gizmos.DrawRay(pos, (Vector3)GetFireDirection(null) * 3f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                $"Charging: {_chargeTimer:F2}s\nAngle: {_aimAngle:F1}°");
#endif
        }
    }
}