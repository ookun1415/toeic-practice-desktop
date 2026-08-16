$ErrorActionPreference = 'Stop'
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$data = Get-Content -Raw toeic-vocabulary-ccby.json | ConvertFrom-Json
$count = ($data.vocabulary_by_importance.psobject.Properties | ForEach-Object { $_.Value.Count } | Measure-Object -Sum).Sum
if ($count -ne 9537) { throw "Expected 9537 words, got $count" }
$missingExamples = @($data.vocabulary_by_importance.psobject.Properties | ForEach-Object { $_.Value } | Where-Object { -not $_.examples -or -not $_.examples[0].english })
if ($missingExamples.Count) { throw "Words without an example question: $($missingExamples.english_word -join ', ')" }
$assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path 'bin\Release\net9.0-windows\win-x64\DesktopApp.dll'))
if ($assembly.GetManifestResourceNames() -notcontains 'DesktopApp.toeic-vocabulary-ccby.json') { throw 'Vocabulary resource is missing' }
$source = Get-Content -Raw Program.cs
if ($source -notmatch 'OrderByDescending\(x => x.star_rating\)' -or $source -notmatch 'ProgressData' -or $source -notmatch 'UnionWith\(LoadProgress\(\).Learned\)' -or $source -notmatch '切換單字時發音' -or $source -notmatch '音量' -or $source -notmatch 'var targetIndex = qi % words.Count' -or $source -notmatch 'hasBlank \?' -or $source -notmatch 'Regex.Replace\(target.example' -or $source -notmatch 'choices.Controls.Add\(quizTranslation\)' -or $source -notmatch 'quizTranslation.Text = \$"中文翻譯：\{translation\}"' -or $source -notmatch 'wordHeader.Controls.Add\(progressLabel\)' -or $source -notmatch 'wordControls.Controls.AddRange' -or $source -notmatch 'wordControls.Location = new Point\(28, wordHeader.Bottom \+ 10\)' -or $source -notmatch 'void LayoutQuiz\(\)' -or $source -notmatch 'quizNext.Location = new Point\(28, feedback.Bottom \+ 18\)' -or $source -notmatch 'testPrompt.MaximumSize' -or $source -notmatch 'testInput.Top = testPrompt.Bottom') { throw 'Required question-bank or responsive test layout behavior is missing' }
Write-Output "OK: $count word-specific Part 5 questions and responsive test layout"
