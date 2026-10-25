''' <summary>Dark window base for the small dialogs.</summary>
Friend Class DarkForm
    Inherits Form

    Public Sub New(title As String, width As Integer, height As Integer)
        Text = title
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New System.Drawing.Size(width, height)
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkTitleBar(Me)
    End Sub

    Protected Shared Function Caption(text As String, Optional muted As Boolean = True, Optional small As Boolean = True) As Label
        Return New Label() With {.Text = text, .AutoSize = False, .ForeColor = If(muted, Theme.TextMuted, Theme.TextMain),
                                 .Font = If(small, Theme.SmallFont, Theme.BodyFont), .BackColor = Color.Transparent}
    End Function

    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If keyData = Keys.Escape Then
            DialogResult = DialogResult.Cancel
            Close()
            Return True
        End If
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function
End Class


''' <summary>
''' Adds any dedicated server by its Steam App-ID (experimental): SteamCMD reads name,
''' Windows support and the start program from Steam, so new servers work without a catalog update.
''' </summary>
Friend Class AppIdDialog
    Inherits DarkForm

    Private ReadOnly owningForm As MainForm
    Private ReadOnly appIdBox As New NumberBox() With {.Minimum = 1, .Maximum = Integer.MaxValue, .Placeholder = "z. B. 2394010"}
    Private ReadOnly checkButton As New ModernButton() With {.Text = "Bei Steam prüfen", .Kind = ButtonKind.Primary, .IconGlyph = Theme.IconSearch}
    Private ReadOnly steamDbButton As New ModernButton() With {.Text = "App-ID auf SteamDB suchen", .Kind = ButtonKind.Secondary, .IconGlyph = Theme.IconOpen}
    Private ReadOnly resultLabel As Label = Caption("", False, False)
    Private ReadOnly nameBox As New DarkTextBox()
    Private ReadOnly categoryCombo As New DarkComboBox()
    Private ReadOnly exeBox As New DarkTextBox() With {.Placeholder = "leer = nach der Installation automatisch suchen"}
    Private ReadOnly argsBox As New DarkTextBox()
    Private ReadOnly loginToggle As New ToggleSwitch() With {.Text = "Steam-Login nötig (Spiel muss im Account sein)"}
    Private ReadOnly addButton As New ModernButton() With {.Text = "Zum Katalog hinzufügen", .Kind = ButtonKind.Primary, .IconGlyph = Theme.IconAdd}
    Private ReadOnly cancelButton As New ModernButton() With {.Text = "Abbrechen", .Kind = ButtonKind.Secondary}

    ''' <summary>The new server after "Hinzufügen".</summary>
    Public Property Server As GameServer

    Public Sub New(owner As MainForm)
        MyBase.New("Neuer Server per App-ID (experimentell)", 640, 560)
        owningForm = owner
        For Each category As String In Catalog.Categories
            categoryCombo.Items.Add(category)
        Next
        categoryCombo.SelectedItem = Catalog.Other

        Dim intro = Caption("Funktioniert mit jedem Dedicated Server auf Steam – auch mit ganz neuen, die noch nicht im Katalog stehen. " &
                            "Gib die App-ID des Servers ein (nicht die des Spiels!) und lass sie von Steam prüfen.", True, False)
        intro.SetBounds(24, 18, 592, 40)
        Dim idCaption = Caption("Steam App-ID des Dedicated Servers")
        idCaption.SetBounds(24, 68, 300, 17)
        appIdBox.SetBounds(24, 87, 160, 34)
        checkButton.SetBounds(194, 87, 180, 34)
        steamDbButton.SetBounds(384, 87, 232, 34)
        resultLabel.SetBounds(24, 132, 592, 92)
        resultLabel.ForeColor = Theme.TextMuted
        resultLabel.Text = "Tipp: Auf SteamDB nach ""<Spiel> Dedicated Server"" suchen – die Zahl bei ""App ID"" ist die richtige."

        Dim nameCaption = Caption("Name im Katalog")
        nameCaption.SetBounds(24, 232, 380, 17)
        nameBox.SetBounds(24, 251, 380, 34)
        Dim categoryCaption = Caption("Kategorie")
        categoryCaption.SetBounds(416, 232, 200, 17)
        categoryCombo.SetBounds(416, 252, 200, 32)
        Dim exeCaption = Caption("Startprogramm (relativ zum Serverordner)")
        exeCaption.SetBounds(24, 297, 592, 17)
        exeBox.SetBounds(24, 316, 592, 34)
        Dim argsCaption = Caption("Start-Parameter")
        argsCaption.SetBounds(24, 362, 592, 17)
        argsBox.SetBounds(24, 381, 592, 34)
        loginToggle.SetBounds(24, 429, 592, 24)
        addButton.SetBounds(616 - 220, 500, 220, 40)
        cancelButton.SetBounds(addButton.Left - 130, 500, 120, 40)

        Controls.AddRange(New Control() {intro, idCaption, appIdBox, checkButton, steamDbButton, resultLabel, nameCaption, nameBox,
                                         categoryCaption, categoryCombo, exeCaption, exeBox, argsCaption, argsBox, loginToggle, addButton, cancelButton})

        AddHandler checkButton.Click, Async Sub(s, e) Await CheckAsync()
        AddHandler steamDbButton.Click,
            Sub(s, e)
                Dim term = If(nameBox.Text.Trim().Length > 0, nameBox.Text.Trim(), "") & " Dedicated Server"
                Try
                    Process.Start(New ProcessStartInfo("https://steamdb.info/search/?a=app&q=" & Uri.EscapeDataString(term.Trim())) With {.UseShellExecute = True})
                Catch
                End Try
            End Sub
        AddHandler addButton.Click, AddressOf Add_Click
        AddHandler cancelButton.Click,
            Sub(s, e)
                DialogResult = DialogResult.Cancel
                Close()
            End Sub
    End Sub

    Private Async Function CheckAsync() As Task
        Dim appId = CInt(appIdBox.Value)
        If appIdBox.Text.Trim().Length = 0 OrElse appId <= 0 Then
            ShowResult("Bitte zuerst eine App-ID eingeben.", Theme.Warning)
            Return
        End If
        If owningForm.Runner.IsRunning Then
            ShowResult("SteamCMD arbeitet gerade (Installation läuft). Bitte warten, bis es fertig ist.", Theme.Warning)
            Return
        End If
        If Not Await owningForm.EnsureSteamCmdAsync() Then Return
        checkButton.Enabled = False
        ShowResult("Frage Steam nach App " & appId & " … (beim ersten Mal bis zu einer Minute)", Theme.TextMuted)
        Try
            Dim info = Await SteamAppInfo.QueryAsync(owningForm.Data.SteamCmdExe, appId)
            If Not info.Found Then
                ShowResult(info.ErrorText & Environment.NewLine & "Du kannst den Server trotzdem hinzufügen und die Installation versuchen.", Theme.Warning)
                Return
            End If
            Dim lines As New List(Of String) From {
                "✓ " & info.Name & If(info.AppType.Length > 0, "  (" & info.AppType & ")", "")}
            If Not info.HasWindows Then
                lines.Add("⚠ Laut Steam gibt es diesen Server nur für Linux – unter Windows lässt er sich vermutlich nicht starten.")
            End If
            Dim launch = info.WindowsLaunch
            If launch IsNot Nothing AndAlso launch.Executable.Length > 0 Then
                lines.Add("Startprogramm laut Steam: " & launch.Executable & If(launch.Arguments.Length > 0, " " & launch.Arguments, ""))
                exeBox.Text = launch.Executable
                argsBox.Text = launch.Arguments
            Else
                lines.Add("Kein Startprogramm hinterlegt – es wird nach der Installation automatisch gesucht.")
            End If
            If nameBox.Text.Trim().Length = 0 Then nameBox.Text = info.ShortName
            ShowResult(String.Join(Environment.NewLine, lines), If(info.HasWindows, Theme.Good, Theme.Warning))
        Catch ex As Exception
            ShowResult("Prüfen fehlgeschlagen: " & ex.Message, Theme.Danger)
        Finally
            checkButton.Enabled = True
        End Try
    End Function

    Private Sub ShowResult(text As String, textColor As Color)
        resultLabel.Text = text
        resultLabel.ForeColor = textColor
    End Sub

    Private Sub Add_Click(sender As Object, e As EventArgs)
        Dim appId = CInt(appIdBox.Value)
        If appIdBox.Text.Trim().Length = 0 OrElse appId <= 0 Then
            ShowResult("Bitte eine App-ID eingeben.", Theme.Warning)
            appIdBox.Focus()
            Return
        End If
        Dim existing = owningForm.Data.Servers.FirstOrDefault(Function(x) x.AppId = appId)
        If existing IsNot Nothing Then
            ShowResult("Diese App-ID ist schon im Katalog: " & existing.Name, Theme.Warning)
            Return
        End If
        Server = New GameServer With {
            .Name = If(nameBox.Text.Trim().Length > 0, nameBox.Text.Trim(), "App " & appId),
            .AppId = appId,
            .Category = If(categoryCombo.SelectedItem IsNot Nothing, CStr(categoryCombo.SelectedItem), Catalog.Other),
            .StartExe = exeBox.Text.Trim(),
            .StartArgs = argsBox.Text.Trim(),
            .NeedsLogin = loginToggle.Checked,
            .IsCustom = True}
        DialogResult = DialogResult.OK
        Close()
    End Sub

