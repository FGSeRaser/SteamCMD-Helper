Imports System.Text

''' <summary>
''' Settings, own servers and changed start commands.
''' Saved as JSON in %AppData%\SteamCMD Helper\data.json. The Steam password is never saved.
''' </summary>
Friend Class Store

    Public Property SteamCmdFolder As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SteamCMD")
    Public Property InstallRoot As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Gameserver")
    Public Property SteamUser As String = ""
    Public Property Validate As Boolean = True
    ''' <summary>Load extra servers from catalog.json on GitHub.</summary>
    Public Property UseOnlineCatalog As Boolean = True

    ''' <summary>All servers: built-in ones (with the user's changes) plus own ones.</summary>
    Public Property Servers As New List(Of GameServer)()

    ''' <summary>Password for this session only.</summary>
    Public Property SessionPassword As String = ""

    Private Shared ReadOnly DataPath As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SteamCMD Helper", "data.json")

    Public ReadOnly Property SteamCmdExe As String
        Get
            Return Path.Combine(SteamCmdFolder, "steamcmd.exe")
        End Get
    End Property

    Public Function InstallFolder(server As GameServer) As String
        Return Path.Combine(InstallRoot, server.FolderName)
    End Function

    Public Function IsInstalled(server As GameServer) As Boolean
        Return File.Exists(Path.Combine(InstallFolder(server), "steamapps", "appmanifest_" & server.AppId & ".acf"))
    End Function

    ' ---------- Load / save ----------

    Public Shared Function Load() As Store
        Dim s As New Store()
        Dim root As Object = Nothing
        Try
            If File.Exists(DataPath) Then root = Json.Parse(File.ReadAllText(DataPath, Encoding.UTF8))
        Catch
            root = Nothing
        End Try

        Dim settings = Json.GetObject(root, "settings")
        If settings IsNot Nothing Then
            Dim folder = Json.GetString(settings, "steamcmd")
            If folder.Length > 0 Then s.SteamCmdFolder = folder
            Dim installRootValue = Json.GetString(settings, "installRoot")
            If installRootValue.Length > 0 Then s.InstallRoot = installRootValue
            s.SteamUser = Json.GetString(settings, "user")
            s.Validate = Json.GetString(settings, "validate", "True") = "True"
            s.UseOnlineCatalog = Json.GetString(settings, "online", "True") = "True"
        End If

        s.Servers = Catalog.BuiltIn()
        If s.UseOnlineCatalog Then s.MergeOnline(OnlineCatalog.LoadCached())
        For Each item As Object In Json.GetArray(root, "servers")
            Dim appId = CInt(Json.GetNumber(item, "appid"))
            If appId <= 0 Then Continue For
            Dim isCustom = Json.GetString(item, "custom") = "True"
            Dim server = If(isCustom, Nothing, s.Servers.FirstOrDefault(Function(x) x.AppId = appId AndAlso Not x.IsCustom))
            If server Is Nothing Then
                server = New GameServer With {.AppId = appId, .IsCustom = True}
                s.Servers.Add(server)
            End If
            Dim name = Json.GetString(item, "name")
            If name.Length > 0 Then server.Name = name
            Dim category = Json.GetString(item, "category")
            If category.Length > 0 Then server.Category = category
            server.NeedsLogin = Json.GetString(item, "login", server.NeedsLogin.ToString()) = "True"
            server.StartExe = Json.GetString(item, "exe", server.StartExe)
            server.StartArgs = Json.GetString(item, "args", server.StartArgs)
            server.Beta = Json.GetString(item, "beta", server.Beta)
        Next
        Return s
    End Function

    ''' <summary>Adds servers from the online catalog that are not in the list yet. Returns how many were added.</summary>
    Public Function MergeOnline(online As IEnumerable(Of GameServer)) As Integer
        Dim added = 0
        For Each server As GameServer In online
            If Servers.Any(Function(x) x.AppId = server.AppId) Then Continue For
            Servers.Add(server)
            added += 1
        Next
        Return added
    End Function

    ''' <summary>Writes own servers and every built-in server that differs from the defaults.</summary>
    Public Sub Save()
        Try
            Dim defaults = Catalog.BuiltIn()
            defaults.AddRange(OnlineCatalog.LoadCached())
            Dim entries As New List(Of String)()
            For Each server In Servers
                Dim original = defaults.FirstOrDefault(Function(d) d.AppId = server.AppId AndAlso Not server.IsCustom)
                If original IsNot Nothing AndAlso original.Name = server.Name AndAlso original.Category = server.Category AndAlso
                   original.NeedsLogin = server.NeedsLogin AndAlso original.StartExe = server.StartExe AndAlso
                   original.StartArgs = server.StartArgs AndAlso original.Beta = server.Beta Then Continue For
                entries.Add("    {""appid"": " & server.AppId &
                            ", ""custom"": """ & server.IsCustom.ToString() & """" &
                            ", ""name"": " & Json.Quote(server.Name) &
                            ", ""category"": " & Json.Quote(server.Category) &
                            ", ""login"": """ & server.NeedsLogin.ToString() & """" &
                            ", ""exe"": " & Json.Quote(server.StartExe) &
                            ", ""args"": " & Json.Quote(server.StartArgs) &
                            ", ""beta"": " & Json.Quote(server.Beta) & "}")
            Next

            Dim sb As New StringBuilder()
            sb.AppendLine("{")
            sb.Append("  ""settings"": {""steamcmd"": ").Append(Json.Quote(SteamCmdFolder)).
                Append(", ""installRoot"": ").Append(Json.Quote(InstallRoot)).
                Append(", ""user"": ").Append(Json.Quote(SteamUser)).
                Append(", ""validate"": """).Append(Validate.ToString()).
                Append(""", ""online"": """).Append(UseOnlineCatalog.ToString()).AppendLine("""},")
            sb.AppendLine("  ""servers"": [")
            sb.AppendLine(String.Join("," & Environment.NewLine, entries))
            sb.AppendLine("  ]")
            sb.AppendLine("}")

            Directory.CreateDirectory(Path.GetDirectoryName(DataPath))
            File.WriteAllText(DataPath, sb.ToString(), New UTF8Encoding(False))
        Catch
            ' Not critical
        End Try
    End Sub

End Class
