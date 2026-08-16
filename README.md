# 多益練習

直接執行 `publish/DesktopApp.exe` 即可使用 Windows 桌面版；也可以把它複製到桌面建立捷徑。

若要修改題目或單字，請編輯 `questions.json`、`words.json` 後重新發布：`dotnet publish -c Release -o publish`。

功能：題目練習、JSON 題庫匯入、單字卡、瀏覽器語音朗讀、中文/英文輸入測驗。題目與單字可直接編輯 `questions.json`、`words.json`；瀏覽器會把匯入資料存到本機。

題庫格式：`{"question":"題目","choices":["A","B","C","D"],"answer":0,"explanation":"解析"}`。
