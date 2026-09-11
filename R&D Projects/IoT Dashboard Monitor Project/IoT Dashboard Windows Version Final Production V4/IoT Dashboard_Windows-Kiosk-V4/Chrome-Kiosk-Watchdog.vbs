Option Explicit

Dim shell, fso
Dim dashboardUrl, profileDir, kioskRoot, logFile, chromeExe
Dim commandLine

Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")

dashboardUrl = "https://web.itank.io/login/"
kioskRoot = "C:\Kiosk"
profileDir = "C:\Kiosk\ChromeProfile"
logFile = "C:\Kiosk\kiosk.log"

If Not fso.FolderExists(kioskRoot) Then
    fso.CreateFolder kioskRoot
End If

If Not fso.FolderExists(profileDir) Then
    fso.CreateFolder profileDir
End If

chromeExe = FindChrome()

If chromeExe = "" Then
    WriteLog "ERROR: Google Chrome was not found."
    WScript.Quit 2
End If

WriteLog "V4 watchdog started. User=" & shell.ExpandEnvironmentStrings("%USERNAME%")
WriteLog "Chrome=" & chromeExe

' Allow Windows Explorer and networking to settle after automatic logon.
WScript.Sleep 10000

Do
    If Not IsKioskChromeRunning(profileDir) Then
        commandLine = Quote(chromeExe) _
            & " --kiosk" _
            & " --start-fullscreen" _
            & " --no-first-run" _
            & " --no-default-browser-check" _
            & " --noerrdialogs" _
            & " --disable-session-crashed-bubble" _
            & " --disable-translate" _
            & " --disable-features=TranslateUI" _
            & " --overscroll-history-navigation=0" _
            & " --autoplay-policy=no-user-gesture-required" _
            & " --user-data-dir=" & Quote(profileDir) _
            & " " & Quote(dashboardUrl)

        WriteLog "Starting Chrome."
        shell.Run commandLine, 1, False
        WScript.Sleep 5000
    Else
        WScript.Sleep 5000
    End If
Loop

Function FindChrome()
    Dim candidates, item

    candidates = Array( _
        shell.ExpandEnvironmentStrings("%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"), _
        shell.ExpandEnvironmentStrings("%ProgramFiles%\Google\Chrome\Application\chrome.exe"), _
        shell.ExpandEnvironmentStrings("%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe") _
    )

    For Each item In candidates
        If fso.FileExists(item) Then
            FindChrome = item
            Exit Function
        End If
    Next

    FindChrome = ""
End Function

Function IsKioskChromeRunning(profile)
    On Error Resume Next

    Dim service, processes, process
    Set service = GetObject("winmgmts:\\.\root\cimv2")
    Set processes = service.ExecQuery( _
        "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='chrome.exe'" _
    )

    IsKioskChromeRunning = False

    For Each process In processes
        If Not IsNull(process.CommandLine) Then
            If InStr(1, process.CommandLine, profile, vbTextCompare) > 0 Then
                IsKioskChromeRunning = True
                Exit For
            End If
        End If
    Next

    On Error GoTo 0
End Function

Function Quote(value)
    Quote = Chr(34) & value & Chr(34)
End Function

Sub WriteLog(message)
    On Error Resume Next

    Dim stream
    Set stream = fso.OpenTextFile(logFile, 8, True)
    stream.WriteLine "[" & Now & "] " & message
    stream.Close

    On Error GoTo 0
End Sub
