using System;

namespace Data
{
    // StreamingAssets/Visitor.json 매핑 — 서버 연동 여부와 서버 미연결 시 사용할 기본 체험자 이름
    [Serializable]
    public class VisitorData
    {
        public bool isServerConnected;
        public string defaultUserName = "체험자";
    }
}
