using UnityEngine;

namespace Data
{
    // 운영 모드(로컬/서버 연동)와 로컬 모드에서 화면에 쓰는 체험자 이름.
    // 에셋 값은 기본값이고, 관리자 페이지에서 바꾼 값은 PlayerPrefs에 저장해 그 값을 우선한다
    // (빌드에서는 런타임에 SO를 바꿔도 앱을 다시 켜면 에셋 값으로 돌아가므로).
    [CreateAssetMenu(fileName = "VisitorSettings", menuName = "DG/Visitor Settings")]
    public class VisitorSettings : ScriptableObject
    {
        public const string ServerConnectedKey = "VisitorSettings.IsServerConnected";
        public const string VisitorNameKey     = "VisitorSettings.VisitorName";

        [Tooltip("서버 연동(QR 인식) 모드 기본값 — 관리자 페이지에서 바꾼 값이 있으면 그 값을 쓴다")]
        [SerializeField] private bool defaultServerConnected;

        [Tooltip("로컬 모드에서 표시할 체험자 이름 기본값 — 관리자 페이지에서 바꾼 값이 있으면 그 값을 쓴다")]
        [SerializeField] private string defaultVisitorName = "체험자";

        public bool IsServerConnected
        {
            get => PlayerPrefs.GetInt(ServerConnectedKey, defaultServerConnected ? 1 : 0) != 0;
            set
            {
                PlayerPrefs.SetInt(ServerConnectedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public string VisitorName
        {
            get => PlayerPrefs.GetString(VisitorNameKey, defaultVisitorName);
            set
            {
                PlayerPrefs.SetString(VisitorNameKey, value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 관리자 페이지에서 바꾼 값을 지워 에셋 기본값으로 되돌린다 — 에디터에서 기본값을 바꿔도 반영되지 않을 때 쓴다.
        /// </summary>
        [ContextMenu("관리자 페이지 변경값 지우기")]
        public void ClearOverrides()
        {
            PlayerPrefs.DeleteKey(ServerConnectedKey);
            PlayerPrefs.DeleteKey(VisitorNameKey);
            PlayerPrefs.Save();
        }
    }
}
