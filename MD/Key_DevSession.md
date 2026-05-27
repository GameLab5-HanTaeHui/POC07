## 1. 코딩 규칙

### 🛠️ 유니티 환경

| 항목 | 내용 |
|------|------|
| Unity | TBD (LTS 권장) — 2D URP |
| Cinemachine | `Unity.Cinemachine` — `CinemachineCamera` Priority 방식 |
| Input System | New Input System — 코드 직접 방식 |
| DOTween | 최신 안정 버전 |
| TextMeshPro | UI 텍스트 전용 |

---

### 📐 코딩 컨벤션

| 항목 | 규칙 |
|------|------|
| 네임스페이스 | `KEY` 통일 |
| 변수명 | `_camelCase` (언더스코어 접두사) |
| 접근 제한자 | `[SerializeField] private` 또는 `public` 명시 필수 |
| 주석 | 모든 함수·변수에 `/// <summary>` 필수 |
| 인스펙터 변수 | `[SerializeField]` 에 반드시 `[Tooltip]` 추가 |
| 싱글턴 | `public static T Instance { get; private set; }` |
| 충돌 판단 | `CompareTag` 금지 → `LayerMask` 비트 연산 |
| DOTween | 이동·페이드·펀치·쉐이크 전반 활용 |