using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;

record Word(string en, string zh, string example, string exampleZh, string category);
record SourceExample(string english, string chinese);
record SourceWord(string english_word, string chinese_definition, int star_rating, string category, List<SourceExample>? examples);
record VocabularyData(Dictionary<string, List<SourceWord>> vocabulary_by_importance);
record ProgressData(int Cursor, HashSet<string> Learned);

static class Program { [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); } }

class MainForm : Form
{
    static readonly Color Navy = Color.FromArgb(21, 42, 74), Blue = Color.FromArgb(37, 99, 235), Canvas = Color.FromArgb(244, 247, 251), Ink = Color.FromArgb(30, 41, 59), Muted = Color.FromArgb(100, 116, 139);
    readonly List<Word> words; readonly HashSet<string> learned; readonly string progressPath;
    int wi, qi, testIndex; string mode = "en";
    readonly TabControl tabs = new() { Dock = DockStyle.Fill, DrawMode = TabDrawMode.OwnerDrawFixed, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(150, 42), Padding = new Point(16, 8) }; readonly Label feedback = new() { AutoSize = true }, quizTranslation = new() { AutoSize = true }; readonly Button quizNext = new() { Text = "下一題", AutoSize = true };
    readonly FlowLayoutPanel choices = new() { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    readonly FlowLayoutPanel wordHeader = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(800, 0) }, wordControls = new() { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(800, 0) };
    readonly Label wordLabel = new(), translationLabel = new(), exampleLabel = new(), progressLabel = new(), statsLabel = new(), volumeLabel = new(), startHint = new() { Text = "從第", AutoSize = true };
    readonly TextBox typeInput = new() { Width = 440, Font = new Font("Segoe UI", 16), TextAlign = HorizontalAlignment.Center };
    readonly Button speakButton = new() { Text = "🔊 發音", AutoSize = true }, previousButton = new() { Text = "← 上一個", AutoSize = true }, skipButton = new() { Text = "跳過 →", AutoSize = true }, jumpButton = new() { Text = "開始", AutoSize = true }, resumeButton = new() { Text = "接續上次", AutoSize = true };
    readonly NumericUpDown startNumber = new() { Minimum = 1, Width = 80 };
    readonly CheckBox autoSpeak = new() { Text = "切換單字時發音", Checked = true, AutoSize = true };
    readonly TrackBar volume = new() { Minimum = 0, Maximum = 100, Value = 80, TickFrequency = 10, Width = 145 };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    readonly DateTime started = DateTime.Now; int totalKeys, correctKeys, completedWords;
    readonly Label testPrompt = new() { AutoSize = true }, testNote = new() { AutoSize = true }, testResult = new() { AutoSize = true };
    readonly TextBox testInput = new() { Width = 430 };
    readonly Button testCheck = new() { Text = "檢查", AutoSize = true }, testNext = new() { Text = "下一題", AutoSize = true };

    public MainForm()
    {
        Text = "多益單字練習"; Width = 920; Height = 680; MinimumSize = new Size(700, 520); StartPosition = FormStartPosition.CenterScreen; BackColor = Canvas; Font = new Font("Microsoft JhengHei", 10);
        words = LoadWords(); progressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ToeicPractice", "progress.json");
        var saved = LoadProgress(); learned = saved.Learned; wi = Math.Clamp(saved.Cursor, 0, Math.Max(0, words.Count - 1)); testIndex = wi;
        tabs.TabPages.Add(QuizPage()); tabs.TabPages.Add(WordsPage()); tabs.TabPages.Add(TestPage()); tabs.DrawItem += DrawTab; tabs.SelectedIndexChanged += (_, _) => { if (tabs.SelectedIndex == 0) RenderQuiz(); if (tabs.SelectedIndex == 1) RenderWord(true); if (tabs.SelectedIndex == 2) RenderTest(); };
        FormClosing += (_, _) => SaveProgress(); Controls.Add(tabs); timer.Tick += (_, _) => UpdateStats(); timer.Start(); RenderQuiz(); RenderWord(false); RenderTest();
    }

