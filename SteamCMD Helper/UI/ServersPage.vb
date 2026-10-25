''' <summary>Catalog of all servers with details, start command and install button.</summary>
Friend Class ServersPage
    Inherits PageBase

    Private Const AllCategories As String = "Alle Kategorien"

    ' List
    Private ReadOnly listCard As New CardPanel() With {.Title = "Server"}
    Private ReadOnly searchBox As New DarkTextBox() With {.Placeholder = "Spiel oder App-ID suchen"}
    Private ReadOnly categoryCombo As New DarkComboBox()
    Private ReadOnly anonymousToggle As New ToggleSwitch() With {.Text = "Nur ohne Steam-Login"}
    Private ReadOnly serverList As New EntryListBox() With {.ItemHeight = 46, .EmptyText = "Kein Server gefunden"}
    Private ReadOnly addButton As ModernButton = MakeButton("Neuer Server per App-ID", ButtonKind.Secondary, Theme.IconAdd)

    ' Details
    Private ReadOnly detailCard As New CardPanel() With {.Title = "Details"}
    Private ReadOnly nameLabel As Label = MakeLabel("Name", small:=True)
    Private ReadOnly nameBox As New DarkTextBox()
    Private ReadOnly appIdLabel As Label = MakeLabel("Steam App-ID", small:=True)
    Private ReadOnly appIdBox As New NumberBox() With {.Minimum = 1, .Maximum = Integer.MaxValue}
    Private ReadOnly categoryLabel As Label = MakeLabel("Kategorie", small:=True)
    Private ReadOnly detailCategoryCombo As New DarkComboBox()
    Private ReadOnly betaLabel As Label = MakeLabel("Beta-Branch (optional)", small:=True)
    Private ReadOnly betaBox As New DarkTextBox() With {.Placeholder = "leer = normale Version"}
    Private ReadOnly loginToggle As New ToggleSwitch() With {.Text = "Steam-Login nötig (Spiel muss im Account sein)"}
    Private ReadOnly exeLabel As Label = MakeLabel("Startprogramm (relativ zum Serverordner)", small:=True)
    Private ReadOnly exeBox As New DarkTextBox() With {.Placeholder = "leer = nach der Installation automatisch suchen"}
    Private ReadOnly detectButton As ModernButton = MakeButton("Erkennen", ButtonKind.Secondary, Theme.IconSearch)
    Private ReadOnly argsLabel As Label = MakeLabel("Start-Parameter", small:=True)
    Private ReadOnly argsBox As New DarkTextBox()
    Private ReadOnly noteLabel As Label = MakeLabel("", small:=True)
    Private ReadOnly folderLabel As Label = MakeLabel("", small:=True)
    Private ReadOnly installButton As ModernButton = MakeButton("Installieren", ButtonKind.Primary, Theme.IconDownload)
    Private ReadOnly startButton As ModernButton = MakeButton("Server starten", ButtonKind.Secondary, Theme.IconPlay)
    Private ReadOnly folderButton As ModernButton = MakeButton("Ordner", ButtonKind.Secondary, Theme.IconFolder)
    Private ReadOnly scriptButton As ModernButton = MakeButton(".bat erstellen", ButtonKind.Secondary, Theme.IconSave)
    Private ReadOnly removeButton As ModernButton = MakeButton("Aus Liste löschen", ButtonKind.Ghost, Theme.IconDelete)

    Private selected As GameServer
    Private loadingDetails As Boolean

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Server-Katalog", "Über 170 Dedicated Server – und jeder andere per App-ID")

        categoryCombo.Items.Add(AllCategories)
        For Each category As String In Catalog.Categories
            categoryCombo.Items.Add(category)
            detailCategoryCombo.Items.Add(category)
        Next
        categoryCombo.SelectedIndex = 0

        listCard.Controls.AddRange(New Control() {searchBox, categoryCombo, anonymousToggle, serverList, addButton})
        detailCard.Controls.AddRange(New Control() {nameLabel, nameBox, appIdLabel, appIdBox, categoryLabel, detailCategoryCombo,
                                                    betaLabel, betaBox, loginToggle, exeLabel, exeBox, detectButton, argsLabel, argsBox,
                                                    noteLabel, folderLabel, installButton, startButton, folderButton, scriptButton, removeButton})
        Controls.Add(listCard)
        Controls.Add(detailCard)
        noteLabel.ForeColor = Theme.Warning

        AddHandler searchBox.TextChanged, Sub(s, e) RefreshList()
        AddHandler categoryCombo.SelectedIndexChanged, Sub(s, e) RefreshList()
        AddHandler anonymousToggle.CheckedChanged, Sub(s, e) RefreshList()
        AddHandler serverList.SelectedIndexChanged,
            Sub(s, e)
                Dim entry = TryCast(serverList.SelectedItem, ListEntry)
                If entry IsNot Nothing Then ShowDetails(DirectCast(entry.Tag, GameServer))
            End Sub
        AddHandler serverList.DoubleClick, Sub(s, e) Install()
        AddHandler addButton.Click, AddressOf AddCustomServer

        For Each box As DarkTextBox In New DarkTextBox() {nameBox, betaBox, exeBox, argsBox, appIdBox}
            AddHandler box.Leave, Sub(s, e) SaveDetails()
        Next
        AddHandler detailCategoryCombo.SelectedIndexChanged, Sub(s, e) SaveDetails()
        AddHandler loginToggle.CheckedChanged, Sub(s, e) SaveDetails()

        AddHandler installButton.Click, Sub(s, e) Install()
        AddHandler startButton.Click,
            Sub(s, e)
                SaveDetails()
                If selected IsNot Nothing Then Main.StartServer(selected)
            End Sub
        AddHandler folderButton.Click,
            Sub(s, e)
                If selected IsNot Nothing Then MainForm.OpenFolder(Data.InstallFolder(selected))
            End Sub
        AddHandler scriptButton.Click,
            Sub(s, e)
                SaveDetails()
                If selected IsNot Nothing Then Main.CreateStartScript(selected)
            End Sub
        AddHandler removeButton.Click, AddressOf RemoveCustomServer
        AddHandler detectButton.Click,
            Sub(s, e)
                If selected Is Nothing Then Return
                SaveDetails()
                Dim chosen = Main.PickStartProgram(selected)
                If chosen.Length = 0 Then Return
                exeBox.Text = chosen
                SaveDetails()
            End Sub

        RefreshList()
    End Sub

    Friend Overrides Sub OnShown()
        RefreshList()
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If detailCard Is Nothing OrElse removeButton Is Nothing Then Return

        Dim top = HeaderHeight
        Dim h = Math.Max(400, ClientSize.Height - top - Pad)
        Dim listWidth = Math.Max(360, CInt((ClientSize.Width - Pad * 2) * 0.42))
        listCard.SetBounds(Pad, top, listWidth, h)
        detailCard.SetBounds(listCard.Right + 12, top, Math.Max(380, ClientSize.Width - listCard.Right - 12 - Pad), h)

        Dim lw = listWidth - 32
        searchBox.SetBounds(16, listCard.ContentTop, lw, 34)
        categoryCombo.SetBounds(16, searchBox.Bottom + 8, (lw - 8) \ 2, 32)
        anonymousToggle.SetBounds(categoryCombo.Right + 12, searchBox.Bottom + 12, lw - categoryCombo.Width - 12, 24)
        addButton.SetBounds(16, h - 16 - 36, lw, 36)
        serverList.SetBounds(12, categoryCombo.Bottom + 10, lw + 8, addButton.Top - 10 - categoryCombo.Bottom - 10)

        Dim dw = detailCard.Width - 32
        Dim half = (dw - 12) \ 2
        Dim y = detailCard.ContentTop
        nameLabel.SetBounds(16, y, dw, 18)
        nameBox.SetBounds(16, y + 20, dw, 34) : y += 62
        appIdLabel.SetBounds(16, y, half, 18)
        appIdBox.SetBounds(16, y + 20, half, 34)
        categoryLabel.SetBounds(16 + half + 12, y, half, 18)
        detailCategoryCombo.SetBounds(16 + half + 12, y + 21, half, 32) : y += 62
        betaLabel.SetBounds(16, y, half, 18)
        betaBox.SetBounds(16, y + 20, half, 34)
        loginToggle.SetBounds(16 + half + 12, y + 25, half, 24) : y += 62
        exeLabel.SetBounds(16, y, dw, 18)
        exeBox.SetBounds(16, y + 20, dw - 128, 34)
        detectButton.SetBounds(16 + dw - 120, y + 20, 120, 34) : y += 62
        argsLabel.SetBounds(16, y, dw, 18)
        argsBox.SetBounds(16, y + 20, dw, 34) : y += 62
        noteLabel.SetBounds(16, y, dw, 36) : y += 40
        folderLabel.SetBounds(16, y, dw, 18)

        Dim by = h - 16 - 40
        installButton.SetBounds(16, by, Math.Min(200, half), 40)
        startButton.SetBounds(installButton.Right + 8, by, 150, 40)
        folderButton.SetBounds(startButton.Right + 8, by, 100, 40)
        scriptButton.SetBounds(folderButton.Right + 8, by, 130, 40)
        If scriptButton.Right > 16 + dw Then
            ' Narrow: second row
            scriptButton.SetBounds(16, by - 48, 130, 40)
        End If
        removeButton.SetBounds(16 + dw - 160, detailCard.ContentTop - 34, 160, 30)
    End Sub

    ' ---------- List ----------

    Friend Sub RefreshList()
        Dim filter = searchBox.Text.Trim()
        Dim category = If(categoryCombo.SelectedIndex > 0, CStr(categoryCombo.SelectedItem), "")
        Dim keep = selected

        serverList.BeginUpdate()
        serverList.Items.Clear()
        Dim shown = 0
        For Each server As GameServer In Data.Servers.OrderBy(Function(x) x.Name, StringComparer.CurrentCultureIgnoreCase)
            If category.Length > 0 AndAlso server.Category <> category Then Continue For
            If anonymousToggle.Checked AndAlso server.NeedsLogin Then Continue For
            If filter.Length > 0 AndAlso server.Name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) < 0 AndAlso
               server.AppId.ToString() <> filter Then Continue For

            Dim installed = Data.IsInstalled(server)
            Dim entry As New ListEntry With {
                .Title = server.Name,
                .Subtitle = $"App {server.AppId} · {server.Category}" & If(server.IsCustom, " · eigener Eintrag", If(server.FromOnline, " · online", "")),
                .Tag = server}
            If installed Then
                entry.Badge = "Installiert"
                entry.BadgeColor = Theme.Good
            ElseIf server.NeedsLogin Then
                entry.Badge = "Login"
                entry.BadgeColor = Theme.Warning
            End If
            serverList.Items.Add(entry)
            shown += 1
            If server Is keep Then serverList.SelectedIndex = serverList.Items.Count - 1
        Next
        serverList.EndUpdate()
        listCard.Subtitle = $"{shown} von {Data.Servers.Count} Servern"

        If serverList.SelectedIndex < 0 AndAlso serverList.Items.Count > 0 Then serverList.SelectedIndex = 0
        If serverList.Items.Count = 0 Then ShowDetails(Nothing)
        If selected IsNot Nothing Then UpdateInstallState()
    End Sub

    ' ---------- Details ----------

    Private Sub ShowDetails(server As GameServer)
        SaveDetails()
        selected = server
        loadingDetails = True
        Try
            Dim hasServer = server IsNot Nothing
            For Each c As Control In New Control() {nameBox, appIdBox, detailCategoryCombo, betaBox, loginToggle, exeBox, detectButton, argsBox,
                                                   installButton, startButton, folderButton, scriptButton}
                c.Enabled = hasServer
            Next
            removeButton.Visible = hasServer AndAlso server.IsCustom
            If Not hasServer Then
                detailCard.Title = "Details"
                Return
            End If

            detailCard.Title = server.Name
            nameBox.Text = server.Name
            nameBox.Enabled = server.IsCustom
            appIdBox.Value = server.AppId
            appIdBox.Enabled = server.IsCustom
            detailCategoryCombo.SelectedIndex = Math.Max(0, Array.IndexOf(Catalog.Categories, server.Category))
            betaBox.Text = server.Beta
            loginToggle.Checked = server.NeedsLogin
            exeBox.Text = server.StartExe
            argsBox.Text = server.StartArgs
            noteLabel.Text = If(server.Note.Length > 0, server.Note,
                                If(server.StartExe.Length = 0, "Startprogramm noch unbekannt – es wird nach der Installation automatisch gesucht (oder ""Erkennen"").", ""))
            UpdateInstallState()
        Finally
            loadingDetails = False
        End Try
    End Sub

    Private Sub UpdateInstallState()
        If selected Is Nothing Then Return
        Dim installed = Data.IsInstalled(selected)
        installButton.Text = If(installed, "Aktualisieren", "Installieren")
        folderLabel.Text = If(installed, "Installiert in ", "Wird installiert nach ") & Data.InstallFolder(selected)
        folderLabel.ForeColor = If(installed, Theme.Good, Theme.TextMuted)
    End Sub

    Private Sub SaveDetails()
        If loadingDetails OrElse selected Is Nothing Then Return
        Dim changed = False
        If selected.IsCustom Then
            Dim newName = nameBox.Text.Trim()
            If newName.Length > 0 AndAlso newName <> selected.Name Then
                selected.Name = newName
                detailCard.Title = newName
                changed = True
            End If
            Dim newId = CInt(appIdBox.Value)
            If newId > 0 AndAlso newId <> selected.AppId Then
                selected.AppId = newId
                changed = True
            End If
        End If
        Dim newCategory = If(detailCategoryCombo.SelectedIndex >= 0, CStr(detailCategoryCombo.SelectedItem), selected.Category)
        If newCategory <> selected.Category Then
            selected.Category = newCategory
            changed = True
        End If
        If betaBox.Text.Trim() <> selected.Beta Then
            selected.Beta = betaBox.Text.Trim()
            changed = True
        End If
        If loginToggle.Checked <> selected.NeedsLogin Then
            selected.NeedsLogin = loginToggle.Checked
            changed = True
        End If
        If exeBox.Text.Trim() <> selected.StartExe Then
            selected.StartExe = exeBox.Text.Trim()
            changed = True
        End If
        If argsBox.Text.Trim() <> selected.StartArgs Then
            selected.StartArgs = argsBox.Text.Trim()
            changed = True
        End If
        If changed Then
            Data.Save()
            UpdateInstallState()
        End If
    End Sub

    Private Sub Install()
        SaveDetails()
        If selected IsNot Nothing Then Main.InstallServers({selected})
    End Sub

    ''' <summary>Any dedicated server by App-ID – Steam provides name and start program.</summary>
    Private Sub AddCustomServer(sender As Object, e As EventArgs)
        Dim server As GameServer
        Using dialog As New AppIdDialog(Main)
            If dialog.ShowDialog(Me) <> DialogResult.OK OrElse dialog.Server Is Nothing Then Return
            server = dialog.Server
        End Using
        Data.Servers.Add(server)
        Data.Save()
        searchBox.Text = ""
        categoryCombo.SelectedIndex = 0
        selected = Nothing
        RefreshList()
        For i = 0 To serverList.Items.Count - 1
            If DirectCast(serverList.Items(i), ListEntry).Tag Is server Then serverList.SelectedIndex = i
        Next
    End Sub

    Private Sub RemoveCustomServer(sender As Object, e As EventArgs)
        If selected Is Nothing OrElse Not selected.IsCustom Then Return
        If MessageBox.Show(Me, $"""{selected.Name}"" aus der Liste entfernen? Installierte Dateien bleiben erhalten.", "Entfernen",
                           MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        Data.Servers.Remove(selected)
        selected = Nothing
        Data.Save()
        RefreshList()
    End Sub

End Class
