using System.Collections.Generic;

namespace Admin
{
    // 관리자 창(비밀번호 창·관리자 화면)이 열려 있는지 — 타이틀이 그동안 찍힌 QR을 서버로 보내지 않는 데 쓴다.
    // 비밀번호 창에서 관리자 화면으로 넘어갈 때 두 창이 잠깐 함께 켜지거나 꺼지는 순서와 상관없이 맞도록 열린 창을 모아 센다.
    public sealed class AdminScreenState
    {
        private readonly HashSet<object> _openPanels = new();

        public bool IsOpen => _openPanels.Count > 0;

        /// <summary>
        /// 관리자 창 하나가 켜지거나 꺼졌음을 기록한다.
        /// </summary>
        public void SetOpen(object panel, bool isOpen)
        {
            if (isOpen) _openPanels.Add(panel);
            else _openPanels.Remove(panel);
        }
    }
}