    T? LoadJson<T>(string file) { try { return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file))); } catch { return default; } }
    T? LoadEmbeddedJson<T>(string name) { using var stream = typeof(Program).Assembly.GetManifestResourceStream(name); return stream is null ? default : JsonSerializer.Deserialize<T>(stream); }
    List<Word> LoadWords()
    {
        var data = LoadEmbeddedJson<VocabularyData>("DesktopApp.toeic-vocabulary-ccby.json");
        return data?.vocabulary_by_importance.Values.SelectMany(group => group).OrderByDescending(x => x.star_rating).Select(x =>
        {
            var example = x.examples?.FirstOrDefault(e => Regex.IsMatch(e.english, $@"\b{Regex.Escape(x.english_word)}\b", RegexOptions.IgnoreCase)) ?? x.examples?.FirstOrDefault();
            return new Word(x.english_word, x.chinese_definition, example?.english ?? "", example?.chinese ?? "", x.category);
        }).ToList() ?? LoadJson<List<Word>>("words.json") ?? [];
    }
    ProgressData LoadProgress()
    {
        try { return JsonSerializer.Deserialize<ProgressData>(File.ReadAllText(progressPath)) ?? new(0, []); } catch { return new(0, []); }
    }
    void SaveProgress()
    {
        learned.UnionWith(LoadProgress().Learned); Directory.CreateDirectory(Path.GetDirectoryName(progressPath)!); File.WriteAllText(progressPath, JsonSerializer.Serialize(new ProgressData(wi, learned)));
    }
    List<int> StudiedIndexes() => words.Select((word, index) => (word, index)).Where(x => learned.Contains(x.word.en)).Select(x => x.index).ToList();
    TabPage Page(string title) => new(title) { Padding = new Padding(32), AutoScroll = true, BackColor = Canvas, ForeColor = Ink };
    void DrawTab(object? sender, DrawItemEventArgs e)
    {
        var selected = e.Index == tabs.SelectedIndex; using var background = new SolidBrush(selected ? Blue : Navy); using var font = new Font("Microsoft JhengHei", 10, selected ? FontStyle.Bold : FontStyle.Regular);
        e.Graphics.FillRectangle(background, e.Bounds); TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, font, e.Bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(29, 78, 216) : Color.FromArgb(219, 234, 254); button.BackColor = primary ? Blue : Color.White; button.ForeColor = primary ? Color.White : Blue; button.Padding = new Padding(8, 4, 8, 4); button.Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold);
    }
    static void StyleInput(TextBox input) { input.BorderStyle = BorderStyle.FixedSingle; input.BackColor = Color.White; input.ForeColor = Ink; }

    TabPage QuizPage()
    {
        var p = Page("題目練習"); StyleButton(quizNext, true); feedback.Font = new Font("Microsoft JhengHei", 11, FontStyle.Bold); feedback.MaximumSize = new Size(760, 0); feedback.ForeColor = Blue; quizTranslation.Font = new Font("Microsoft JhengHei", 11); quizTranslation.MaximumSize = new Size(760, 0); quizTranslation.ForeColor = Muted; quizTranslation.Visible = false; quizNext.Click += (_, _) => { qi++; RenderQuiz(); };
        p.Controls.AddRange([choices, feedback, quizNext]); choices.Location = new Point(28, 50); return p;
    }
    void LayoutQuiz()
    {
        choices.PerformLayout(); feedback.Location = new Point(28, choices.Bottom + 18); quizNext.Location = new Point(28, feedback.Bottom + 18);
    }
    void RenderQuiz()
    {
        choices.Controls.Clear(); feedback.Text = ""; quizTranslation.Text = ""; quizTranslation.Visible = false; if (words.Count == 0) return;
        var targetIndex = qi % words.Count; var target = words[targetIndex]; var unfamiliar = !learned.Contains(target.en); var pattern = $@"\b{Regex.Escape(target.en)}\b"; var hasBlank = Regex.IsMatch(target.example, pattern, RegexOptions.IgnoreCase); var prompt = hasBlank ? Regex.Replace(target.example, pattern, "_____", RegexOptions.IgnoreCase) : $"{target.example}\n\nWhich word best matches 「{target.zh}」?";
        choices.Controls.Add(new Label { Text = $"TOEIC 風格單字題　{targetIndex + 1:N0} / {words.Count:N0}\n{(unfamiliar ? "⚠ 這題的單字尚未學過" : "✓ 已學過的單字")}\n\n{prompt}", AutoSize = true, MaximumSize = new Size(760, 0), Font = new Font("Microsoft JhengHei", 15, FontStyle.Bold), ForeColor = Ink, Padding = new Padding(0, 8, 0, 22) }); choices.Controls.Add(quizTranslation);
        var options = new List<int> { targetIndex }; var random = new Random(targetIndex); while (options.Count < 4) { var candidate = random.Next(words.Count); if (!options.Contains(candidate)) options.Add(candidate); }
        var translation = string.IsNullOrWhiteSpace(target.exampleZh) ? target.zh : target.exampleZh; var shuffled = options.OrderBy(_ => random.Next()).ToList(); for (var optionNumber = 0; optionNumber < shuffled.Count; optionNumber++) { var option = shuffled[optionNumber]; var button = new Button { Text = $"{(char)('A' + optionNumber)}. {words[option].en}", AutoSize = true, Tag = option, MinimumSize = new Size(360, 42), TextAlign = ContentAlignment.MiddleLeft }; StyleButton(button); button.Click += (_, _) => { var correct = (int)button.Tag! == targetIndex; button.BackColor = correct ? Color.FromArgb(220, 252, 231) : Color.FromArgb(254, 226, 226); button.ForeColor = correct ? Color.FromArgb(22, 101, 52) : Color.FromArgb(153, 27, 27); quizTranslation.Text = $"中文翻譯：{translation}"; quizTranslation.Visible = true; feedback.Text = correct ? $"答對了！「{target.en}」：{target.zh}" : $"答案：{target.en}　{target.zh}"; choices.PerformLayout(); LayoutQuiz(); }; choices.Controls.Add(button); } LayoutQuiz();
    }

    TabPage WordsPage()
    {
        var p = Page("鍵盤背單字");
        wordLabel.Font = new Font("Segoe UI", 36, FontStyle.Bold); wordLabel.AutoSize = true; wordLabel.ForeColor = Blue;
        translationLabel.Font = new Font("Microsoft JhengHei", 16, FontStyle.Bold); translationLabel.AutoSize = true; translationLabel.MaximumSize = new Size(800, 0); translationLabel.ForeColor = Ink;
        exampleLabel.Font = new Font("Segoe UI", 11); exampleLabel.AutoSize = true; exampleLabel.MaximumSize = new Size(800, 0); exampleLabel.ForeColor = Muted;
        progressLabel.AutoSize = true; progressLabel.ForeColor = Blue; progressLabel.Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold); statsLabel.AutoSize = true; statsLabel.Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold); statsLabel.ForeColor = Ink; volumeLabel.AutoSize = true; volumeLabel.ForeColor = Muted;
        speakButton.Click += (_, _) => Speak(words[wi].en, "en-US"); previousButton.Click += (_, _) => MoveWord(wi - 1, true); skipButton.Click += (_, _) => MoveWord(wi + 1, true);
        jumpButton.Click += (_, _) => MoveWord((int)startNumber.Value - 1, true); resumeButton.Click += (_, _) => MoveWord(LoadProgress().Cursor, true); volume.ValueChanged += (_, _) => volumeLabel.Text = $"音量 {volume.Value}%";
        StyleButton(speakButton, true); StyleButton(previousButton); StyleButton(skipButton); StyleButton(jumpButton, true); StyleButton(resumeButton); StyleInput(typeInput); typeInput.KeyPress += TypeKeyPress; typeInput.TextChanged += (_, _) => CheckWordComplete();
        volumeLabel.Text = $"音量 {volume.Value}%"; wordHeader.Controls.Add(progressLabel); wordControls.Controls.AddRange([startHint, startNumber, jumpButton, resumeButton, volume, volumeLabel]); p.Controls.AddRange([wordHeader, wordControls, wordLabel, translationLabel, exampleLabel, typeInput, speakButton, previousButton, skipButton, autoSpeak, statsLabel]); wordHeader.Location = new Point(28, 28);
        return p;
    }
    void MoveWord(int index, bool readAloud)
    {
        wi = Math.Clamp(index, 0, Math.Max(0, words.Count - 1)); SaveProgress(); RenderWord(readAloud);
    }
    void RenderWord(bool readAloud)
    {
        if (words.Count == 0) { wordLabel.Text = "尚無單字"; return; }
        var w = words[wi]; startNumber.Maximum = words.Count; startNumber.Value = wi + 1; progressLabel.Text = $"TOEIC 單字庫　{wi + 1:N0} / {words.Count:N0}　·　{w.category}　·　已學 {learned.Count:N0}"; wordLabel.Text = w.en; translationLabel.Text = w.zh; exampleLabel.Text = string.IsNullOrWhiteSpace(w.example) ? "" : $"例句：{w.example}\n{w.exampleZh}";
        wordHeader.PerformLayout(); wordControls.Location = new Point(28, wordHeader.Bottom + 10); wordControls.PerformLayout(); wordLabel.Location = new Point(28, wordControls.Bottom + 20); translationLabel.Top = wordLabel.Bottom + 12; exampleLabel.Top = translationLabel.Bottom + 16; typeInput.Top = exampleLabel.Bottom + 32; speakButton.Location = new Point(typeInput.Right + 12, typeInput.Top); previousButton.Location = new Point(28, typeInput.Bottom + 16); skipButton.Location = new Point(previousButton.Right + 12, previousButton.Top); autoSpeak.Location = new Point(skipButton.Right + 18, previousButton.Top + 5); statsLabel.Location = new Point(28, previousButton.Bottom + 52);
        typeInput.Clear(); typeInput.BackColor = Color.White; if (readAloud && autoSpeak.Checked) Speak(w.en, "en-US"); UpdateStats(); if (tabs.SelectedIndex == 1) typeInput.Focus();
    }
    void TypeKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (words.Count == 0 || char.IsControl(e.KeyChar)) return;
        var target = words[wi].en; var position = typeInput.Text.Length; totalKeys++;
        if (position >= target.Length || char.ToLowerInvariant(e.KeyChar) != char.ToLowerInvariant(target[position])) { e.Handled = true; typeInput.Clear(); typeInput.BackColor = Color.MistyRose; UpdateStats(); return; }
        correctKeys++; typeInput.BackColor = Color.White; UpdateStats();
    }
    void CheckWordComplete()
    {
        if (words.Count > 0 && typeInput.Text.Equals(words[wi].en, StringComparison.OrdinalIgnoreCase)) { learned.Add(words[wi].en); completedWords++; wi = (wi + 1) % words.Count; SaveProgress(); RenderWord(true); }
    }
    void UpdateStats()
    {
        var elapsed = DateTime.Now - started; var minutes = Math.Max(elapsed.TotalMinutes, 1d / 60); var accuracy = totalKeys == 0 ? 100 : 100d * correctKeys / totalKeys;
        statsLabel.Text = $"時間 {elapsed:mm\\:ss}     輸入 {totalKeys:N0} 鍵     速度 {(correctKeys / minutes):0} CPM     正確 {correctKeys:N0}     正確率 {accuracy:0.0}%     完成 {completedWords:N0} 詞";
    }

    TabPage TestPage()
    {
        var p = Page("中文／英文測驗");
        var en = new RadioButton { Text = "中文 → 英文", Checked = true, AutoSize = true, ForeColor = Blue, Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold) }; var zh = new RadioButton { Text = "英文 → 中文", AutoSize = true, ForeColor = Blue, Font = new Font("Microsoft JhengHei", 10, FontStyle.Bold) }; StyleButton(testCheck, true); StyleButton(testNext); StyleInput(testInput); testPrompt.Font = new Font("Microsoft JhengHei", 16, FontStyle.Bold); testPrompt.MaximumSize = new Size(760, 0); testPrompt.ForeColor = Blue; testNote.ForeColor = Muted; testResult.Font = new Font("Microsoft JhengHei", 11, FontStyle.Bold); testResult.ForeColor = Blue; en.CheckedChanged += (_, _) => { if (en.Checked) { mode = "en"; RenderTest(); } }; zh.CheckedChanged += (_, _) => { if (zh.Checked) { mode = "zh"; RenderTest(); } };
        testCheck.Click += (_, _) => { if (words.Count == 0) return; var expected = mode == "en" ? words[testIndex].en : words[testIndex].zh; testResult.Text = testInput.Text.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase) ? "答對了！" : $"答案：{expected}"; };
        testNext.Click += (_, _) => RenderTest(); p.Controls.AddRange([en, zh, testNote, testPrompt, testInput, testCheck, testNext, testResult]); en.Location = new Point(28, 28); zh.Location = new Point(145, 28); testNote.Location = new Point(28, 78); testPrompt.Location = new Point(28, 115); return p;
    }
    void RenderTest()
    {
        if (words.Count == 0) return; var studied = StudiedIndexes(); testIndex = studied.Count == 0 ? wi : studied[Random.Shared.Next(studied.Count)]; var unfamiliar = !learned.Contains(words[testIndex].en);
        testNote.Text = unfamiliar ? "⚠ 尚未學過：先到鍵盤背單字完成它。" : $"✓ 已學過的單字 · 已學 {studied.Count:N0} 詞"; testPrompt.Text = mode == "en" ? words[testIndex].zh : words[testIndex].en; testInput.Top = testPrompt.Bottom + 22; testCheck.Location = new Point(testInput.Right + 12, testInput.Top - 2); testNext.Location = new Point(28, testInput.Bottom + 18); testResult.Location = new Point(28, testNext.Bottom + 18); testInput.Clear(); testResult.Text = "";
    }
    void Speak(string text, string lang)
    {
        var safe = text.Replace("'", "''"); Process.Start(new ProcessStartInfo("powershell", $"-NoProfile -Command \"Add-Type -AssemblyName System.Speech; $s=New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Volume={volume.Value}; try {{$s.SelectVoiceByHints([System.Speech.Synthesis.VoiceGender]::NotSet,[System.Speech.Synthesis.VoiceAge]::NotSet,0,'{lang}')}} catch {{}}; $s.Speak('{safe}')\"") { CreateNoWindow = true, UseShellExecute = false });
    }
}
