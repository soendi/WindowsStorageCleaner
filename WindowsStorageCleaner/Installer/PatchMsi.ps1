param(
    [Parameter(Mandatory=$true)]
    [string]$MsiPath
)

try {
    $msi = New-Object -ComObject WindowsInstaller.Installer
    $db = $msi.OpenDatabase($MsiPath, 1)

    # Helper: update Text column via Modify (handles long strings)
    function Update-Control {
        param([string]$dialog, [string]$control, [string]$text)
        $view = $db.OpenView("SELECT `Dialog_`, `Control`, `Type`, `X`, `Y`, `Width`, `Height`, `Attributes`, `Property`, `Text`, `Control_Next`, `Help` FROM `Control` WHERE `Dialog_`='$dialog' AND `Control`='$control'")
        $view.Execute()
        $r = $view.Fetch()
        if ($r) {
            $r.StringData(10) = $text
            $view.Modify(3, $r)  # MSIMODIFY_UPDATE = 3
        }
        $view.Close()
    }

    Write-Output "=== Patching LicenseAgreementDlg controls ==="
    $rtfContent = Get-Content -Path "$PSScriptRoot\License.rtf" -Raw
    Update-Control "LicenseAgreementDlg" "LicenseText" $rtfContent
    Update-Control "LicenseAgreementDlg" "Title" "{\WixUI_Font_Title}Lizenzvereinbarung"
    Update-Control "LicenseAgreementDlg" "Description" "Bitte lesen Sie die Lizenzvereinbarung sorgfältig durch"
    Update-Control "LicenseAgreementDlg" "LicenseAcceptedCheckBox" "Ich stimme den Bedingungen der Lizenzvereinbarung zu"
    Update-Control "LicenseAgreementDlg" "Print" "Drucken"
    Update-Control "LicenseAgreementDlg" "Next" "Weiter"
    Update-Control "LicenseAgreementDlg" "Back" "Zurück"
    Update-Control "LicenseAgreementDlg" "Cancel" "Abbrechen"
    Write-Output "LicenseAgreementDlg done"

    Write-Output "=== Patching WelcomeDlg controls ==="
    Update-Control "WelcomeDlg" "Title" "{\WixUI_Font_Title}Willkommen beim Setup von [ProductName]"
    Update-Control "WelcomeDlg" "Description" "Der Installationsassistent führt Sie durch die Installation von [ProductName]."
    Update-Control "WelcomeDlg" "WelcomeLabel" "Willkommen"
    Update-Control "WelcomeDlg" "WelcomeTitle" "Willkommen beim Setup von [ProductName]"
    Update-Control "WelcomeDlg" "Next" "Weiter"
    Update-Control "WelcomeDlg" "Back" "Zurück"
    Update-Control "WelcomeDlg" "Cancel" "Abbrechen"
    Update-Control "WelcomeDlg" "PatchDescription" "Der Installationsassistent wird [ProductName] auf Ihrem Computer aktualisieren. Klicken Sie auf Weiter, um fortzufahren, oder auf Abbrechen, um den Assistenten zu beenden."
    Write-Output "WelcomeDlg done"

    Write-Output "=== Patching CustomizeDlg controls ==="
    Update-Control "CustomizeDlg" "Title" "{\WixUI_Font_Title}Installationsumfang"
    Update-Control "CustomizeDlg" "Description" "Wählen Sie die zu installierenden Komponenten aus."
    Update-Control "CustomizeDlg" "Text" "Klicken Sie auf die Symbole in der Baumstruktur, um die Installation anzupassen."
    Update-Control "CustomizeDlg" "Browse" "Durchsuchen..."
    Update-Control "CustomizeDlg" "Reset" "Zurücksetzen"
    Update-Control "CustomizeDlg" "DiskCost" "Speicherplatz"
    Update-Control "CustomizeDlg" "LocationLabel" "Ziel:"
    Update-Control "CustomizeDlg" "Next" "Weiter"
    Update-Control "CustomizeDlg" "Back" "Zurück"
    Update-Control "CustomizeDlg" "Cancel" "Abbrechen"
    Write-Output "CustomizeDlg done"

    Write-Output "=== Patching VerifyReadyDlg controls ==="
    Update-Control "VerifyReadyDlg" "Title" "{\WixUI_Font_Title}Installation bereit"
    Update-Control "VerifyReadyDlg" "Description" "Der Installationsassistent ist bereit für die Installation."
    Update-Control "VerifyReadyDlg" "InstallTitle" "Bereit zur Installation von [ProductName]"
    Update-Control "VerifyReadyDlg" "InstallText" "Klicken Sie auf Installieren, um die Installation zu starten."
    Update-Control "VerifyReadyDlg" "ChangeTitle" "Bereit zum ändern von [ProductName]"
    Update-Control "VerifyReadyDlg" "ChangeText" "Klicken Sie auf ändern, um die Einstellungen anzupassen."
    Update-Control "VerifyReadyDlg" "RepairTitle" "Bereit zur Reparatur von [ProductName]"
    Update-Control "VerifyReadyDlg" "RepairText" "Klicken Sie auf Reparieren, um die Installation zu reparieren."
    Update-Control "VerifyReadyDlg" "RemoveTitle" "Bereit zum Entfernen von [ProductName]"
    Update-Control "VerifyReadyDlg" "RemoveText" "Klicken Sie auf Entfernen, um [ProductName] zu deinstallieren."
    Update-Control "VerifyReadyDlg" "UpdateTitle" "Bereit zum Aktualisieren von [ProductName]"
    Update-Control "VerifyReadyDlg" "UpdateText" "Klicken Sie auf Aktualisieren, um [ProductName] zu aktualisieren."
    Update-Control "VerifyReadyDlg" "Install" "Installieren"
    Update-Control "VerifyReadyDlg" "Change" "ändern"
    Update-Control "VerifyReadyDlg" "Repair" "Reparieren"
    Update-Control "VerifyReadyDlg" "Remove" "Entfernen"
    Update-Control "VerifyReadyDlg" "Update" "Aktualisieren"
    Update-Control "VerifyReadyDlg" "InstallNoShield" "Installieren"
    Update-Control "VerifyReadyDlg" "ChangeNoShield" "ändern"
    Update-Control "VerifyReadyDlg" "RepairNoShield" "Reparieren"
    Update-Control "VerifyReadyDlg" "RemoveNoShield" "Entfernen"
    Update-Control "VerifyReadyDlg" "UpdateNoShield" "Aktualisieren"
    Update-Control "VerifyReadyDlg" "Back" "Zurück"
    Update-Control "VerifyReadyDlg" "Cancel" "Abbrechen"
    Write-Output "VerifyReadyDlg done"

    Write-Output "=== Patching ProgressDlg controls ==="
    Update-Control "ProgressDlg" "Title" "{\WixUI_Font_Title}Installation wird ausgeführt"
    Update-Control "ProgressDlg" "Description" "Die Installation wird ausgeführt."
    Update-Control "ProgressDlg" "StatusLabel" "Status:"
    Update-Control "ProgressDlg" "Cancel" "Abbrechen"
    Update-Control "ProgressDlg" "TextInstalling" "Bitte warten Sie, während [ProductName] installiert wird."
    Update-Control "ProgressDlg" "TitleInstalling" "{\WixUI_Font_Title}[ProductName] wird installiert"
    Update-Control "ProgressDlg" "TextChanging" "Bitte warten Sie, während [ProductName] geändert wird."
    Update-Control "ProgressDlg" "TitleChanging" "{\WixUI_Font_Title}[ProductName] wird geändert"
    Update-Control "ProgressDlg" "TextRepairing" "Bitte warten Sie, während [ProductName] repariert wird."
    Update-Control "ProgressDlg" "TitleRepairing" "{\WixUI_Font_Title}[ProductName] wird repariert"
    Update-Control "ProgressDlg" "TextRemoving" "Bitte warten Sie, während [ProductName] entfernt wird."
    Update-Control "ProgressDlg" "TitleRemoving" "{\WixUI_Font_Title}[ProductName] wird entfernt"
    Update-Control "ProgressDlg" "TextUpdating" "Bitte warten Sie, während [ProductName] aktualisiert wird."
    Update-Control "ProgressDlg" "TitleUpdating" "{\WixUI_Font_Title}[ProductName] wird aktualisiert"
    Update-Control "CancelDlg" "Text" "Möchten Sie die Installation wirklich abbrechen?"
    Update-Control "CancelDlg" "Title" "{\WixUI_Font_Title}Installation abbrechen"
    Write-Output "ProgressDlg done"

    Write-Output "=== Patching ExitDialog controls ==="
    Update-Control "ExitDialog" "Title" "{\WixUI_Font_Bigger}[ProductName] Setup abgeschlossen"
    Update-Control "ExitDialog" "Description" "Die Installation wurde erfolgreich abgeschlossen."
    Update-Control "ExitDialog" "Finish" "Fertigstellen"
    Update-Control "ExitDialog" "Back" "Zurück"
    Update-Control "ExitDialog" "Cancel" "Schließen"
    Write-Output "ExitDialog done"

    Write-Output "=== Patching Maintenance dialogs ==="
    Update-Control "MaintenanceTypeDlg" "Title" "{\WixUI_Font_Title}Wartung"
    Update-Control "MaintenanceTypeDlg" "Description" "Wählen Sie die gewünschte Aktion aus."
    Update-Control "MaintenanceTypeDlg" "ChangeButton" "ändern"
    Update-Control "MaintenanceTypeDlg" "RepairButton" "Reparieren"
    Update-Control "MaintenanceTypeDlg" "RemoveButton" "Entfernen"
    Update-Control "MaintenanceTypeDlg" "Cancel" "Abbrechen"
    Update-Control "MaintenanceWelcomeDlg" "Title" "{\WixUI_Font_Title}Willkommen bei der Wartung"
    Update-Control "MaintenanceWelcomeDlg" "Description" "Wählen Sie die gewünschte Aktion aus."
    Update-Control "MaintenanceWelcomeDlg" "Next" "Weiter"
    Update-Control "MaintenanceWelcomeDlg" "Cancel" "Abbrechen"
    Write-Output "Maintenance dialogs done"

    Write-Output "=== Patching InstallDirDlg ==="
    Update-Control "InstallDirDlg" "Title" "{\WixUI_Font_Title}Zielordner"
    Update-Control "InstallDirDlg" "Description" "Wählen Sie den Installationsordner."
    Update-Control "InstallDirDlg" "FolderLabel" "Zielordner:"
    Update-Control "InstallDirDlg" "FolderTitle" "Installationsverzeichnis"
    Update-Control "InstallDirDlg" "Browse" "Durchsuchen..."
    Update-Control "InstallDirDlg" "Next" "Weiter"
    Update-Control "InstallDirDlg" "Back" "Zurück"
    Update-Control "InstallDirDlg" "Cancel" "Abbrechen"

    Write-Output "=== Patching DiskCostDlg ==="
    Update-Control "DiskCostDlg" "Title" "{\WixUI_Font_Title}Speicherplatz"
    Update-Control "DiskCostDlg" "Description" "überprüfen Sie den verfügbaren Speicherplatz."
    Update-Control "DiskCostDlg" "Next" "OK"
    Update-Control "DiskCostDlg" "Cancel" "Abbrechen"

    Write-Output "=== Patching BrowseDlg ==="
    Update-Control "BrowseDlg" "Title" "{\WixUI_Font_Title}Ordner suchen"
    Update-Control "BrowseDlg" "Description" "Wählen Sie einen Ordner aus."
    Update-Control "BrowseDlg" "BrowseLabel" "Ordner:"
    Update-Control "BrowseDlg" "OK" "OK"
    Update-Control "BrowseDlg" "Cancel" "Abbrechen"
    Update-Control "BrowseDlg" "FolderExist" "Der Ordner existiert bereits."

    $db.Commit()
    Write-Output "=== ALL CHANGES COMMITTED SUCCESSFULLY ==="
}
catch {
    Write-Output "ERROR: $_"
    exit 1
}
