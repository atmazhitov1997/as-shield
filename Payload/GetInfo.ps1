# 1. Basic information (fewer queries to the system)
$csInfo = Get-CimInstance Win32_ComputerSystem
$manufacturer = $csInfo.Manufacturer
$Model = $csInfo.Model
$hostname = $csInfo.DNSHostName

$computerSerial = (Get-CimInstance Win32_BIOS).SerialNumber
$today = Get-Date -Format "dd.MM.yyyy HH:mm:ss"

Write-Host "Report date:                 $today" -ForegroundColor Yellow
Write-Host "Computer name:               $hostname" -ForegroundColor Green

$reportFolder = Get-ReportDir
$textFile = Join-Path $reportFolder ($hostname + '.docx')

# --- CHECK FOR AN EXISTING FILE ---
$manualData = @()
if (Test-Path $textFile) {
    Write-Host "Report file $textFile found, the data will be updated." -ForegroundColor Yellow
	$existingLines = Read-DocxText -Path $textFile
    $startCollecting = $false
    foreach ($line in $existingLines) {
        # Start collecting only after the first system lines
        if ($line -match "-----------------\[ User Info \]") {
            $startCollecting = $true
        }
        
        # Stop at the ESET marker
        if ($line -match "-----------------\[ ESET ProductInstanceID \]") {
            break
        }
        
        # Add the line once the header has been passed
        if ($startCollecting) {
            $manualData += $line
        }
    }
}
else {
    Write-Host "Creating report file $textFile" -ForegroundColor Green
}

# --- TECHNICAL DATA ---
$os = Get-CimInstance Win32_OperatingSystem
$licenseKey = (Get-WmiObject -query 'select * from SoftwareLicensingService' -ErrorAction SilentlyContinue).OA3xOriginalProductKey
$winRelease = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion" -ErrorAction SilentlyContinue).DisplayVersion
if (!$winRelease) { $winRelease = "N/A" }

$motherboard = Get-CimInstance Win32_BaseBoard
$ram = Get-CimInstance Win32_PhysicalMemory
$disks = Get-CimInstance Win32_DiskDrive | Select-Object Model, Size, SerialNumber, InterfaceType, PNPDeviceID 
$videoAdapter = Get-CimInstance Win32_VideoController
$processor = Get-CimInstance Win32_Processor

$networkAdapters = Get-CimInstance Win32_NetworkAdapter | Where-Object { 
        ($_.NetConnectionID -match "Ethernet|Wi-Fi|Wireless") -and 
        ($_.Name -notmatch "Virtual|VMware") 
    }
	
$bluetoothAdapters = Get-CimInstance Win32_NetworkAdapter | Where-Object { $_.NetConnectionID -match "Bluetooth" }

