Imports System.IO.Compression
Imports System.Net.Http
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading

Friend Enum LineKind
    Normal
    Info
    Success
    Warning
    [Error]
    Prompt
End Enum

''' <summary>Downloads SteamCMD and runs it with live output, progress and input for Steam Guard codes.</summary>
Friend Class SteamCmdRunner

    Public Const DownloadUrl As String = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"

    Public Event OutputLine(text As String, kind As LineKind)
    Public Event ProgressChanged(percent As Double, status As String)
    Public Event Finished(exitCode As Integer, success As Boolean, summary As String)
    Public Event RunningChanged()

    Private Shared ReadOnly ProgressPattern As New Regex("Update state \(0x[0-9a-f]+\) ([a-z ]+), progress: ([0-9.]+)", RegexOptions.IgnoreCase)

    Private steamProcess As Process
    Private context As SynchronizationContext
    Private sawSuccess As Boolean
    Private lastError As String = ""
    Private needsLoginHint As Boolean

    ''' <summary>True when the last run failed because the server needs a Steam account that owns the game.</summary>
    Public ReadOnly Property LastRunNeededLogin As Boolean
        Get
            Return needsLoginHint
        End Get
    End Property

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return steamProcess IsNot Nothing
        End Get
    End Property

    ' ---------- Download SteamCMD ----------

    Public Shared Async Function DownloadAsync(targetFolder As String, progress As IProgress(Of Double)) As Task
        Directory.CreateDirectory(targetFolder)
        Using client As New HttpClient() With {.Timeout = TimeSpan.FromMinutes(5)}
            Using response = Await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead)
                response.EnsureSuccessStatusCode()
                Dim total = response.Content.Headers.ContentLength
                Using input = Await response.Content.ReadAsStreamAsync()
                    Using memory As New MemoryStream()
                        Dim buffer(81919) As Byte
                        Do
                            Dim read = Await input.ReadAsync(buffer, 0, buffer.Length)
                            If read = 0 Then Exit Do
                            memory.Write(buffer, 0, read)
                            If total.HasValue AndAlso total.Value > 0 Then progress?.Report(memory.Length * 100.0 / total.Value)
                        Loop
                        memory.Position = 0
                        Using archive As New ZipArchive(memory, ZipArchiveMode.Read)
                            For Each entry As ZipArchiveEntry In archive.Entries
                                If String.IsNullOrEmpty(entry.Name) Then Continue For
                                Dim target = Path.Combine(targetFolder, entry.FullName)
                                Directory.CreateDirectory(Path.GetDirectoryName(target))
                                Using source = entry.Open()
                                    Using output As New FileStream(target, FileMode.Create, FileAccess.Write)
                                        Await source.CopyToAsync(output)
                                    End Using
                                End Using
                            Next
                        End Using
                    End Using
                End Using
            End Using
        End Using
        progress?.Report(100)
    End Function

    ' ---------- Arguments ----------

    ''' <summary>Builds the SteamCMD command line. The second value hides the password for display.</summary>
    Public Shared Function BuildInstallArguments(server As GameServer, installDir As String, user As String, password As String,
                                                 validate As Boolean) As (Arguments As String, Display As String)
        Dim login As String
        Dim loginDisplay As String
        If server.NeedsLogin AndAlso Not String.IsNullOrWhiteSpace(user) Then
            login = "+login " & Quote(user) & If(String.IsNullOrEmpty(password), "", " " & Quote(password))
            loginDisplay = "+login " & user & If(String.IsNullOrEmpty(password), "", " ********")
        Else
            login = "+login anonymous"
            loginDisplay = login
        End If

        Dim update = "+app_update " & server.AppId
        If Not String.IsNullOrWhiteSpace(server.Beta) Then update &= " -beta " & server.Beta.Trim()
        If validate Then update &= " validate"

        ' force_install_dir has to come before the login, otherwise SteamCMD warns and may ignore it
        Dim prefix = "+force_install_dir " & Quote(installDir) & " "
        Return (prefix & login & " " & update & " +quit", prefix & loginDisplay & " " & update & " +quit")
    End Function

    Private Shared Function Quote(value As String) As String
        Return """" & value.Replace("""", "") & """"
    End Function

    ' ---------- Running ----------

    Public Sub Start(steamCmdExe As String, arguments As String, displayArguments As String)
        If IsRunning Then Throw New InvalidOperationException("SteamCMD läuft bereits.")
        context = If(SynchronizationContext.Current, New SynchronizationContext())
        sawSuccess = False
        lastError = ""
        needsLoginHint = False

        Dim psi As New ProcessStartInfo(steamCmdExe, arguments) With {
            .WorkingDirectory = Path.GetDirectoryName(steamCmdExe),
            .UseShellExecute = False,
            .RedirectStandardInput = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True,
            .StandardOutputEncoding = Encoding.UTF8,
            .StandardErrorEncoding = Encoding.UTF8
        }
        Dim p As New Process() With {.StartInfo = psi, .EnableRaisingEvents = True}
        p.Start()
        steamProcess = p
        RaiseEvent RunningChanged()
        Emit("> steamcmd " & displayArguments, LineKind.Info)

        Dim stdoutReader = p.StandardOutput
        Dim stderrReader = p.StandardError
        Task.Run(Sub() ReadStream(stdoutReader))
        Task.Run(Sub() ReadStream(stderrReader))
        Task.Run(Sub() WaitForExit(p))
    End Sub

    Public Sub SendInput(text As String)
        Try
            If steamProcess Is Nothing OrElse steamProcess.HasExited Then Return
            steamProcess.StandardInput.WriteLine(text)
            steamProcess.StandardInput.Flush()
        Catch
        End Try
    End Sub

    Public Sub Cancel()
        Try
            If steamProcess IsNot Nothing AndAlso Not steamProcess.HasExited Then steamProcess.Kill()
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Reads character by character so progress lines ending in \r and prompts without
    ''' a line break (Steam Guard code, password) show up immediately.
    ''' </summary>
    Private Sub ReadStream(reader As StreamReader)
        Dim line As New StringBuilder()
        Dim buffer(4095) As Char
        Try
            Do
                Dim count = reader.Read(buffer, 0, buffer.Length)
                If count <= 0 Then Exit Do
                For i = 0 To count - 1
                    Dim c = buffer(i)
                    If c = ChrW(13) OrElse c = ChrW(10) Then
                        If line.Length > 0 Then HandleLine(line.ToString())
                        line.Clear()
                    Else
                        line.Append(c)
                    End If
                Next
                ' A prompt waits for input without sending a line break
                If line.Length > 0 AndAlso IsPrompt(line.ToString()) Then
                    HandleLine(line.ToString())
                    line.Clear()
                End If
            Loop
            If line.Length > 0 Then HandleLine(line.ToString())
        Catch
            ' Stream closed
        End Try
    End Sub

    Private Shared Function IsPrompt(text As String) As Boolean
        Dim t = text.TrimEnd()
        If Not t.EndsWith(":") Then Return False
        Return t.IndexOf("code", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               t.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Sub HandleLine(raw As String)
        Dim text = raw.Trim()
        If text.Length = 0 Then Return

        Dim m = ProgressPattern.Match(text)
        If m.Success Then
            Dim percent As Double
            Double.TryParse(m.Groups(2).Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, percent)
            Dim status = TranslateState(m.Groups(1).Value.Trim())
            Post(Sub() RaiseProgress(percent, status))
            Emit(text, LineKind.Normal)
            Return
        End If

        Dim kind = LineKind.Normal
        If IsPrompt(text) Then
            kind = LineKind.Prompt
        ElseIf text.StartsWith("Success!", StringComparison.OrdinalIgnoreCase) Then
            sawSuccess = True
            kind = LineKind.Success
        ElseIf text.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 OrElse text.StartsWith("FAILED", StringComparison.OrdinalIgnoreCase) Then
            lastError = text
            kind = LineKind.Error
            If text.IndexOf("No subscription", StringComparison.OrdinalIgnoreCase) >= 0 Then needsLoginHint = True
        ElseIf text.StartsWith("Warning", StringComparison.OrdinalIgnoreCase) Then
            kind = LineKind.Warning
        End If
        Emit(text, kind)
    End Sub

    Private Shared Function TranslateState(state As String) As String
        Select Case state.ToLowerInvariant()
            Case "downloading" : Return "Lade herunter"
            Case "verifying install", "verifying update" : Return "Prüfe Dateien"
            Case "preallocating" : Return "Reserviere Speicher"
            Case "committing" : Return "Schreibe Dateien"
            Case "reconfiguring" : Return "Konfiguriere"
            Case Else : Return state
        End Select
    End Function

    Private Sub WaitForExit(p As Process)
        p.WaitForExit()
        ' Let the readers finish the last lines
        Thread.Sleep(300)
        Dim code = 0
        Try
            code = p.ExitCode
        Catch
        End Try
        p.Dispose()

        Dim success = sawSuccess OrElse (code = 0 AndAlso lastError.Length = 0)
        Dim summary As String
        If success Then
            summary = "Fertig."
        ElseIf needsLoginHint Then
            summary = "Für diesen Server brauchst du einen Steam-Account, der das Spiel besitzt (Einstellungen > Steam-Login)."
        ElseIf lastError.Length > 0 Then
            summary = lastError
        Else
            summary = "SteamCMD wurde mit Code " & code & " beendet."
        End If

        Post(Sub() RaiseFinished(code, success, summary))
    End Sub

    Private Sub Emit(text As String, kind As LineKind)
        Post(Sub() RaiseOutput(text, kind))
    End Sub

    ' Events are raised from these helpers, called on the UI thread
    Private Sub RaiseOutput(text As String, kind As LineKind)
        RaiseEvent OutputLine(text, kind)
    End Sub

    Private Sub RaiseProgress(percent As Double, status As String)
        RaiseEvent ProgressChanged(percent, status)
    End Sub

    Private Sub RaiseFinished(code As Integer, success As Boolean, summary As String)
        steamProcess = Nothing
        RaiseEvent RunningChanged()
        RaiseEvent Finished(code, success, summary)
    End Sub

    Private Sub Post(action As Action)
        context.Post(Sub(state) action(), Nothing)
    End Sub

End Class
