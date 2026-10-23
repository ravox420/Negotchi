Option Explicit
Dim fileSystem, shell, root, executable, buildScript, command, result, logPath
Set fileSystem = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")
root = fileSystem.GetParentFolderName(WScript.ScriptFullName)
executable = root & "\.build\Negotchi.exe"
buildScript = root & "\desktop\build.ps1"
If Not fileSystem.FileExists(executable) Then
    logPath = root & "\.build-error.txt"
    command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command " & Chr(34) & "& '" & Replace(buildScript, "'", "''") & "' *> '" & Replace(logPath, "'", "''") & "'; if (-not $?) { exit 1 }" & Chr(34)
    result = shell.Run(command, 0, True)
    If result <> 0 Or Not fileSystem.FileExists(executable) Then
        MsgBox "Nao foi possivel preparar o Negotchi. Consulte .build-error.txt na pasta do projeto.", vbExclamation, "Negotchi"
        WScript.Quit 1
    End If
    If fileSystem.FileExists(logPath) Then fileSystem.DeleteFile logPath
End If
shell.CurrentDirectory = root
shell.Run Chr(34) & executable & Chr(34), 1, False

