using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Admin
{
    // 순서에 뜻이 있는 버튼 배열(비밀번호 숫자 키·레벨 이동 버튼)에, 누른 버튼의 순번을 넘기는 동작을 연결하고 해제한다.
    // 람다로 연결한 동작은 같은 인스턴스로만 해제할 수 있어 연결할 때 만든 동작 배열을 돌려준다.
    public static class IndexedButtons
    {
        /// <summary>
        /// 각 버튼에 onClick(순번)을 연결하고, 해제할 때 쓸 동작 배열을 반환한다. 비어 있는 칸은 onMissing(순번)으로 알린다.
        /// </summary>
        public static UnityAction[] Bind(Button[] buttons, Action<int> onClick, Action<int> onMissing)
        {
            UnityAction[] actions = new UnityAction[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (!buttons[i])
                {
                    onMissing(i);
                    continue;
                }

                int index = i;
                actions[i] = () => onClick(index);
                buttons[i].onClick.AddListener(actions[i]);
            }
            return actions;
        }

        /// <summary>
        /// Bind로 연결한 동작을 해제한다 (연결 전이면 아무것도 하지 않는다).
        /// </summary>
        public static void Unbind(Button[] buttons, UnityAction[] actions)
        {
            if (actions == null) return;

            for (int i = 0; i < buttons.Length && i < actions.Length; i++)
                if (buttons[i] && actions[i] != null) buttons[i].onClick.RemoveListener(actions[i]);
        }
    }
}
