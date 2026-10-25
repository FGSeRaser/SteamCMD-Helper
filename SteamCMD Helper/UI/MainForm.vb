''' <summary>Main window: navigation, SteamCMD runner and install queue.</summary>
Friend Class MainForm
    Inherits Form

    Friend ReadOnly Data As Store = Store.Load()
    Friend ReadOnly Runner As New SteamCmdRunner()

    Friend ReadOnly ServersView As ServersPage
    Friend ReadOnly InstalledView As InstalledPage
    Friend ReadOnly ConsoleView As ConsolePage
    Friend ReadOnly SettingsView As SettingsPage

    Private ReadOnly sidebar As Panel
    Private ReadOnly authorLink As New GitHubLink()
    Private ReadOnly content As Panel
    Private ReadOnly navButtons As New List(Of NavButton)()
    Private ReadOnly pages As New List(Of PageBase)()
    Private ReadOnly installQueue As New Queue(Of GameServer)()
    Private currentServer As GameServer
    Private statusText As String = ""

    Public Sub New()
        Text = "SteamCMD Helper"
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New System.Drawing.Size(1240, 760)
        MinimumSize = New System.Drawing.Size(1040, 680)
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        Try
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        sidebar = New DoubleBufferedPanel() With {.Dock = DockStyle.Left, .Width = 230, .BackColor = Theme.Sidebar}
        AddHandler sidebar.Paint, AddressOf Sidebar_Paint
        sidebar.Controls.Add(authorLink)
        AddHandler sidebar.Resize, Sub(s, e) authorLink.SetBounds(16, sidebar.Height - 92 - 54, sidebar.Width - 32, 46)
        content = New Panel() With {.Dock = DockStyle.Fill, .BackColor = Theme.Back}

        ServersView = New ServersPage(Me)
        InstalledView = New InstalledPage(Me)
        ConsoleView = New ConsolePage(Me)
        SettingsView = New SettingsPage(Me)

        AddPage(ServersView, "Server-Katalog", Theme.IconServer)
        AddPage(InstalledView, "Installiert", Theme.IconList)
        AddPage(ConsoleView, "Konsole", Theme.IconConsole)
        AddPage(SettingsView, "Einstellungen", Theme.IconSettings)

        Controls.Add(content)
        Controls.Add(sidebar)

        AddHandler Runner.RunningChanged, Sub() sidebar.Invalidate()
        AddHandler Runner.Finished, AddressOf Runner_Finished
        ShowPage(If(File.Exists(Data.SteamCmdExe), CType(ServersView, PageBase), SettingsView))
    End Sub

    Protected Overrides Async Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If Not Data.UseOnlineCatalog Then Return
        Try
            Dim added = Data.MergeOnline(Await OnlineCatalog.RefreshAsync())
            If added > 0 Then
                ServersView.RefreshList()
                SetStatus(added & " neue Server aus dem Online-Katalog")
            End If
        Catch
            ' Offline or catalog not published yet – the built-in list is enough
        End Try
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkTitleBar(Me)
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Runner.IsRunning Then
            Dim answer = MessageBox.Show(Me, "SteamCMD arbeitet noch. Trotzdem beenden? Der Download wird abgebrochen.",
                                         "SteamCMD läuft", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
            If answer <> DialogResult.Yes Then
                e.Cancel = True
                Return
            End If
            installQueue.Clear()
            Runner.Cancel()
        End If
        Data.Save()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub AddPage(page As PageBase, title As String, glyph As String)
        page.Dock = DockStyle.Fill
        page.Visible = False
        content.Controls.Add(page)
        pages.Add(page)
        Dim nav As New NavButton() With {
            .Text = title, .IconGlyph = glyph, .Tag = page,
            .Location = New Point(16, 120 + navButtons.Count * 46), .Size = New System.Drawing.Size(198, 40)}
        AddHandler nav.Click, Sub(s, e) ShowPage(DirectCast(DirectCast(s, NavButton).Tag, PageBase))
        sidebar.Controls.Add(nav)
        navButtons.Add(nav)
    End Sub

    Friend Sub ShowPage(page As PageBase)
        For Each p As PageBase In pages
            p.Visible = p Is page
        Next
        For Each nav As NavButton In navButtons
            nav.Selected = nav.Tag Is page
        Next
        page.OnShown()
    End Sub

    Friend Sub SetStatus(text As String)
        statusText = text
        sidebar.Invalidate()
    End Sub

    ' ---------- Install queue ----------

    ''' <summary>Installs or updates servers one after another.</summary>
    Friend Async Sub InstallServers(servers As IEnumerable(Of GameServer))
        If Not Await EnsureSteamCmdAsync() Then Return

        Dim list = servers.ToList()
        Dim missingLogin = list.Where(Function(s) s.NeedsLogin).ToList()
        If missingLogin.Count > 0 AndAlso String.IsNullOrWhiteSpace(Data.SteamUser) Then
            MessageBox.Show(Me, "Für " & String.Join(", ", missingLogin.Select(Function(s) s.Name)) &
                            " brauchst du einen Steam-Account, der das Spiel besitzt." & Environment.NewLine & Environment.NewLine &
                            "Trage deinen Steam-Namen unter Einstellungen ein.",
                            "Steam-Login nötig", MessageBoxButtons.OK, MessageBoxIcon.Information)
            ShowPage(SettingsView)
            Return
        End If

        For Each server As GameServer In list
            If Not installQueue.Contains(server) AndAlso server IsNot currentServer Then installQueue.Enqueue(server)
        Next
        ShowPage(ConsoleView)
        If Not Runner.IsRunning Then StartNext()
    End Sub

    Private Sub StartNext()
        If installQueue.Count = 0 Then
            currentServer = Nothing
            SetStatus("")
            Return
        End If
        currentServer = installQueue.Dequeue()
        Dim installDir = Data.InstallFolder(currentServer)
        Directory.CreateDirectory(installDir)
        Dim args = SteamCmdRunner.BuildInstallArguments(currentServer, installDir, Data.SteamUser, Data.SessionPassword, Data.Validate)
        ConsoleView.BeginJob(currentServer.Name & If(installQueue.Count > 0, $"  (noch {installQueue.Count} in der Warteschlange)", ""))
        SetStatus(currentServer.Name)
        Try
            Runner.Start(Data.SteamCmdExe, args.Arguments, args.Display)
        Catch ex As Exception
            ConsoleView.WriteLine("SteamCMD konnte nicht gestartet werden: " & ex.Message, LineKind.Error)
            installQueue.Clear()
            currentServer = Nothing
            SetStatus("")
        End Try
    End Sub

    Private Sub Runner_Finished(exitCode As Integer, success As Boolean, summary As String)
        If currentServer IsNot Nothing Then
            ConsoleView.EndJob(currentServer.Name, success, summary)
            AfterInstall(currentServer, success)
        Else
            ConsoleView.EndJob("SteamCMD", success, summary)
        End If
        ServersView.RefreshList()
        InstalledView.RefreshList()
        StartNext()
    End Sub

    ''' <summary>
    ''' Learns from the result: marks servers that need a Steam login and finds the
    ''' start program of servers that have none yet (new or experimental App-IDs).
    ''' </summary>
    Private Sub AfterInstall(server As GameServer, success As Boolean)
        If Not success AndAlso Runner.LastRunNeededLogin AndAlso Not server.NeedsLogin Then
            server.NeedsLogin = True
            Data.Save()
            ConsoleView.WriteLine("Hinweis: " & server.Name & " ist jetzt als ""Steam-Login nötig"" markiert. Trage deinen Steam-Namen unter Einstellungen ein.", LineKind.Warning)
            Return
        End If
        If Not success Then Return
        Dim folder = Data.InstallFolder(server)
        If server.StartExe.Length > 0 AndAlso File.Exists(Path.Combine(folder, server.StartExe)) Then Return
        Dim found = ExeFinder.Find(folder)
        If found.Count = 0 Then Return
        server.StartExe = found(0)
        Data.Save()
        ConsoleView.WriteLine("Startprogramm automatisch erkannt: " & found(0) & If(found.Count > 1, "  (weitere Kandidaten im Katalog über ""Erkennen"")", ""), LineKind.Info)
    End Sub

    ''' <summary>Runs SteamCMD with custom arguments (e.g. login test or self update).</summary>
    Friend Async Sub RunRaw(arguments As String, display As String, title As String)
        If Runner.IsRunning Then
            MessageBox.Show(Me, "SteamCMD arbeitet gerade. Warte, bis es fertig ist.", "SteamCMD", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        If Not Await EnsureSteamCmdAsync() Then Return
        currentServer = Nothing
        ShowPage(ConsoleView)
        ConsoleView.BeginJob(title)
        SetStatus(title)
        Try
            Runner.Start(Data.SteamCmdExe, arguments, display)
        Catch ex As Exception
            ConsoleView.WriteLine("SteamCMD konnte nicht gestartet werden: " & ex.Message, LineKind.Error)
            SetStatus("")
        End Try
    End Sub

    ''' <summary>Downloads SteamCMD if it is missing (asks first).</summary>
    Friend Async Function EnsureSteamCmdAsync() As Task(Of Boolean)
        If File.Exists(Data.SteamCmdExe) Then Return True
        Dim answer = MessageBox.Show(Me, "SteamCMD wurde in " & Data.SteamCmdFolder & " nicht gefunden." & Environment.NewLine &
                                     "Jetzt von Valve herunterladen?", "SteamCMD fehlt", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
        If answer <> DialogResult.Yes Then Return False
        Return Await SettingsView.DownloadSteamCmdAsync()
    End Function

    ' ---------- Starting servers ----------

    Friend Sub StartServer(server As GameServer)
        Dim installDir = Data.InstallFolder(server)
        If (String.IsNullOrWhiteSpace(server.StartExe) OrElse Not File.Exists(Path.Combine(installDir, server.StartExe))) AndAlso
           Data.IsInstalled(server) Then
            ' Unknown or wrong start program: look for it in the server folder
            Dim chosen = PickStartProgram(server)
            If chosen.Length > 0 Then
                server.StartExe = chosen
                Data.Save()
                ServersView.RefreshList()
            End If
        End If
        If String.IsNullOrWhiteSpace(server.StartExe) Then
            MessageBox.Show(Me, "Für " & server.Name & " ist kein Startprogramm eingetragen." & Environment.NewLine &
                            "Trage es im Katalog unter ""Startprogramm"" ein (Pfad relativ zum Serverordner).",
                            "Server starten", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim exePath = Path.Combine(installDir, server.StartExe)
        If Not File.Exists(exePath) Then
            MessageBox.Show(Me, "Nicht gefunden:" & Environment.NewLine & exePath & Environment.NewLine & Environment.NewLine &
                            "Ist der Server installiert? Sonst passe das Startprogramm im Katalog an.",
                            "Server starten", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        Try
            Process.Start(New ProcessStartInfo(exePath, server.StartArgs) With {
                .UseShellExecute = True,
                .WorkingDirectory = Path.GetDirectoryName(exePath)
            })
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Server starten", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    ''' <summary>Searches the server folder for the start program and lets the user choose. "" if nothing was chosen.</summary>
    Friend Function PickStartProgram(server As GameServer) As String
        Dim found = ExeFinder.Find(Data.InstallFolder(server))
        If found.Count = 0 Then
            MessageBox.Show(Me, "Im Serverordner wurde kein passendes Startprogramm gefunden." & Environment.NewLine &
                            "Ist der Server installiert? Sonst trage das Programm von Hand ein.",
                            "Startprogramm suchen", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return ""
        End If
        Return ChoiceDialog.Ask(Me, "Startprogramm wählen",
                                "Diese Programme kommen für " & server.Name & " in Frage – das beste steht oben:", found)
    End Function

    Friend Sub CreateStartScript(server As GameServer)
        Dim installDir = Data.InstallFolder(server)
        Using dialog As New SaveFileDialog() With {
            .Title = "Skript speichern",
            .Filter = "Batch-Datei (*.bat)|*.bat",
            .FileName = server.FolderName & "_update.bat",
            .InitialDirectory = Data.InstallRoot
        }
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            ' The script never contains the password; SteamCMD asks for it if needed
            Dim args = SteamCmdRunner.BuildInstallArguments(server, installDir, Data.SteamUser, "", Data.Validate)
            Dim lines As New List(Of String) From {
                "@echo off",
                "title " & server.Name,
                """" & Data.SteamCmdExe & """ " & args.Arguments
            }
            If Not String.IsNullOrWhiteSpace(server.StartExe) Then
                lines.Add("cd /d """ & Path.GetDirectoryName(Path.Combine(installDir, server.StartExe)) & """")
                lines.Add("""" & Path.GetFileName(server.StartExe) & """ " & server.StartArgs)
            End If
            lines.Add("pause")
            File.WriteAllLines(dialog.FileName, lines, New System.Text.UTF8Encoding(False))
        End Using
    End Sub

    Friend Shared Sub OpenFolder(folder As String)
        Try
            Directory.CreateDirectory(folder)
            Process.Start(New ProcessStartInfo(folder) With {.UseShellExecute = True})
        Catch
        End Try
    End Sub

    ' ---------- Sidebar ----------

    Private Sub Sidebar_Paint(sender As Object, e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias

        ' Logo: round badge with a gear-like ring
        Using b As New SolidBrush(Theme.Accent)
            g.FillEllipse(b, 22, 30, 40, 40)
        End Using
        Using b As New SolidBrush(Theme.Sidebar)
            g.FillEllipse(b, 32, 40, 20, 20)
        End Using
        Using b As New SolidBrush(Theme.Accent)
            g.FillEllipse(b, 37, 45, 10, 10)
        End Using

        TextRenderer.DrawText(g, "SteamCMD", Theme.HeadingFont, New Point(74, 30), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, "Gameserver Helper", Theme.SmallFont, New Point(75, 52), Theme.TextMuted, TextFormatFlags.NoPadding)

        Dim box As New RectangleF(16, sidebar.Height - 92, sidebar.Width - 32, 74)
        Theme.FillRounded(g, box, 8, Theme.Surface, Theme.Border)
        Dim stateText As String
        Dim stateColor As Color
        If Runner.IsRunning Then
            stateText = "Arbeitet …"
            stateColor = Theme.Accent
        ElseIf File.Exists(Data.SteamCmdExe) Then
            stateText = "Bereit"
            stateColor = Theme.Good
        Else
            stateText = "SteamCMD fehlt"
            stateColor = Theme.Warning
        End If
        Using dot As New SolidBrush(stateColor)
            g.FillEllipse(dot, box.X + 14, box.Y + 18, 9, 9)
        End Using
        TextRenderer.DrawText(g, stateText, Theme.BodyBoldFont, New Point(CInt(box.X) + 30, CInt(box.Y) + 13), Theme.TextMain, TextFormatFlags.NoPadding)
        Dim info = If(statusText.Length > 0, statusText, If(Data.SteamUser.Length > 0, "Steam: " & Data.SteamUser, "Anonym angemeldet"))
        TextRenderer.DrawText(g, info, Theme.SmallFont, New Rectangle(CInt(box.X) + 14, CInt(box.Y) + 40, CInt(box.Width) - 24, 20),
                              Theme.TextMuted, TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        sidebar?.Invalidate()
    End Sub

End Class


''' <summary>Panel without flicker for custom-painted areas.</summary>
Friend Class DoubleBufferedPanel
    Inherits Panel

    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
    End Sub
End Class


''' <summary>Base class for pages: draws the page title and offers helpers.</summary>
Friend Class PageBase
    Inherits Panel

    Protected ReadOnly Main As MainForm
    Private ReadOnly _title As String
    Private ReadOnly _subtitle As String

    Protected Const Pad As Integer = 24
    Protected Const HeaderHeight As Integer = 84

    Public Sub New(owner As MainForm, title As String, subtitle As String)
        Main = owner
        _title = title
        _subtitle = subtitle
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        DoubleBuffered = True
        ResizeRedraw = True
    End Sub

    Protected ReadOnly Property Data As Store
        Get
            Return Main.Data
        End Get
    End Property

    Friend Overridable Sub OnShown()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        TextRenderer.DrawText(e.Graphics, _title, Theme.TitleFont, New Point(Pad - 2, 20), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(e.Graphics, _subtitle, Theme.BodyFont, New Rectangle(Pad, 54, Width - Pad * 2, 22), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

    Protected Shared Function MakeLabel(text As String, Optional muted As Boolean = True, Optional small As Boolean = False) As Label
        Return New Label() With {
            .Text = text, .AutoSize = False, .AutoEllipsis = True,
            .ForeColor = If(muted, Theme.TextMuted, Theme.TextMain),
            .Font = If(small, Theme.SmallFont, Theme.BodyFont),
            .BackColor = Color.Transparent, .Height = If(small, 18, 20)}
    End Function

    Protected Shared Function MakeButton(text As String, kind As ButtonKind, Optional glyph As String = "") As ModernButton
        Return New ModernButton() With {.Text = text, .Kind = kind, .IconGlyph = glyph}
    End Function

End Class
