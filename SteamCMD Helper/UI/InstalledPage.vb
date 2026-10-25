''' <summary>Installed servers: update, start, open or uninstall.</summary>
Friend Class InstalledPage
    Inherits PageBase

    Private ReadOnly card As New CardPanel() With {.Title = "Installierte Server"}
    Private ReadOnly installedList As New EntryListBox() With {.ItemHeight = 50, .EmptyText = "Noch keine Server installiert. Wähle im Katalog einen aus."}
    Private ReadOnly updateAllButton As ModernButton = MakeButton("Alle aktualisieren", ButtonKind.Primary, Theme.IconRefresh)
    Private ReadOnly updateButton As ModernButton = MakeButton("Aktualisieren", ButtonKind.Secondary, Theme.IconDownload)
    Private ReadOnly startButton As ModernButton = MakeButton("Starten", ButtonKind.Secondary, Theme.IconPlay)
    Private ReadOnly folderButton As ModernButton = MakeButton("Ordner", ButtonKind.Secondary, Theme.IconFolder)
    Private ReadOnly detailsButton As ModernButton = MakeButton("Im Katalog", ButtonKind.Ghost)
    Private ReadOnly uninstallButton As ModernButton = MakeButton("Deinstallieren", ButtonKind.Danger, Theme.IconDelete)

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Installiert", "Deine Server auf einen Blick – Updates mit einem Klick")

        card.Controls.AddRange(New Control() {installedList, updateAllButton, updateButton, startButton, folderButton, detailsButton, uninstallButton})
        Controls.Add(card)

        AddHandler installedList.SelectedIndexChanged, Sub(s, e) UpdateButtons()
        AddHandler installedList.DoubleClick, Sub(s, e) StartSelected()
        AddHandler updateAllButton.Click,
            Sub(s, e)
                Dim all = Data.Servers.Where(Function(x) Data.IsInstalled(x)).ToList()
                If all.Count > 0 Then Main.InstallServers(all)
            End Sub
        AddHandler updateButton.Click,
            Sub(s, e)
                Dim server = SelectedServer()
                If server IsNot Nothing Then Main.InstallServers({server})
            End Sub
        AddHandler startButton.Click, Sub(s, e) StartSelected()
        AddHandler folderButton.Click,
            Sub(s, e)
                Dim server = SelectedServer()
                If server IsNot Nothing Then MainForm.OpenFolder(Data.InstallFolder(server))
            End Sub
        AddHandler detailsButton.Click, Sub(s, e) Main.ShowPage(Main.ServersView)
        AddHandler uninstallButton.Click, AddressOf Uninstall
        UpdateButtons()
    End Sub

    Friend Overrides Sub OnShown()
        RefreshList()
    End Sub

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If card Is Nothing OrElse uninstallButton Is Nothing Then Return
        Dim top = HeaderHeight
        Dim w = ClientSize.Width - Pad * 2
        Dim h = Math.Max(300, ClientSize.Height - top - Pad)
        card.SetBounds(Pad, top, w, h)

        Dim cw = w - 32
        Dim by = h - 16 - 38
        updateAllButton.SetBounds(16, by, 180, 38)
        updateButton.SetBounds(updateAllButton.Right + 8, by, 150, 38)
        startButton.SetBounds(updateButton.Right + 8, by, 120, 38)
        folderButton.SetBounds(startButton.Right + 8, by, 110, 38)
        uninstallButton.SetBounds(16 + cw - 160, by, 160, 38)
        detailsButton.SetBounds(uninstallButton.Left - 8 - 120, by, 120, 38)
        installedList.SetBounds(12, card.ContentTop, cw + 8, by - 12 - card.ContentTop)
    End Sub

    Friend Sub RefreshList()
        Dim keep = SelectedServer()
        installedList.BeginUpdate()
        installedList.Items.Clear()
        For Each server As GameServer In Data.Servers.OrderBy(Function(x) x.Name, StringComparer.CurrentCultureIgnoreCase)
            If Not Data.IsInstalled(server) Then Continue For
            Dim manifest = Path.Combine(Data.InstallFolder(server), "steamapps", "appmanifest_" & server.AppId & ".acf")
            Dim updated = File.GetLastWriteTime(manifest)
            Dim entry As New ListEntry With {
                .Title = server.Name,
                .Subtitle = Data.InstallFolder(server),
                .Detail = "zuletzt aktualisiert " & updated.ToString("dd.MM.yyyy HH:mm"),
                .Tag = server}
            If String.IsNullOrWhiteSpace(server.StartExe) Then
                entry.Badge = "kein Startbefehl"
                entry.BadgeColor = Theme.TextMuted
            End If
            installedList.Items.Add(entry)
            If server Is keep Then installedList.SelectedIndex = installedList.Items.Count - 1
        Next
        installedList.EndUpdate()
        card.Subtitle = $"{installedList.Items.Count} Server in {Data.InstallRoot}"
        updateAllButton.Enabled = installedList.Items.Count > 0
        UpdateButtons()
    End Sub

    Private Function SelectedServer() As GameServer
        Return TryCast(TryCast(installedList.SelectedItem, ListEntry)?.Tag, GameServer)
    End Function

    Private Sub UpdateButtons()
        Dim has = SelectedServer() IsNot Nothing
        updateButton.Enabled = has
        startButton.Enabled = has
        folderButton.Enabled = has
        uninstallButton.Enabled = has
    End Sub

    Private Sub StartSelected()
        Dim server = SelectedServer()
        If server IsNot Nothing Then Main.StartServer(server)
    End Sub

    Private Sub Uninstall(sender As Object, e As EventArgs)
        Dim server = SelectedServer()
        If server Is Nothing Then Return
        Dim folder = Data.InstallFolder(server)
        If MessageBox.Show(Me, $"{server.Name} deinstallieren?" & Environment.NewLine & Environment.NewLine &
                           $"Der Ordner {folder} kommt in den Papierkorb – inklusive Welten und Einstellungen.",
                           "Deinstallieren", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then Return
        Try
            Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(folder,
                Microsoft.VisualBasic.FileIO.UIOption.AllDialogs,
                Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin)
        Catch ex As OperationCanceledException
            Return
        Catch ex As Exception
            MessageBox.Show(Me, "Konnte nicht gelöscht werden (läuft der Server noch?)" & Environment.NewLine & ex.Message,
                            "Deinstallieren", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
        RefreshList()
        Main.ServersView.RefreshList()
    End Sub

End Class
