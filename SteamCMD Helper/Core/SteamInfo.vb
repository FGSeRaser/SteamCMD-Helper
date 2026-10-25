Imports System.Text
Imports System.Text.RegularExpressions

''' <summary>One start entry from the Steam app info (config/launch).</summary>
Friend Class LaunchOption
    Public Property Executable As String = ""
    Public Property Arguments As String = ""
    Public Property Description As String = ""
    Public Property OsList As String = ""

    Public ReadOnly Property IsWindows As Boolean
        Get
            Return OsList.Length = 0 OrElse OsList.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0
        End Get
    End Property
End Class


''' <summary>
''' What Steam knows about an App-ID (read with "app_info_print" from SteamCMD).
''' Works for any dedicated server, also brand-new ones that are not in the catalog yet.
''' </summary>
Friend Class SteamAppInfo
    Public Property AppId As Integer
    Public Property Found As Boolean
    Public Property Name As String = ""
    Public Property AppType As String = ""
    Public Property OsList As String = ""
    Public Property Launches As New List(Of LaunchOption)()
    Public Property ErrorText As String = ""

    ''' <summary>Name without "Dedicated Server" etc., for the catalog.</summary>
    Public ReadOnly Property ShortName As String
        Get
            Dim n = Name
            For Each suffix As String In {" Dedicated Server", " Dedicated server", " Dedicated", " DS", " Server"}
                If n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) Then n = n.Substring(0, n.Length - suffix.Length)
            Next
            Return n.Trim(" "c, "-"c, ":"c)
        End Get
    End Property

    Public ReadOnly Property HasWindows As Boolean
        Get
            If OsList.Length > 0 Then Return OsList.IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0
            If Launches.Count > 0 Then Return Launches.Any(Function(l) l.IsWindows)
            Return True ' unknown – assume yes
        End Get
    End Property

    ''' <summary>The best start entry for Windows (first one without special type).</summary>
    Public ReadOnly Property WindowsLaunch As LaunchOption
        Get
            Dim best = Launches.FirstOrDefault(Function(l) l.IsWindows AndAlso l.Executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            Return If(best, Launches.FirstOrDefault(Function(l) l.IsWindows))
        End Get
    End Property

    ''' <summary>Asks SteamCMD for the app info. Takes a few seconds; SteamCMD must not be busy.</summary>
    Public Shared Async Function QueryAsync(steamCmdExe As String, appId As Integer) As Task(Of SteamAppInfo)
        Dim info As New SteamAppInfo With {.AppId = appId}
        ' Printing twice is a known trick: the first print is sometimes empty right after the update
        Dim args = "+login anonymous +app_info_update 1 +app_info_print " & appId & " +app_info_print " & appId & " +quit"
        Dim output = Await Task.Run(Function() RunCapture(steamCmdExe, args, 180000))
        info.Parse(output)
        Return info
    End Function

    Private Shared Sub Append(sb As StringBuilder, line As String)
        If line Is Nothing Then Return
        SyncLock sb
            sb.AppendLine(line)
        End SyncLock
    End Sub

    Private Shared Function RunCapture(exe As String, args As String, timeoutMs As Integer) As String
        Dim psi As New ProcessStartInfo(exe, args) With {
            .WorkingDirectory = Path.GetDirectoryName(exe),
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .RedirectStandardInput = True,
            .CreateNoWindow = True,
            .StandardOutputEncoding = Encoding.UTF8
        }
        Dim sb As New StringBuilder()
        Using p As New Process() With {.StartInfo = psi}
            AddHandler p.OutputDataReceived, Sub(s, e) Append(sb, e.Data)
            AddHandler p.ErrorDataReceived, Sub(s, e) Append(sb, e.Data)
            p.Start()
            p.StandardInput.Close()
            p.BeginOutputReadLine()
            p.BeginErrorReadLine()
            If Not p.WaitForExit(timeoutMs) Then
                Try
                    p.Kill()
                Catch
                End Try
            End If
            p.WaitForExit(2000)
        End Using
        SyncLock sb
            Return sb.ToString()
        End SyncLock
    End Function

    ''' <summary>Reads the KeyValues block "&lt;appid&gt;" { ... } from the SteamCMD output.</summary>
    Friend Sub Parse(output As String)
        Dim matches = Regex.Matches(output, """" & AppId & """\s*\{")
        If matches.Count = 0 Then
            Found = False
            ErrorText = If(output.IndexOf("No app info", StringComparison.OrdinalIgnoreCase) >= 0,
                           "Steam kennt diese App-ID nicht (oder sie ist nicht öffentlich).",
                           "Keine App-Infos erhalten. Ist SteamCMD aktuell und besteht eine Internetverbindung?")
            Return
        End If
        ' The last block is the complete one
        Dim start = matches(matches.Count - 1).Index
        Dim root = KeyValues.Parse(output.Substring(start))
        Dim app = TryCast(KeyValues.GetChild(root, AppId.ToString()), Dictionary(Of String, Object))
        If app Is Nothing Then
            ErrorText = "Die App-Infos konnten nicht gelesen werden."
            Return
        End If
        Dim common = TryCast(KeyValues.GetChild(app, "common"), Dictionary(Of String, Object))
        Name = KeyValues.GetText(common, "name")
        AppType = KeyValues.GetText(common, "type")
        OsList = KeyValues.GetText(common, "oslist")
        Dim launch = TryCast(KeyValues.GetChild(TryCast(KeyValues.GetChild(app, "config"), Dictionary(Of String, Object)), "launch"), Dictionary(Of String, Object))
        If launch IsNot Nothing Then
            For Each entry In launch.Values
                Dim item = TryCast(entry, Dictionary(Of String, Object))
                If item Is Nothing Then Continue For
                Dim config = TryCast(KeyValues.GetChild(item, "config"), Dictionary(Of String, Object))
                Launches.Add(New LaunchOption With {
                    .Executable = KeyValues.GetText(item, "executable").Replace("/", "\"),
                    .Arguments = KeyValues.GetText(item, "arguments"),
                    .Description = KeyValues.GetText(item, "description"),
                    .OsList = KeyValues.GetText(config, "oslist")})
            Next
        End If
        Found = Name.Length > 0 OrElse Launches.Count > 0
        If Not Found Then ErrorText = "Steam liefert für diese App-ID keine Daten (evtl. nur mit Login sichtbar)."
    End Sub

End Class


''' <summary>Minimal reader for Valve's KeyValues text format ("key" "value" / "key" { ... }).</summary>
Friend NotInheritable Class KeyValues

    Private Sub New()
    End Sub

    Public Shared Function Parse(text As String) As Dictionary(Of String, Object)
        Dim pos = 0
        Return ReadBlock(text, pos, False)
    End Function

    Private Shared Function ReadBlock(text As String, ByRef pos As Integer, nested As Boolean) As Dictionary(Of String, Object)
        Dim result As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)
        Do
            Dim key = ReadToken(text, pos)
            If key Is Nothing Then Return result
            If key = "}" Then
                If nested Then Return result
                Continue Do
            End If
            If key = "{" Then Continue Do
            Dim value = ReadToken(text, pos)
            If value Is Nothing Then Return result
            If value = "{" Then
                result(key) = ReadBlock(text, pos, True)
                ' Only the first top level block is needed
                If Not nested Then Return result
            ElseIf value = "}" Then
                If nested Then Return result
            Else
                result(key) = value
            End If
        Loop
    End Function

    ''' <summary>Next quoted string, "{" or "}"; Nothing at the end. Text outside quotes is skipped.</summary>
    Private Shared Function ReadToken(text As String, ByRef pos As Integer) As String
        While pos < text.Length
            Dim c = text(pos)
            If c = "{"c OrElse c = "}"c Then
                pos += 1
                Return c.ToString()
            End If
            If c = """"c Then
                Dim sb As New StringBuilder()
                pos += 1
                While pos < text.Length AndAlso text(pos) <> """"c
                    If text(pos) = "\"c AndAlso pos + 1 < text.Length Then
                        pos += 1
                        Dim escaped = text(pos)
                        sb.Append(If(escaped = "n"c, ChrW(10), If(escaped = "t"c, ChrW(9), escaped)))
                    Else
                        sb.Append(text(pos))
                    End If
                    pos += 1
                End While
                pos += 1
                Return sb.ToString()
            End If
            pos += 1
        End While
        Return Nothing
    End Function

    Public Shared Function GetChild(block As Dictionary(Of String, Object), key As String) As Object
        If block Is Nothing Then Return Nothing
        Dim value As Object = Nothing
        block.TryGetValue(key, value)
        Return value
    End Function

    Public Shared Function GetText(block As Dictionary(Of String, Object), key As String) As String
        Return If(TryCast(GetChild(block, key), String), "")
    End Function

End Class


''' <summary>Finds the start program of a server in its install folder.</summary>
Friend NotInheritable Class ExeFinder

    Private Sub New()
    End Sub

    Private Shared ReadOnly Skip As String() = {
        "crashreport", "crashhandler", "unrealcefsubprocess", "vc_redist", "vcredist", "dxsetup", "directx", "uninstall", "unins0",
        "steamcmd", "steamservice", "steamerrorreporter", "eac", "easyanticheat", "battleye", "be_service", "dotnet", "createdump",
        "ue4prereq", "ueprereq", "setup", "installer", "updater", "launcherhelper", "cefprocess", "ffmpeg"}

    ''' <summary>Candidates relative to the folder, best first.</summary>
    Public Shared Function Find(folder As String, Optional hint As String = "") As List(Of String)
        Dim result As New List(Of Tuple(Of Integer, String))()
        If Not Directory.Exists(folder) Then Return New List(Of String)()
        If hint.Length > 0 AndAlso File.Exists(Path.Combine(folder, hint)) Then Return New List(Of String) From {hint}

        Dim files As New List(Of String)()
        Try
            files.AddRange(Directory.GetFiles(folder, "*.exe", SearchOption.AllDirectories))
            files.AddRange(Directory.GetFiles(folder, "*.bat", SearchOption.TopDirectoryOnly))
        Catch
        End Try

        For Each fullPath As String In files
            Dim relative = fullPath.Substring(folder.Length).TrimStart("\"c, "/"c)
            Dim lower = relative.ToLowerInvariant()
            If relative.StartsWith("steamapps", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim fileName = Path.GetFileNameWithoutExtension(lower)
            If Skip.Any(Function(word) fileName.Contains(word)) Then Continue For
            If lower.Contains("\engine\") OrElse lower.Contains("\redist") OrElse lower.Contains("_commonredist") Then Continue For

            Dim score = 0
            If fileName.Contains("server") Then score += 50
            If fileName.Contains("dedicated") Then score += 40
            If fileName.EndsWith("ds") Then score += 25
            If fileName.Contains("shipping") Then score += 10
            If fileName.Contains("win64") Then score += 8
            If fileName.Contains("start") AndAlso lower.EndsWith(".bat") Then score += 30
            If fileName.Contains("debug") OrElse fileName.Contains("test") Then score -= 5
            If lower.EndsWith(".bat") AndAlso score < 30 Then Continue For
            ' Shallow files are more likely the real start program (UE "launchers" sit at the top)
            score -= relative.Count(Function(c) c = "\"c) * 3
            result.Add(Tuple.Create(score, relative))
        Next
        Return result.OrderByDescending(Function(t) t.Item1).ThenBy(Function(t) t.Item2.Length).Select(Function(t) t.Item2).Take(12).ToList()
    End Function

End Class
