''' <summary>Live output of SteamCMD with progress and an input line for Steam Guard codes.</summary>
Friend Class ConsolePage
    Inherits PageBase

    Private ReadOnly card As New CardPanel() With {.Title = "SteamCMD", .Subtitle = "Noch nichts gestartet"}
    Private ReadOnly progressBar As New SlimProgress()
    Private ReadOnly progressLabel As Label = MakeLabel("", small:=True)
    Private ReadOnly output As New ConsoleBox()
    Private ReadOnly inputBox As New DarkTextBox() With {.Placeholder = "Eingabe an SteamCMD, z. B. Steam-Guard-Code"}
    Private ReadOnly sendButton As ModernButton = MakeButton("Senden", ButtonKind.Secondary)
    Private ReadOnly cancelButton As ModernButton = MakeButton("Abbrechen", ButtonKind.Danger)
    Private ReadOnly clearButton As ModernButton = MakeButton("Leeren", ButtonKind.Ghost, Theme.IconDelete)

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Konsole", "Was SteamCMD gerade macht – bei Steam Guard hier den Code eingeben")

        card.Controls.AddRange(New Control() {progressBar, progressLabel, output, inputBox, sendButton, cancelButton, clearButton})
        Controls.Add(card)
        progressBar.Visible = False
        cancelButton.Enabled = False

        AddHandler Main.Runner.OutputLine, AddressOf Runner_OutputLine
        AddHandler Main.Runner.ProgressChanged, AddressOf Runner_ProgressChanged
        AddHandler Main.Runner.RunningChanged,
            Sub()
                cancelButton.Enabled = Main.Runner.IsRunning
                progressBar.Visible = Main.Runner.IsRunning
            End Sub
        AddHandler sendButton.Click, Sub(s, e) SendInput()
        AddHandler inputBox.KeyDown,
            Sub(s, e)
                If e.KeyCode = Keys.Enter Then
                    e.SuppressKeyPress = True
                    SendInput()
                End If
            End Sub
        AddHandler cancelButton.Click,
            Sub(s, e)
                If MessageBox.Show(Me, "SteamCMD abbrechen? Halb geladene Server lassen sich später einfach fortsetzen.", "Abbrechen",
                                   MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                    Main.Runner.Cancel()
                End If
            End Sub
        AddHandler clearButton.Click, Sub(s, e) output.Clear()
        output.AppendLine("Hier erscheint die Ausgabe von SteamCMD.", Theme.TextDim)
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If card Is Nothing OrElse clearButton Is Nothing Then Return
        Dim top = HeaderHeight
        Dim w = ClientSize.Width - Pad * 2
        Dim h = Math.Max(300, ClientSize.Height - top - Pad)
        card.SetBounds(Pad, top, w, h)

        Dim cw = w - 32
        clearButton.SetBounds(w - 16 - 90, 12, 90, 30)
        progressLabel.SetBounds(16, card.ContentTop, cw, 18)
        progressBar.SetBounds(16, card.ContentTop + 22, cw, 6)
        cancelButton.SetBounds(16 + cw - 120, h - 16 - 34, 120, 34)
        sendButton.SetBounds(cancelButton.Left - 8 - 90, h - 16 - 34, 90, 34)
        inputBox.SetBounds(16, h - 16 - 34, sendButton.Left - 8 - 16, 34)
        output.SetBounds(16, card.ContentTop + 38, cw, inputBox.Top - 12 - card.ContentTop - 38)
    End Sub

    ' ---------- Called by the main form ----------

    Friend Sub BeginJob(title As String)
        card.Title = title
        card.Subtitle = "Läuft …"
        progressBar.Value = 0
        progressLabel.Text = "Starte SteamCMD (beim ersten Start aktualisiert es sich selbst) …"
        progressLabel.ForeColor = Theme.TextMuted
        output.AppendLine("", Theme.TextDim)
        output.AppendLine("===== " & title & " =====", Theme.Accent)
    End Sub

    Friend Sub EndJob(title As String, success As Boolean, summary As String)
        card.Subtitle = If(success, "Fertig", "Fehlgeschlagen")
        progressBar.Value = If(success, 100, progressBar.Value)
        progressLabel.Text = If(success, title & " ist auf dem neuesten Stand.", summary)
        progressLabel.ForeColor = If(success, Theme.Good, Theme.Danger)
        WriteLine(If(success, "✔ " & title & ": fertig", "✖ " & title & ": " & summary), If(success, LineKind.Success, LineKind.Error))
    End Sub

    Friend Sub WriteLine(text As String, kind As LineKind)
        Dim lineColor As Color
        Select Case kind
            Case LineKind.Info : lineColor = Theme.TextDim
            Case LineKind.Success : lineColor = Theme.Good
            Case LineKind.Warning : lineColor = Theme.Warning
            Case LineKind.Error : lineColor = Theme.Danger
            Case LineKind.Prompt : lineColor = Theme.Accent
            Case Else : lineColor = Theme.TextMain
        End Select
        output.AppendLine(text, lineColor)
    End Sub

    ' ---------- Runner events ----------

    Private Sub Runner_OutputLine(text As String, kind As LineKind)
        WriteLine(text, kind)
        If kind = LineKind.Prompt Then
            progressLabel.Text = "SteamCMD wartet auf eine Eingabe: " & text
            progressLabel.ForeColor = Theme.Warning
            inputBox.UsePassword = text.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
            inputBox.Focus()
            If Not Visible Then Main.ShowPage(Me)
        End If
    End Sub

    Private Sub Runner_ProgressChanged(percent As Double, status As String)
        progressBar.Value = percent
        progressLabel.Text = $"{status} … {percent:0.0} %"
        progressLabel.ForeColor = Theme.TextMuted
    End Sub

    Private Sub SendInput()
        Dim value = inputBox.Text
        If value.Length = 0 Then Return
        Main.Runner.SendInput(value)
        WriteLine("> " & If(inputBox.UsePassword, "********", value), LineKind.Info)
        inputBox.Text = ""
        inputBox.UsePassword = False
    End Sub

End Class
