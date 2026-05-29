// ============================================================
// SealComponent.cs  v1.0
// 적 봉인 상태 관리 컴포넌트 — EnemySealComponent 대체
//
// [EnemySealComponent 와의 차이]
//   클래스명 변경: EnemySealComponent → SealComponent
//   내부 로직 동일 유지 (Dictionary + Queue 구조)
//   로그 prefix 변경: [EnemySealComponent] → [SealComponent]
//
// [역할]
//   SealProjectile 이 적에 명중했을 때 봉인을 수신·관리.
//   EnemyAI 가 행동 실행 직전 IsSealedAction() 을 체크하여
//   봉인된 행동이면 스킵.
//
// [봉인 적용 흐름]
//   SealProjectile.OnTriggerEnter2D()
//     → SealComponent.ApplySeal(SealDataSO)
//         → _activeSeals 딕셔너리에 (SealType, 잔여시간) 등록
//         → maxSealCount 초과 시 가장 오래된 봉인 제거
//         → OnSealApplied 이벤트 발행
//
// [봉인 해제 흐름]
//   Update() 에서 잔여시간 감산
//     → 0 이하 시 해당 SealType 제거
//     → OnSealRemoved 이벤트 발행
//
// [EnemyAI 연동]
//   EnemyAI._sealComponent : SealComponent 타입으로 참조
//   IsSealed(SealType) → _sealComponent.IsSealedAction(sealType)
//
// [EnemyKnight 연동]
//   Guard 봉인 활성 시 방패 무시 → base.TakeDamage() 허용
//
// [네임스페이스]
//   namespace : KEY
// ============================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KEY
{
    /// <summary>
    /// 적 봉인 상태 관리 컴포넌트. (v1.0)
    ///
    /// ────────────────────────────────────────────────────
    /// [부착 위치]
    ///   EnemyBase 를 가진 오브젝트와 같은 게임오브젝트에 부착.
    ///   EnemyKnight, EnemyDummy 등 모든 적에 공통 사용 가능.
    ///
    /// [EnemyAI 에서 사용 예시]
    ///   if (_sealComponent.IsSealedAction(SealType.Dash)) return;
    ///
    /// [EnemyKnight 에서 Guard 봉인 체크 예시]
    ///   bool guardSealed = _sealComponent.IsSealedAction(SealType.Guard);
    ///   if (guardSealed) base.TakeDamage(info);
    /// ────────────────────────────────────────────────────
    /// </summary>
    public class SealComponent : MonoBehaviour
    {
        // ──────────────────────────────────────────
        // Inspector
        // ──────────────────────────────────────────

        [Header("── 비주얼 연결 ──────────────────────")]

        /// <summary>
        /// 봉인 상태 오버레이 SpriteRenderer.
        /// 적 위에 자물쇠 아이콘을 표시하는 자식 오브젝트.
        /// 미연결 시 오버레이 표시 생략.
        /// </summary>
        [Tooltip("봉인 오버레이 스프라이트 렌더러. 적 위 자물쇠 아이콘. 미연결 시 생략.")]
        [SerializeField] private SpriteRenderer _overlayRenderer;

        // ──────────────────────────────────────────
        // 컴포넌트 참조
        // ──────────────────────────────────────────

        /// <summary>
        /// 봉인 플래시에 사용할 본체 SpriteRenderer.
        /// Awake 에서 같은 오브젝트의 SpriteRenderer 자동 취득.
        /// </summary>
        private SpriteRenderer _spriteRenderer;

        // ──────────────────────────────────────────
        // 봉인 상태 — 핵심 데이터
        // ──────────────────────────────────────────

        /// <summary>
        /// 현재 활성 봉인 목록.
        /// Key: SealType / Value: 잔여 지속 시간 (초).
        /// Update() 에서 매 프레임 감산, 0 이하 시 제거.
        /// </summary>
        private readonly Dictionary<SealType, float> _activeSeals
            = new Dictionary<SealType, float>();

        /// <summary>
        /// 봉인 적용 순서 추적 큐.
        /// maxSealCount 초과 시 가장 먼저 적용된 타입을 제거.
        /// </summary>
        private readonly Queue<SealType> _sealOrder
            = new Queue<SealType>();

        // ──────────────────────────────────────────
        // 내부 상태 — 비주얼
        // ──────────────────────────────────────────

        private Coroutine _flashCoroutine;
        private Color _originalColor;

        // ──────────────────────────────────────────
        // GC 방지 버퍼
        // ──────────────────────────────────────────

        private readonly List<SealType> _expiredBuffer = new List<SealType>();

        // ──────────────────────────────────────────
        // 이벤트
        // ──────────────────────────────────────────

        /// <summary> 봉인이 새로 적용될 때 발행. </summary>
        public event Action<SealType> OnSealApplied;

        /// <summary> 봉인이 해제될 때 발행. </summary>
        public event Action<SealType> OnSealRemoved;

        // ──────────────────────────────────────────
        // 프로퍼티
        // ──────────────────────────────────────────

        /// <summary> 봉인이 하나라도 활성 중인지 여부. </summary>
        public bool HasAnySeal => _activeSeals.Count > 0;

        /// <summary> 현재 활성 봉인 수. </summary>
        public int SealCount => _activeSeals.Count;

        // ══════════════════════════════════════════════════════
        // Unity 라이프사이클
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_spriteRenderer != null)
                _originalColor = _spriteRenderer.color;

            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;
        }

        private void Update()
        {
            if (_activeSeals.Count == 0) return;

            _expiredBuffer.Clear();

            foreach (var pair in _activeSeals)
            {
                float remaining = pair.Value - Time.deltaTime;
                if (remaining <= 0f)
                    _expiredBuffer.Add(pair.Key);
                else
                    _activeSeals[pair.Key] = remaining;
            }

            foreach (SealType expired in _expiredBuffer)
                RemoveSeal(expired);
        }

        private void OnDestroy()
        {
            ForceReleaseAll();
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — SealProjectile 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 봉인 적용.
        /// SealProjectile 이 Enemy 레이어 명중 시 호출.
        ///
        /// [같은 타입 재적용] 타이머 리셋 (스택 없음)
        /// [maxSealCount 초과] 가장 오래된 봉인 제거 후 추가
        /// </summary>
        public void ApplySeal(SealDataSO data)
        {
            if (data == null) return;

            SealType type = data.sealType;

            // 같은 타입 재적용 → 타이머 리셋
            if (_activeSeals.ContainsKey(type))
            {
                _activeSeals[type] = data.sealDuration;
                RefreshSealOrder(type);
                Debug.Log($"[SealComponent] 봉인 타이머 리셋: {type} ({data.sealDuration:F1}초)");
                return;
            }

            // maxSealCount 초과 시 가장 오래된 봉인 제거
            while (_sealOrder.Count >= data.maxSealCount && _sealOrder.Count > 0)
            {
                SealType oldest = _sealOrder.Dequeue();
                RemoveSeal(oldest);
            }

            // 새 봉인 등록
            _activeSeals[type] = data.sealDuration;
            _sealOrder.Enqueue(type);

            StartSealVisual(data);
            OnSealApplied?.Invoke(type);

            Debug.Log($"[SealComponent] 봉인 적용: {type} ({data.sealDuration:F1}초) " +
                      $"현재 봉인 수: {_activeSeals.Count}");
        }

        // ══════════════════════════════════════════════════════
        // 외부 API — EnemyAI / EnemyKnight 에서 호출
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 지정 봉인 타입이 현재 활성 중인지 확인.
        /// EnemyAI 행동 실행 직전 체크.
        /// </summary>
        public bool IsSealedAction(SealType sealType)
            => _activeSeals.ContainsKey(sealType);

        /// <summary>
        /// 특정 봉인 타입의 잔여 시간 반환.
        /// 활성 중이 아니면 0 반환.
        /// </summary>
        public float GetRemainingTime(SealType sealType)
            => _activeSeals.TryGetValue(sealType, out float t) ? t : 0f;

        /// <summary>
        /// 모든 봉인 즉시 강제 해제.
        /// 자물쇠 해제 완료 / 적 사망 등 특수 상황.
        /// </summary>
        public void ForceReleaseAll()
        {
            var types = new List<SealType>(_activeSeals.Keys);
            foreach (SealType t in types)
                RemoveSeal(t);

            _sealOrder.Clear();
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 봉인 제거
        // ══════════════════════════════════════════════════════

        private void RemoveSeal(SealType sealType)
        {
            if (!_activeSeals.ContainsKey(sealType)) return;

            _activeSeals.Remove(sealType);
            RemoveFromOrder(sealType);

            if (_activeSeals.Count == 0)
                StopSealVisual();

            OnSealRemoved?.Invoke(sealType);

            Debug.Log($"[SealComponent] 봉인 해제: {sealType} " +
                      $"남은 봉인: {_activeSeals.Count}");
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 큐 관리
        // ══════════════════════════════════════════════════════

        private void RefreshSealOrder(SealType sealType)
        {
            RemoveFromOrder(sealType);
            _sealOrder.Enqueue(sealType);
        }

        private void RemoveFromOrder(SealType sealType)
        {
            int count = _sealOrder.Count;
            for (int i = 0; i < count; i++)
            {
                SealType t = _sealOrder.Dequeue();
                if (t != sealType)
                    _sealOrder.Enqueue(t);
            }
        }

        // ══════════════════════════════════════════════════════
        // 내부 — 비주얼 피드백
        // ══════════════════════════════════════════════════════

        private void StartSealVisual(SealDataSO data)
        {
            if (_overlayRenderer != null)
            {
                if (data.sealOverlaySprite != null)
                    _overlayRenderer.sprite = data.sealOverlaySprite;
                _overlayRenderer.color = data.sealColor;
                _overlayRenderer.enabled = true;
            }

            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            if (_spriteRenderer != null)
                _flashCoroutine = StartCoroutine(SealFlashRoutine(data));
        }

        private void StopSealVisual()
        {
            if (_overlayRenderer != null)
                _overlayRenderer.enabled = false;

            if (_flashCoroutine != null)
            {
                StopCoroutine(_flashCoroutine);
                _flashCoroutine = null;
            }

            if (_spriteRenderer != null)
                _spriteRenderer.color = _originalColor;
        }

        private IEnumerator SealFlashRoutine(SealDataSO data)
        {
            float interval = data.sealFlashInterval;

            while (_activeSeals.Count > 0)
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.color = data.sealColor;
                yield return new WaitForSeconds(interval);

                if (_spriteRenderer != null)
                    _spriteRenderer.color = _originalColor;
                yield return new WaitForSeconds(interval);
            }

            if (_spriteRenderer != null)
                _spriteRenderer.color = _originalColor;
        }

        // ══════════════════════════════════════════════════════
        // Gizmos
        // ══════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_activeSeals.Count == 0) return;

            UnityEditor.Handles.color = Color.blue;
            int i = 0;
            foreach (var pair in _activeSeals)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * (1.8f + i * 0.3f),
                    $"[봉인] {pair.Key} : {pair.Value:F1}초");
                i++;
            }
        }
#endif
    }
}