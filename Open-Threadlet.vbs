Option Explicit
Dim shell, files, root, app, result
Set shell = CreateObject("WScript.Shell")
Set files = CreateObject("Scripting.FileSystemObject")
root = files.GetParentFolderName(WScript.ScriptFullName)
app = root & "\artifacts\app\Threadlet.exe"
If Not files.FileExists(app) Then
    MsgBox "Threadlet has not been packaged yet. Run Start-Threadlet.bat or build the Release package.", 48, "Threadlet"
    WScript.Quit 1
End If
If files.FileExists(root & "\vm-config.cmd") Then
    result = shell.Run("cmd.exe /d /c """ & root & "\Start-VM.cmd""", 0, True)
    If result <> 0 Then
        MsgBox "The background VM could not start. Check vm-config.cmd. Threadlet will still open.", 48, "Threadlet"
    End If
End If
shell.Run """" & app & """", 1, False
