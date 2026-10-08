namespace Network
{
    /// <summary>
    /// 체험자 서버의 JSON 응답 본문을 다룬다. .cfm 출력에는 JSON 앞뒤에 줄바꿈이나 다른 글자가 섞여 나올 수 있다
    /// — 현장 서버 getUser는 JSON 끝 } 뒤에 ``` 줄을 붙여 보낸 적이 있다.
    /// </summary>
    public static class ApiJson
    {
        /// <summary>
        /// 본문에서 첫 '{'부터 마지막 '}'까지만 잘라 돌려준다. 중괄호가 없으면 본문을 그대로 돌려줘 JSON 해석에서 실패하게 둔다.
        /// </summary>
        public static string ExtractObject(string body)
        {
            int start = body.IndexOf('{');
            int end = body.LastIndexOf('}');
            return start >= 0 && end > start ? body.Substring(start, end - start + 1) : body;
        }
    }
}
