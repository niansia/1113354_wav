# WAV 音效播放器 Pro Max

這是一個使用 C# 與 Windows Forms (WinForms) 開發的 WAV 音效播放器，利用 Windows 內建的 MCI (Media Control Interface) 播放音訊，無需安裝額外的 NuGet 套件。

## 功能特色 (Features)

1. **播放清單管理**
   - 支援加入單一/多個 WAV 檔案
   - 支援加入整個資料夾 (自動掃描子資料夾內的 WAV 檔)
   - 支援直接拖曳 WAV 檔案或資料夾至播放清單
   - 支援儲存 (Save) 與載入 (Load) M3U / TXT 格式的播放清單
   - 支援移除選取項目及清空清單

2. **播放控制**
   - 支援播放、暫停、停止功能
   - 支援上一首 (Previous)、下一首 (Next) 
   - 支援循環模式：不循環、單曲循環、清單循環
   - 支援隨機播放 (Shuffle)
   - 支援從進度條拖曳以快轉/倒轉 (Seek)

3. **音訊調整**
   - 支援音量調整 (0% ~ 100%)
   - 支援播放速度/倍速調整 (50% ~ 200%)

4. **WAV 資訊解析**
   - 讀取 WAV 檔標頭 (Header) 解析出音訊格式
   - 顯示聲道數 (Channels)、取樣率 (Sample Rate) 及位元深度 (Bits Per Sample)

5. **快捷鍵支援**
   - `Space` (空白鍵)：暫停 / 繼續播放
   - `Right` (右方向鍵)：快轉 5 秒
   - `Left` (左方向鍵)：倒退 5 秒
   - `N`：切換至下一首
   - `P`：切換至上一首
   - `Delete`：移除播放清單中選取的項目

## 開發環境
- .NET Framework 4.7.2
- C# 7.3

## 如何執行
以 Visual Studio 或相容的 IDE 開啟專案並建置即可執行。
