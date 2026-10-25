Imports System.Net.Http
Imports System.Text

''' <summary>
''' Extra servers from catalog.json in the GitHub repository. New dedicated servers can be
''' added there without a new program version. The last download is kept as a local copy.
''' </summary>
Friend NotInheritable Class OnlineCatalog

    Private Sub New()
    End Sub

    Public Const Url As String = "https://raw.githubusercontent.com/FGSeRaser/SteamCMD-Helper/main/catalog.json"

    Private Shared ReadOnly CachePath As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SteamCMD Helper", "catalog-online.json")

    Public Shared ReadOnly Property LastUpdate As DateTime?
        Get
            If Not File.Exists(CachePath) Then Return Nothing
            Return File.GetLastWriteTime(CachePath)
        End Get
    End Property

    ''' <summary>Servers from the last download (empty if never downloaded).</summary>
    Public Shared Function LoadCached() As List(Of GameServer)
        Try
            If File.Exists(CachePath) Then Return ParseCatalog(File.ReadAllText(CachePath, Encoding.UTF8))
        Catch
        End Try
        Return New List(Of GameServer)()
    End Function

    ''' <summary>Downloads catalog.json. Throws on network errors.</summary>
    Public Shared Async Function RefreshAsync() As Task(Of List(Of GameServer))
        Using client As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(20)}
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SteamCMD-Helper (github.com/FGSeRaser)")
            Dim text = Await client.GetStringAsync(Url & "?t=" & DateTime.UtcNow.Ticks)
            Dim servers = ParseCatalog(text) ' validates before overwriting the copy
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath))
            File.WriteAllText(CachePath, text, New UTF8Encoding(False))
            Return servers
        End Using
    End Function

    ''' <summary>
    ''' Format: { "servers": [ { "name": "...", "appid": 123, "category": "Survival", "login": false,
    ''' "exe": "...", "args": "...", "note": "...", "beta": "" } ] }
    ''' </summary>
    Public Shared Function ParseCatalog(text As String) As List(Of GameServer)
        Dim root = Json.Parse(text)
        Dim result As New List(Of GameServer)()
        For Each item As Object In Json.GetArray(root, "servers")
            Dim appId = CInt(Json.GetNumber(item, "appid"))
            Dim name = Json.GetString(item, "name").Trim()
            If appId <= 0 OrElse name.Length = 0 Then Continue For
            Dim category = Json.GetString(item, "category", Catalog.Other)
            If Not Catalog.Categories.Contains(category) Then category = Catalog.Other
            Dim login = Json.GetValue(item, "login")
            result.Add(New GameServer With {
                .Name = name, .AppId = appId, .Category = category,
                .NeedsLogin = (TypeOf login Is Boolean AndAlso CBool(login)) OrElse Json.GetString(item, "login") = "True",
                .StartExe = Json.GetString(item, "exe"),
                .StartArgs = Json.GetString(item, "args"),
                .Note = Json.GetString(item, "note"),
                .Beta = Json.GetString(item, "beta"),
                .FromOnline = True})
        Next
        Return result
    End Function

End Class