# --- OUTPUT ---
$textOutput = @(
    # These lines are ALWAYS fresh (from the current session)
    "Report date:                 $today"
    "Computer name:               $hostname"
    ""

    if ($manualData.Count -eq 0) {
        # Empty file: create the sections
    "-----------------[ User Info ]----------------------------------------------------------------------------"
    ""; ""; ""
    "-----------------[ BIOS ]---------------------------------------------------------------------------------"
    ""; ""; ""
    "-----------------[ Bitlocker Keys ]-----------------------------------------------------------------------"
    ""; ""; ""
    } else {
        # Existing file: insert the saved "manual" data
        $manualData
    }

    "-----------------[ ESET ProductInstanceID ]---------------------------------------------------------------"
    ""
    $logPath = "C:\ProgramData\ESET\RemoteAdministrator\Agent\EraAgentApplicationData\Logs\trace.log"
    if (Test-Path $logPath) {
        $fileContent = Get-Content -Path $logPath -Raw
        $match = [regex]::Match($fileContent, "(?<=ProductInstanceID: )[\w-]+")
        if ($match.Success) {
            Write-Host "ProductInstanceID: $($match.Value)       [OK]" -ForegroundColor Green
            $match.Value
        } else {
            Write-Host "trace.log found, but it has no record." -ForegroundColor Yellow
            "trace.log found, but it has no 'ProductInstanceID' record."
        }
    } else {
        Write-Host "Error: trace.log not found.                  [Warning]" -ForegroundColor Red
        "Error: trace.log not found."
    }
    ""

    "-----------------[ External IP address ]------------------------------------------------------------------"
    ""
    $ip = $null
    # v2: external services are queried only with the operator's consent (unwanted on isolated systems).
    $askIp = (Read-Host "Detect the external IP address via api.ipify.org / ifconfig.me? (Y/N)").Trim().ToUpper() -eq 'Y'
    if ($askIp) { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 }
    foreach ($uri in @($(if ($askIp) { "https://api.ipify.org", "https://ifconfig.me/ip" }))) {
        try { $ip = (Invoke-RestMethod -Uri $uri -TimeoutSec 5 -UserAgent "Mozilla/5.0").Trim(); if ($ip) { break } } catch { continue }
    }
    if ($ip) {
        Write-Host "External IP address: $ip" -ForegroundColor Green
        $ip
    } elseif (-not $askIp) {
        "Not detected (the operator declined the Internet request)."
    } else {
        Write-Host "External IP address: service unavailable.     [Warning]" -ForegroundColor Red
        "Service unavailable."
    }
    ""

    "-----------------[ Local Users ]--------------------------------------------------------------------------"
    ""
    "{0,-25} {1,-11} {2,-13} {3,-10} {4,-17} {5,-40}" -f "        Name", "    Type", "    Password", "  Status", "  Last logon", "        Profile"
    "------------------------  ----------- ------------- ---------- ----------------- -------------------------"
    
    Add-Type -AssemblyName System.DirectoryServices.AccountManagement -ErrorAction SilentlyContinue
    $ctx = New-Object System.DirectoryServices.AccountManagement.PrincipalContext([System.DirectoryServices.AccountManagement.ContextType]::Machine)
    $admins = (Get-LocalGroupMember -SID "S-1-5-32-544" -ErrorAction SilentlyContinue).SID

    foreach ($user in (Get-LocalUser | Sort-Object Name)) {
        $uName = $user.Name
        $uStatus = if ($user.Enabled) { "ENABLED" } else { "  disabled" }
        $uType = if ($admins -contains $user.SID) { "   ADMIN" } else { "user" }
        $uLast = if ($user.LastLogon) { $user.LastLogon.ToString("dd-MM-yyyy HH:mm") } else { "   never" }
        # v2: no logon attempts with an empty password (1.5.9 generated 4625 events and could lock accounts out).
        $uPass = if (-not $user.PasswordRequired) { " NOT REQUIRED" } elseif ($user.PasswordLastSet) { "SET" } else { "    none" }
        
        $uProfile = "   none"
        $regPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$($user.SID)"
        if (Test-Path $regPath) { $uProfile = (Get-ItemProperty $regPath -ErrorAction SilentlyContinue).ProfileImagePath }

        "{0,-25} {1,-11} {2,-13} {3,-10} {4,-17} {5,-40}" -f $uName, $uType, $uPass, $uStatus, $uLast, $uProfile
    }

    ""
    "-----------------[ GENERAL ]------------------------------------------------------------------------------"
    ""
    "Computer name:                     $hostname"
    ""
    "Manufacturer:                      $manufacturer"
    "Model:                             $Model"
    "Serial number:                     $computerSerial"
    ""
    "Motherboard model:                 $($motherboard.Product)"
    "Motherboard serial number:         $($motherboard.SerialNumber)"
    ""
    "Operating system:                  $($os.Caption)"
    "Windows version:                   $winRelease"
    "OS build:                          $($os.BuildNumber)"
    "Install date:                      $($os.InstallDate.ToString("dd.MM.yyyy HH:mm:ss"))"
    "License key:                       $licenseKey"
    ""
    "-----------------[ PROCESSOR ]----------------------------------------------------------------------------"
    ""
    foreach ($cpu in $processor) {
        "Processor:         $($cpu.Name.Trim())"
        "Cores:             $($cpu.NumberOfCores)"
		""

    }
    "-----------------[ VIDEO ADAPTER ]------------------------------------------------------------------------"
    ""
    foreach ($adapter in $videoAdapter) {
        "Name:              $($adapter.Name)"
        "Video memory:      $([math]::Round($adapter.AdapterRAM / 1MB, 2)) MB"
		""
    }
    "-----------------[ MEMORY ]-------------------------------------------------------------------------------"
	""
    foreach ($r in $ram) {
        "Manufacturer:      $($r.Manufacturer)"
        "Capacity:          $([math]::Round($r.Capacity / 1GB, 2)) GB"
        "Serial number:     $($r.SerialNumber)"
        ""
    }
	"-----------------[ HDD / SSD / USB ]----------------------------------------------------------------------"
	""
	foreach ($disk in $disks) {
    $serial = $disk.SerialNumber
    # For USB devices, take the clean serial number from PNPDeviceID
    if ($disk.InterfaceType -eq "USB" -and $disk.PNPDeviceID) {
        $parts = $disk.PNPDeviceID.Split("\")
        if ($parts.Count -gt 0) {
            # Take the last part of the path and cut the indexes after '&'
            $serial = $parts[-1].Split("&")[0]
        }
    }
		"Model:             $($disk.Model)"
		"Capacity:          $([math]::Round($disk.Size / 1GB, 2)) GB"
		"Serial number:     $serial"
		""
	} 

    "-----------------[ NETWORK ADAPTERS ]---------------------------------------------------------------------"
    ""
 	foreach ($adapter in $networkAdapters) {
        $mac = if ($adapter.MACAddress) { $adapter.MACAddress } else { "No MAC address" }
        $idx = if ($adapter.InterfaceIndex) { $adapter.InterfaceIndex } else { $adapter.Index }
        $ipAddr = "No IP address"
        if ($idx) {
            $ipInfo = Get-NetIPAddress -InterfaceIndex $idx -AddressFamily IPv4 -ErrorAction SilentlyContinue
            if ($ipInfo) { $ipAddr = $ipInfo.IPAddress -join ", " }
        }
        "Adapter:           $($adapter.Name)"
        "MAC address:       $mac"
        "IP address:        $ipAddr"
        ""
    }
    "-----------------[ BLUETOOTH ADAPTERS ]-------------------------------------------------------------------"
    ""
    if ($null -eq $bluetoothAdapters) {
        "No active Bluetooth adapters found."
		""
    } else {
        foreach ($bt in $bluetoothAdapters) {
            $macAddress = if ($bt.MACAddress) { $bt.MACAddress } else { "No MAC address" }
            "Bluetooth adapter:    $($bt.Name)"
            "Bluetooth MAC:        $macAddress"
            ""
        }
    }
    "-----------------[ INSTALLED SOFTWARE ]-------------------------------------------------------------------"
    ""
    $registryPaths = @(
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
        "HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )
    $installedApps = Get-ItemProperty $registryPaths -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName } | Sort-Object DisplayName
    foreach ($app in $installedApps) {
        $aDate = "Unknown"
        if ($app.InstallDate -match '^\d{8}$') {
            $aDate = "$($app.InstallDate.Substring(6,2))-$($app.InstallDate.Substring(4,2))-$($app.InstallDate.Substring(0,4))"
        }
        "$($app.DisplayName.Trim()) ($(if($app.Publisher){$app.Publisher.Trim()}else{"Unknown"}), $(if($app.DisplayVersion){$app.DisplayVersion}else{"Unknown"}),  $aDate)"
    }
)

# WRITE THE FILE (docx)
$content = @(@{t = 'title'; x = 'PC RECORD CARD' }, @{t = 'meta'; x = ('PC: ' + $hostname + '     Serial number: ' + $computerSerial) }, @{t = 'space' })
foreach ($ln in $textOutput) { $content += @{t = 'mono'; x = [string]$ln } }
try {
    New-DocxReport -Path $textFile -Content $content
    Write-Host ""
    Write-Host ("Report saved: " + $textFile) -ForegroundColor Green
    Write-Host ""
    Write-Host "The [ User Info ] / [ BIOS ] / [ Bitlocker Keys ] sections are filled in manually in the file -" -ForegroundColor Yellow
    Write-Host "they are kept on the next run; only the technical data is refreshed." -ForegroundColor Yellow
    try { Start-Process $textFile } catch { }
} catch { Write-Host ("Could not save the report: " + $_.Exception.Message) -ForegroundColor Red }