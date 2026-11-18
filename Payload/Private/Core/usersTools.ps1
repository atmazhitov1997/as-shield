function createNewUser {
    <#
    .SYNOPSIS
        Removes Windows Terminal and PowerShell from the Win+X menu
    #>
    [CmdletBinding()]
    param()

    Clear-Host

    Write-Host "┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Yellow
    Write-Host "│##               NOTE: the user's working folders are not created when the account is created.                     ##│" -ForegroundColor Yellow
    Write-Host "│##               They are created only when the user signs in for the first time.                                  ##│" -ForegroundColor Yellow
    Write-Host "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow
 
 # List all users and their groups
    Write-Host "`n──────────────────────────────────────────────────" -ForegroundColor Cyan
    Write-Host "  Users on this system:" -ForegroundColor Cyan
    Write-Host "──────────────────────────────────────────────────" -ForegroundColor Cyan

    $allUsers = Get-LocalUser

    foreach ($user in $allUsers) {
        $userGroups = Get-LocalGroup | Where-Object {
            (Get-LocalGroupMember -Group $_.Name -ErrorAction SilentlyContinue).Name -contains "$env:COMPUTERNAME\$($user.Name)"
        } | Select-Object -ExpandProperty Name

        $groupsString = if ($userGroups) { $userGroups -join ", " } else { "No group" }

        Write-Host "  $($user.Name) - $groupsString" -ForegroundColor White
    }

    Write-Host "──────────────────────────────────────────────────`n" -ForegroundColor Cyan

    $choice = (Read-Host "To continue creating the user enter 'Y'`nTo return to the main menu enter '0'`nTo exit the script enter 'X'`nYour choice").ToUpper()
        switch ($choice) {
            "Y" { continue }
            "0" { Start-Menu
                return}
            "X" { Script-end
                return}
            default { Write-Host "Invalid choice, try again."; End}
        }

    while ($true) { # Loop until the name is unique

    $Script:newUserName = Read-Host "Enter the new user name"

    $userExist = Get-LocalUser -Name $Script:newUserName -ErrorAction SilentlyContinue

    if ($userExist) {
        Write-Host "An account named '$Script:newUserName' already exists on this system" -ForegroundColor Green

    } else {
        Write-Host "Account $Script:newUserName will be created" -ForegroundColor Green
        break # Leave the loop when the user does not exist
        }
    }

    $newUserFullName = Read-Host "Enter the user's full name"

    $newUserDescription = Read-Host "Enter the user description"

    # Password check
    $passwordMatch = $false

    while ($passwordMatch -ne $true) {

            $newUserPassword = Read-Host "Enter the user password" -MaskInput

            $newUserPasswordConfirm = Read-Host "Confirm the user password" -MaskInput

            if ($newUserPassword -eq "") {
            Write-Host "---------------------------------------------------------------------------" -ForegroundColor DarkYellow
            Write-Host "                   The password cannot be empty                   " -ForegroundColor Red
            Write-Host "---------------------------------------------------------------------------" -ForegroundColor DarkYellow
            $passwordMatch = $false
            } else {
            if ($newUserPassword -eq $newUserPasswordConfirm) {

                Write-Host "Passwords match" -ForegroundColor Green

                $passwordMatch = $true

                } else {

                Write-Host "Passwords do not match. Please try again." -ForegroundColor Red

                $passwordMatch = $false
                        }
                }
            }

        # Convert the password to a SecureString
        $newUserSecurePassword = ConvertTo-SecureString $newUserPassword -AsPlainText -Force

        # Create the user
        New-LocalUser -Name $Script:newUserName -Password $newUserSecurePassword -FullName $newUserFullName -Description $newUserDescription | Out-Null

        # Add the user to the "Users" group by default
        Add-LocalGroupMember -Group "Users" -Member $Script:newUserName
        Write-Host "User '$Script:newUserName' added to the 'Users' group." -ForegroundColor Green

        # ─── GROUP SELECTION ────────────────────────────────────────────────────────
        Write-Host "`n──────────────────────────────────────────────────" -ForegroundColor Cyan
        $groupChoice = (Read-Host "Add the user to the Administrators group?`n  [A] - Administrators`n  [U] - Keep in the Users group`nYour choice").ToUpper()

        switch ($groupChoice) {
            "A" {
                try {
                    Add-LocalGroupMember -Group "Administrators" -Member $Script:newUserName -ErrorAction Stop
                    Remove-LocalGroupMember -Group "Users" -Member $Script:newUserName -ErrorAction Stop
                    Write-Host "User '$Script:newUserName' moved to the 'Administrators' group and removed from the 'Users' group." -ForegroundColor Green
                }
                catch {
                    Write-Host "Error while changing the user's group:" -ForegroundColor Red
                    Write-Host $_.Exception.Message -ForegroundColor Yellow
                }
            }
            "U" {
                Write-Host "User '$Script:newUserName' stays in the 'Users' group." -ForegroundColor Green
            }
            default {
                Write-Host "Invalid choice. The user stays in the 'Users' group." -ForegroundColor DarkYellow
            }
        }
        # ────────────────────────────────────────────────────────────────────────────

        Write-Host "User '$Script:newUserName' created successfully!" -ForegroundColor Green

        # Force the user to change the password at the next logon
        try {
        net user $Script:newUserName /logonpasswordchg:yes | Out-Null
        Write-Host "User '$Script:newUserName' must change the password at the next logon." -ForegroundColor Green
        }
        catch {
        Write-Host "Error while setting 'User must change password at next logon'." -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Yellow
        }

        $addAnotherUser = (Read-Host "`nAdd another user? Yes (Y), Back to the main menu (0), Exit (X)").ToUpper()
        switch ($addAnotherUser) {
            "Y" { createNewUser }
            "0" { Start-Menu }
            "X" { End }
            default { Write-Host "Invalid choice, try again."; End }
            }

}

function defaultUserPath {
    <#
    .SYNOPSIS
        Removes OneDrive and Skype
    #>
    [CmdletBinding()]
    param()

Write-Host "┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐" -ForegroundColor Yellow
Write-Host "│############                      Change the profile folder path for new users                            ############│" -ForegroundColor Yellow
Write-Host "│############          Choose the default template 'D:\Users' or enter your own path                       ############│" -ForegroundColor Yellow
Write-Host "└──────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘" -ForegroundColor Yellow

$choice = (Read-Host "Use the default template (1)`nEnter your own path (2)`nTo return to the main menu enter '0'`nTo exit the script enter 'X'`nYour choice").ToUpper()
        switch ($choice) {
            "1" { continue }
            "2" { continue }
            "0" { Start-Menu
                return}
            "X" { Script-end
                return}
            default { Write-Host "Invalid choice, try again."; End}
        }
    if ($choice -eq "2") {
        $newProfilePath = Read-Host "Enter your own path for the profile folders of new users"
    } else {
        $newProfilePath = "D:\Users"
    }

    # Create the folder if it does not exist yet
    if (-not (Test-Path $newProfilePath)) {
        New-Item -ItemType Directory -Path $newProfilePath | Out-Null
        Write-Host "Folder created: $newProfilePath" -ForegroundColor Green
    }

    # Point the ProfileList registry key to the new path
    $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"
    Set-ItemProperty -Path $regPath -Name "ProfilesDirectory" -Value $newProfilePath

    Write-Host "From now on the folders of new users will be created in $newProfilePath" -ForegroundColor Yellow

}
