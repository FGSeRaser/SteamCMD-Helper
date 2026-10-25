''' <summary>SteamCMD location and download, install folder, Steam login.</summary>
Friend Class SettingsPage
    Inherits PageBase

    ' SteamCMD
    Private ReadOnly steamCard As New CardPanel() With {.Title = "SteamCMD"}
    Private ReadOnly steamFolderBox As New DarkTextBox() With {.ReadOnly = True}
    Private ReadOnly steamBrowse As ModernButton = MakeButton("…", ButtonKind.Secondary)
    Private ReadOnly steamStatus As Label = MakeLabel("", small:=True)
    Private ReadOnly downloadButton As ModernButton = MakeButton("SteamCMD herunterladen", ButtonKind.Primary, Theme.IconDownload)
    Private ReadOnly selfUpdateButton As ModernButton = MakeButton("Aktualisieren", ButtonKind.Secondary, Theme.IconRefresh)
    Private ReadOnly downloadProgress As New SlimProgress()

    ' Install folder
    Private ReadOnly folderCard As New CardPanel() With {.Title = "Server-Ordner", .Subtitle = "Jeder Server bekommt hier einen eigenen Unterordner"}
    Private ReadOnly rootBox As New DarkTextBox() With {.ReadOnly = True}
    Private ReadOnly rootBrowse As ModernButton = MakeButton("…", ButtonKind.Secondary)
    Private ReadOnly validateToggle As New ToggleSwitch() With {.Text = "Dateien nach dem Download prüfen (validate)"}

    ' Steam login
    Private ReadOnly loginCard As New CardPanel() With {.Title = "Steam-Login", .Subtitle = "Nur für Server, deren Spiel du besitzen musst"}
    Private ReadOnly userLabel As Label = MakeLabel("Steam-Accountname", small:=True)
    Private ReadOnly userBox As New DarkTextBox() With {.Placeholder = "leer = anonym"}
    Private ReadOnly passwordLabel As Label = MakeLabel("Passwort (wird nicht gespeichert)", small:=True)
    Private ReadOnly passwordBox As New DarkTextBox() With {.UsePassword = True}
    Private ReadOnly testButton As ModernButton = MakeButton("Login testen", ButtonKind.Secondary)
    Private ReadOnly loginHint As Label = MakeLabel("", small:=True)

    ' Online catalog
    Private ReadOnly onlineCard As New CardPanel() With {.Title = "Online-Katalog", .Subtitle = "Neue Server ohne Programm-Update – aus catalog.json auf GitHub"}
    Private ReadOnly onlineToggle As New ToggleSwitch() With {.Text = "Beim Start neue Server aus dem Online-Katalog laden"}
    Private ReadOnly onlineButton As ModernButton = MakeButton("Jetzt aktualisieren", ButtonKind.Secondary, Theme.IconRefresh)
    Private ReadOnly onlineStatus As Label = MakeLabel("", small:=True)

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Einstellungen", "SteamCMD, Speicherort und Steam-Login")

        steamCard.Controls.AddRange(New Control() {steamFolderBox, steamBrowse, steamStatus, downloadButton, selfUpdateButton, downloadProgress})
        folderCard.Controls.AddRange(New Control() {rootBox, rootBrowse, validateToggle})
        loginCard.Controls.AddRange(New Control() {userLabel, userBox, passwordLabel, passwordBox, testButton, loginHint})
        onlineCard.Controls.AddRange(New Control() {onlineToggle, onlineButton, onlineStatus})
        Controls.AddRange(New Control() {steamCard, folderCard, loginCard, onlineCard})
        downloadProgress.Visible = False
        AutoScroll = True
        AutoScrollMargin = New System.Drawing.Size(0, Pad)

        loginHint.Text = "Nach dem ersten Login merkt sich SteamCMD die Anmeldung, das Passwort brauchst du dann meist nicht mehr. " &
                         "Fragt Steam Guard nach einem Code, gib ihn auf der Konsolen-Seite ein."

        steamFolderBox.Text = Data.SteamCmdFolder
        rootBox.Text = Data.InstallRoot
        userBox.Text = Data.SteamUser
        validateToggle.Checked = Data.Validate
        onlineToggle.Checked = Data.UseOnlineCatalog
        UpdateOnlineStatus()
        UpdateSteamStatus()

        AddHandler steamBrowse.Click, Sub(s, e) BrowseSteamCmd()
        AddHandler rootBrowse.Click, Sub(s, e) BrowseRoot()
        AddHandler downloadButton.Click, Async Sub(s, e) Await DownloadSteamCmdAsync()
        AddHandler selfUpdateButton.Click, Sub(s, e) Main.RunRaw("+quit", "+quit", "SteamCMD aktualisieren")
        AddHandler validateToggle.CheckedChanged,
            Sub(s, e)
                Data.Validate = validateToggle.Checked
                Data.Save()
            End Sub
        AddHandler userBox.Leave,
            Sub(s, e)
                Data.SteamUser = userBox.Text.Trim()
                Data.Save()
            End Sub
        AddHandler passwordBox.TextChanged, Sub(s, e) Data.SessionPassword = passwordBox.Text
        AddHandler testButton.Click, Sub(s, e) TestLogin()
        AddHandler onlineToggle.CheckedChanged,
            Sub(s, e)
                Data.UseOnlineCatalog = onlineToggle.Checked
                Data.Save()
            End Sub
        AddHandler onlineButton.Click, Async Sub(s, e) Await RefreshOnlineAsync()
    End Sub

    Friend Overrides Sub OnShown()
        UpdateSteamStatus()
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If loginCard Is Nothing OrElse loginHint Is Nothing Then Return
        Dim top = HeaderHeight + AutoScrollPosition.Y
        Dim w = Math.Min(780, ClientSize.Width - Pad * 2)
        Dim cw = w - 32

        steamCard.SetBounds(Pad, top, w, 176)
        steamBrowse.SetBounds(16 + cw - 40, steamCard.ContentTop, 40, 34)
        steamFolderBox.SetBounds(16, steamCard.ContentTop, cw - 48, 34)
        steamStatus.SetBounds(16, steamCard.ContentTop + 42, cw, 18)
        downloadButton.SetBounds(16, steamCard.ContentTop + 68, 220, 36)
        selfUpdateButton.SetBounds(downloadButton.Right + 8, steamCard.ContentTop + 68, 150, 36)
        downloadProgress.SetBounds(selfUpdateButton.Right + 16, steamCard.ContentTop + 83, Math.Max(40, cw - selfUpdateButton.Right), 6)

        folderCard.SetBounds(Pad, steamCard.Bottom + 12, w, 140)
        rootBrowse.SetBounds(16 + cw - 40, folderCard.ContentTop, 40, 34)
        rootBox.SetBounds(16, folderCard.ContentTop, cw - 48, 34)
        validateToggle.SetBounds(16, folderCard.ContentTop + 44, cw, 24)

        loginCard.SetBounds(Pad, folderCard.Bottom + 12, w, 210)
        Dim half = (cw - 12) \ 2
        userLabel.SetBounds(16, loginCard.ContentTop, half, 18)
        userBox.SetBounds(16, loginCard.ContentTop + 20, half, 34)
        passwordLabel.SetBounds(16 + half + 12, loginCard.ContentTop, half, 18)
        passwordBox.SetBounds(16 + half + 12, loginCard.ContentTop + 20, half, 34)
        testButton.SetBounds(16, loginCard.ContentTop + 66, 150, 36)
        loginHint.SetBounds(testButton.Right + 16, loginCard.ContentTop + 62, cw - testButton.Width - 16, 54)

        onlineCard.SetBounds(Pad, loginCard.Bottom + 12, w, 150)
        onlineToggle.SetBounds(16, onlineCard.ContentTop + 4, cw, 24)
        onlineButton.SetBounds(16, onlineCard.ContentTop + 38, 190, 36)
        onlineStatus.SetBounds(onlineButton.Right + 16, onlineCard.ContentTop + 46, cw - onlineButton.Width - 16, 20)
    End Sub

    Private Sub UpdateOnlineStatus(Optional message As String = "")
        If message.Length > 0 Then
            onlineStatus.Text = message
            Return
        End If
        Dim last = OnlineCatalog.LastUpdate
        Dim count = Data.Servers.Where(Function(x) x.FromOnline).Count()
        onlineStatus.Text = If(last.HasValue, "Zuletzt geladen: " & last.Value.ToString("dd.MM.yyyy HH:mm") & " · " & count & " zusätzliche Server",
                               "Noch nicht geladen.")
        onlineStatus.ForeColor = Theme.TextMuted
    End Sub

    Private Async Function RefreshOnlineAsync() As Task
        onlineButton.Enabled = False
        UpdateOnlineStatus("Lade Online-Katalog …")
        Try
            Dim added = Data.MergeOnline(Await OnlineCatalog.RefreshAsync())
            Main.ServersView.RefreshList()
            UpdateOnlineStatus()
            onlineStatus.Text &= If(added > 0, " · " & added & " neu hinzugefügt", " · nichts Neues")
            onlineStatus.ForeColor = Theme.Good
        Catch ex As Exception
            UpdateOnlineStatus("Nicht erreichbar: " & ex.Message)
            onlineStatus.ForeColor = Theme.Warning
        Finally
            onlineButton.Enabled = True
        End Try
    End Function

    Private Sub UpdateSteamStatus()
        If File.Exists(Data.SteamCmdExe) Then
            steamStatus.Text = "SteamCMD ist installiert."
            steamStatus.ForeColor = Theme.Good
            downloadButton.Text = "Neu herunterladen"
            downloadButton.Kind = ButtonKind.Secondary
            selfUpdateButton.Enabled = True
        Else
            steamStatus.Text = "SteamCMD fehlt noch – lade es mit einem Klick direkt von Valve herunter."
            steamStatus.ForeColor = Theme.Warning
            downloadButton.Text = "SteamCMD herunterladen"
            downloadButton.Kind = ButtonKind.Primary
            selfUpdateButton.Enabled = False
        End If
    End Sub

    ''' <summary>Downloads steamcmd.zip from Valve and extracts it. Returns True on success.</summary>
    Friend Async Function DownloadSteamCmdAsync() As Task(Of Boolean)
        If Main.Runner.IsRunning Then Return False
        Main.ShowPage(Me)
        downloadButton.Enabled = False
        downloadProgress.Visible = True
        downloadProgress.Value = 0
        steamStatus.Text = "Lade SteamCMD …"
        steamStatus.ForeColor = Theme.TextMuted
        Try
            Dim reporter As New Progress(Of Double)(Sub(percent) downloadProgress.Value = percent)
            Await SteamCmdRunner.DownloadAsync(Data.SteamCmdFolder, reporter)
            UpdateSteamStatus()
            Return True
        Catch ex As Exception
            steamStatus.Text = "Download fehlgeschlagen: " & ex.Message
            steamStatus.ForeColor = Theme.Danger
            Return False
        Finally
            downloadButton.Enabled = True
            downloadProgress.Visible = False
        End Try
    End Function

    Private Sub BrowseSteamCmd()
        Using dialog As New FolderBrowserDialog() With {
            .Description = "Ordner für SteamCMD wählen (dort liegt bzw. landet steamcmd.exe)",
            .SelectedPath = Data.SteamCmdFolder,
            .ShowNewFolderButton = True
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Data.SteamCmdFolder = dialog.SelectedPath
        End Using
        steamFolderBox.Text = Data.SteamCmdFolder
        Data.Save()
        UpdateSteamStatus()
    End Sub

    Private Sub BrowseRoot()
        Using dialog As New FolderBrowserDialog() With {
            .Description = "Ordner für deine Gameserver wählen",
            .SelectedPath = Data.InstallRoot,
            .ShowNewFolderButton = True
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Data.InstallRoot = dialog.SelectedPath
        End Using
        rootBox.Text = Data.InstallRoot
        Data.Save()
        Main.ServersView.RefreshList()
        Main.InstalledView.RefreshList()
    End Sub

    Private Sub TestLogin()
        Data.SteamUser = userBox.Text.Trim()
        Data.Save()
        If Data.SteamUser.Length = 0 Then
            MessageBox.Show(Me, "Trage zuerst deinen Steam-Accountnamen ein.", "Login testen", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim pwd = Data.SessionPassword
        Dim args = "+login """ & Data.SteamUser & """" & If(pwd.Length > 0, " """ & pwd & """", "") & " +quit"
        Dim display = "+login " & Data.SteamUser & If(pwd.Length > 0, " ********", "") & " +quit"
        Main.RunRaw(args, display, "Steam-Login testen")
    End Sub

End Class
