Option Explicit
Dim shell, files, root, app
Set shell = CreateObject("WScript.Shell")
Set files = CreateObject("Scripting.FileSystemObject")
root = files.GetParentFolderName(WScript.ScriptFullName)
app = root & "\artifacts\app\Threadlet.exe"
If Not files.FileExists(app) Then
    MsgBox "Threadlet has not been packaged yet. Run Start-Threadlet.bat first.", 48, "Threadlet"
    WScript.Quit 1
End If
shell.Run """" & app & """", 1, False