End Class


''' <summary>Lets the user pick one entry from a list (e.g. the start program).</summary>
Friend Class ChoiceDialog
    Inherits DarkForm

    Private ReadOnly choiceList As New EntryListBox() With {.ItemHeight = 40}

    Public ReadOnly Property Choice As String
        Get
            Dim entry = TryCast(choiceList.SelectedItem, ListEntry)
            Return If(entry Is Nothing, "", CStr(entry.Tag))
        End Get
    End Property

    Public Sub New(title As String, message As String, choices As IEnumerable(Of String))
        MyBase.New(title, 600, 460)
        Dim info = Caption(message, True, False)
        info.SetBounds(20, 16, 560, 40)
        choiceList.SetBounds(20, 62, 560, 320)
        Dim okButton As New ModernButton() With {.Text = "Übernehmen", .Kind = ButtonKind.Primary}
        Dim cancelButton As New ModernButton() With {.Text = "Abbrechen", .Kind = ButtonKind.Secondary}
        okButton.SetBounds(440, 400, 140, 40)
        cancelButton.SetBounds(310, 400, 120, 40)
        Controls.AddRange(New Control() {info, choiceList, okButton, cancelButton})
        For Each item As String In choices
            choiceList.Items.Add(New ListEntry With {.Title = Path.GetFileName(item), .Subtitle = item, .Tag = item})
        Next
        If choiceList.Items.Count > 0 Then choiceList.SelectedIndex = 0
        AddHandler okButton.Click, Sub(s, e) Accept()
        AddHandler choiceList.DoubleClick, Sub(s, e) Accept()
        AddHandler cancelButton.Click,
            Sub(s, e)
                DialogResult = DialogResult.Cancel
                Close()
            End Sub
    End Sub

    ''' <summary>Shows the dialog; returns the choice or "" when cancelled.</summary>
    Public Shared Function Ask(owner As IWin32Window, title As String, message As String, choices As IEnumerable(Of String)) As String
        Using dialog As New ChoiceDialog(title, message, choices)
            Return If(dialog.ShowDialog(owner) = DialogResult.OK, dialog.Choice, "")
        End Using
    End Function
    Private Sub Accept()
        DialogResult = If(Choice.Length > 0, DialogResult.OK, DialogResult.Cancel)
        Close()
    End Sub
End Class
