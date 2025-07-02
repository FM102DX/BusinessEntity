
<# 

This script is for environment management and deployment purposes
by Pinega F.V. on july 2023

#>

param(

[string] $transactionId='',
[string] $customWokingFolder='',
[string] $callType='plain' # plain, external-from-self,  external-from-other-script
)

<# 
$userName0 = "root";
$pwd0 = ConvertTo-SecureString "4*cXVNAmP|gy" -AsPlainText -Force
$deployCreds0 = New-Object System.Management.Automation.PSCredential ($userName0, $pwd0)

$userName1 = "root01";
$pwd1 = ConvertTo-SecureString "64pVSprEtQ" -AsPlainText -Force
$deployCreds1 = New-Object System.Management.Automation.PSCredential ($userName1, $pwd1)
 #>


#COMMON
[string] $scriptDir = $PSScriptRoot;
[string] $scriptFile=$MyInvocation.MyCommand;
[string] $scriptFileFullPath = [IO.Path]::Combine($scriptDir,$scriptFile);
[string] $pwshPath = "C:\Program Files\PowerShell\7\pwsh.exe";
[string] $operaExeFullPath = "C:\Users\Admin\AppData\Local\Programs\Opera\opera.exe"
[string] $dllFullPath ="C:\Develop\SampleOnlineMall\SampleOnlineMall.AssortmentApi\bin\Release\net6.0\SampleOnlineMall.Core.dll"

#COMMON -- MENUMAKER
[string] $menuFileName='MenuDetail.txt';
[string] $menuFileFullPath=[IO.Path]::Combine($scriptDir,$menuFileName);

[string] $currentLogFileName = '';
[string] $logFileFullPath='';
[string] $logsDirectory=[IO.Path]::Combine($scriptDir,'Logs');
[int]    $howManyLogFilesToLeaveInLogsDirectory = 3
[bool] $canExit=$false;

# CREDS AND LOGIN DATA
    # file to store creds in
[string] $credsFilePath = "C:\Develop\Deploy\Creds\Creds.txt";
[string] $deployAddress01='';
[string] $deployHost01='';
[string] $marketplaceSuppliersGuidStr = "4b476692-0ec5-49aa-84ca-61a1f25d77c2";
Try { $marketplaceSuppliersGuid = [System.Guid]::Parse($marketplaceSuppliersGuidStr) } Catch { $Result =  $Null } Finally { $Result }

$menuContent = Get-content $menuFileFullPath



function PerformCls()
{
   #  Clear-host
}

function ShowMenu() {
    ""
    ""
    "MAIN MENU"
    $menuContent
    "99-Exit"
}

# setup TrustedHosts
if($transactionId -eq '')
{
   
    # if ($tth -ne $tth_fact)    {        Set-Item WSMan:\localhost\Client\TrustedHosts -Value $tth -Force;    }
    
    $tth = '31.31.42.42';
    $x0 = Set-Item WSMan:\localhost\Client\TrustedHosts -Value $tth -Force;
    $tth_fact = (Get-Item WSMan:\localhost\Client\TrustedHosts).Value;
    
    #Set-Item -Path  WSMan:\localhost\Service\AllowEncrypted -Value $true;
}

function Pressenter ()
{
    Read-Host -Prompt 'Press enter...'
}

function SetupCredentials()
{
    [string] $fileData = "";
    
    [bool] $credFileExists= [System.IO.File]::Exists($credsFilePath);
    
    if (-not $credFileExists) {return;}

    $fileData =  [System.IO.File]::ReadAllText($credsFilePath);
    log "fileData=$($fileData)"
    $arr=$fileData.Split("`n");

    log "arr.Length=$($arr.Length)"

    if($arr.Length -gt 0)
    {
        $script:deployAddress01 = GetValueFromArray -arr $arr -key 'DeployAddress01' | Select-Object  -Last 1
        $script:deployHost01 = GetValueFromArray -arr $arr -key 'DeployHost01' | Select-Object  -Last 1
    }

    Log "deployAddress01=$deployAddress01"
}

function GetValueFromArray($arr, [string]$key)
{
    [string] $found = $arr | Where-Object { $_.Contains($key) }  | Select-Object  -Last 1
    if($null -eq $found) {"";return;}
    ($found.Split(" ")[1]).Trim();
}

function Log ([string] $text)
{
    if ($script:currentLogFileName -eq '')
    {
        [string]$dtVar = Get-Date -Format "dd-MM-yyyy_HH-mm-ss"
        if($transactionId -eq '') {$tr=''} else {$tr="_$transactionId"}
        $script:currentLogFileName = "$($dtVar)$($tr).txt"
    }
    $Script:logFileFullPath = [IO.Path]::Combine($logsDirectory, $script:currentLogFileName)
    if ([System.IO.File]::Exists($Script:logFileFullPath)){}else{New-Item $Script:logFileFullPath | Out-Null}
    "[$(Get-Date -Format "dd.MM.yyyy-HH:mm:ss")] $text" | Add-Content $Script:logFileFullPath;
}

#COMMON FUNCTION
function CheckLogsDirectory()
{
    CreateDirectoryOnLocalHostIfNotExists -directoryName $logsDirectory
}

function CLearLogs()
{
    KeepOnlyNLastFilesInDirectory -directory $logsDirectory -itemsCol $howManyLogFilesToLeaveInLogsDirectory
}

function CreateDirectoryOnLocalHostIfNotExists([string] $directoryName)
{
    if (-not (Test-Path $directoryName -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $directoryName
    } 
}
function PerformTimeSpanFormat([timespan] $ts)
{
    if ($ts.TotalMilliseconds -gt 86400000)
    {
        "{0:dd}:{0:hh}:{0:mm}:{0:ss}" -f $ts
    }
    else 
    {
        "{0:hh}:{0:mm}:{0:ss}" -f $ts
    }
}

function KeepOnlyNLastFilesInDirectory([string] $directory, [int] $itemsCol)
{
    if (-not (Test-path -Path $directory)) {return};
    Get-ChildItem $directory | Sort-Object CreationTime | Select-Object -SkipLast $itemsCol | Remove-Item -Force -Recurse
}

function PerformSecondsCountDown ([int] $seconds, [string]$prefix="Performing countdown", [bool] $performLog=$false)
{
    [datetime] $currentTime= Get-Date;
    [datetime] $targetTime= $currentTime.AddSeconds($seconds);
    [bool] $flag=$true;
    DO
    {
        $currentTime= Get-Date;
        $delta = $targetTime - $currentTime;

            if($flag) 
            {
                [string] $ts= PerformTimeSpanFormat -ts $delta
                write-host "`r$($prefix): $ts"
                if($performLog)
                {
                    Log -text "Performing countdown log, its $ts left"
                }
                $flag = $false;
            }
            else
            {
                $flag=$true;            
            }

        Start-Sleep -Milliseconds 500
    }
    WHILE ($delta.TotalMilliseconds -ge 0)
}

#CORE CONTENT




function DeployMallBlazorFrontend ([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $remoteFolder = "/var/www/www-root/data/www/mall.t109.tech";
    [string] $projectPath =      "C:\Develop\SampleOnlineMall\SampleOnlineMall.FrontEnd.Blazor";
    [string] $projectBuildPath = "C:\Develop\SampleOnlineMall\SampleOnlineMall.FrontEnd.Blazor\bin\Release\net6.0";
    [string] $siteUrl = "https://mall.t109.tech";

    DeployBalzorWasm60SiteToUbuntuHost -remoteFolder $remoteFolder -projectPath $projectPath -projectBuildPath $projectBuildPath -siteUrl $siteUrl

}

function DeployWebLoggerBlazor ([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $remoteFolder = "/var/www/www-root/data/www/weblogger2.t109.tech";
    [string] $projectPath =      "C:\Develop\SampleOnlineMall\WebLogger.Blazor";
    [string] $projectBuildPath = "C:\Develop\SampleOnlineMall\WebLogger.Blazor\bin\Release\net6.0";
    [string] $siteUrl = "https://weblogger2.t109.tech";

    DeployBalzorWasm60SiteToUbuntuHost -remoteFolder $remoteFolder -projectPath $projectPath -projectBuildPath $projectBuildPath -siteUrl $siteUrl

}


function DeployStore01 ([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $remoteFolder = "/var/www/www-root/data/www/store01.t109.tech";
    [string] $projectPath =      "C:\Develop\T109ActivityFrontendFirstSampleVersion\Shop\T109.ActiveDive.FrontEnd.Blazor";
    [string] $projectBuildPath = "C:\Develop\Deploy\BalzorDeployFolder";
    [string] $siteUrl = "https://store01.t109.tech";

    DeployBalzorWasm60SiteToUbuntuHost -remoteFolder $remoteFolder -projectPath $projectPath -projectBuildPath $projectBuildPath -siteUrl $siteUrl

}



function DeployApi01 ([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $remoteFolder = "/var/www/www-root/data/www/storeapi01.t109.tech";
    [string] $projectPath = "C:\Develop\T109ActivityFrontendFirstSampleVersion\Shop\T109.ActiveDive.EventCatalogue.EventCatalogueApi";
    [string] $projectBuildPath = "C:\Develop\T109ActivityFrontendFirstSampleVersion\Shop\T109.ActiveDive.EventCatalogue.EventCatalogueApi\bin\Release\net6.0\*";
    [string] $wwwRootFolder =    "C:\Develop\T109ActivityFrontendFirstSampleVersion\Shop\T109.ActiveDive.EventCatalogue.EventCatalogueApi\wwwroot";
    [string] $executingFileFullPath = "/var/www/www-root/data/www/storeapi01.t109.tech/T109.ActiveDive.EventCatalogue.EventCatalogueApi.dll";
    [string] $serviceName = "storeapi01.service";
    [string] $siteUrl = "https://storeapi01.t109.tech";

    DeployAspNetCore60ApiSiteToUbuntuHost -remoteFolder $remoteFolder -projectPath $projectPath -projectBuildPath $projectBuildPath -wwwRootFolder $wwwRootFolder -serviceName $serviceName -executingFileFullPath $executingFileFullPath -siteUrl $siteUrl

}

Function DeployWebLogger ([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $remoteFolder = "/var/www/www-root/data/www/weblogger.t109.tech";
    [string] $projectPath = "C:\Develop\SampleOnlineMall\SampleOnlineMall.WebLogger";
    [string] $projectBuildPath = "C:\Develop\SampleOnlineMall\SampleOnlineMall.WebLogger\bin\Release\net6.0\*";
    [string] $wwwRootFolder =    "C:\Develop\SampleOnlineMall\SampleOnlineMall.WebLogger\wwwroot\*";
    [string] $executingFileFullPath = "/var/www/www-root/data/www/weblogger.t109.tech/SampleOnlineMall.WebLogger.dll";
    [string] $serviceName = "weblogger.service";
    [string] $siteUrl = "https://weblogger.t109.tech";

    DeployAspNetCore60ApiSiteToUbuntuHost -remoteFolder $remoteFolder -projectPath $projectPath -projectBuildPath $projectBuildPath -wwwRootFolder $wwwRootFolder -serviceName $serviceName -executingFileFullPath $executingFileFullPath -siteUrl $siteUrl

}

function LogAndExit ([string] $text)
{
    log $text
    throw $text
    exit
}
function LogAndOutput ([string] $text)
{
    log $text
    $text
}

function DeleteAllFilesFromFolder ([string] $folderName)
{
    Get-ChildItem -Path  $folderName -recurse | Select-Object -ExpandProperty FullName |   Remove-Item -recurse -force 
}


function DeployAspNetCore60ApiSiteToUbuntuHost ([string] $remoteFolder, [string] $projectPath, [string] $projectBuildPath,[string] $wwwRootFolder, [string] $serviceName, [string] $executingFileFullPath, [string] $siteUrl)
{

    LogAndOutput -text  "Deploying site $siteUrl";
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1
    
    LogAndOutput -text "Session opened successfully";

    # stop service and check if stopped
    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo systemctl stop  $($args[0])"; } -ArgumentList $serviceName
    PerformSecondsCountDown -seconds 3;
    $isActive = Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "sudo systemctl is-active  $($args[0])"; } -ArgumentList $serviceName
    $isActive
    if ($isActive -ne 'inactive') {LogAndExit -text "Unable to stop service $serviceName on remote host, service status=$isActive" }
    
    LogAndOutput -text  "Service $serviceName moved to inactive state";

    # delete folder
    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "sudo rm -r -f  $($args[0])";} -ArgumentList $remoteFolder
    

    # check if folder deleted
    [string] $folderExists = Invoke-Command -Session $deploySession -ScriptBlock    { test-path $args[0]}  -ArgumentList $remoteFolder
    if ($folderExists -ne 'false')   {   LogAndExit -text "Unable to delete folder"  }
    
    LogAndOutput -text  "Remote folder $remoteFolder";

    # --- deploy 

    #  -- build 
    "dotnet build $projectPath --configuration Release" | cmd
    log "Build completed";

    #  -- create dir and copy it all to host
    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "mkdir $($args[0])";} -ArgumentList $remoteFolder
    LogAndOutput -text  "Remote dir $remoteFolder created";

    #  -- copy items to remote
    LogAndOutput -text  "Copying data";
    Copy-Item $projectBuildPath -ToSession $deploySession -Destination $remoteFolder -Recurse -Force ;
    
    LogAndOutput -text  "Copying wwwroot";
    [bool] $wwwrootSpecified=(-not [string]::IsNullOrEmpty($wwwRootFolder));
    LogAndOutput -text  "wwwroot variable specified: $($wwwrootSpecified)";
    if ($wwwrootSpecified)
    {
        Copy-Item $wwwRootFolder -ToSession $deploySession -Destination $remoteFolder -Recurse -Force ;
    }
    LogAndOutput -text  "Finished data copy";

    # give rights to folder
    # $logsDir=[io.path]::Combine($remoteFolder,"Logs");
    $logsDir="$($remoteFolder)/Logs";
    

    #logs folder

    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "Remove-Item -Path $($args[0]) -Force -Recurse";} -ArgumentList $logsDir

    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "mkdir $($args[0])";} -ArgumentList $logsDir

    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "chmod 777 $($args[0])";} -ArgumentList $remoteFolder

    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "chmod 777 $($args[0])";} -ArgumentList $logsDir

    LogAndOutput -text  "Given rights to $remoteFolder on remote host";

    # give execution rights to dll
    Invoke-Command -Session $deploySession -ScriptBlock  {  Invoke-Expression "chmod +X $($args[0])";} -ArgumentList $executingFileFullPath
    LogAndOutput -text  "Given execution rights to dll $executingFileFullPath";

    # start service and check if started
    LogAndOutput -text  "Starting service $serviceName";
    Invoke-Command -Session $deploySession -ScriptBlock  {  Invoke-Expression "sudo systemctl start $($args[0])"; } -ArgumentList $serviceName

    $isActive = Invoke-Command -Session $deploySession -ScriptBlock {  Invoke-Expression "sudo systemctl is-active $($args[0])"; } -ArgumentList $serviceName
    $isActive
    if ($isActive -ne 'active') { LogAndExit -text "Unable to start service $serviceName on remote host" }

    LogAndOutput -text  "Service started";
    LogAndOutput -text  "Deploy completed successfully";

    Start-process -FilePath $operaExeFullPath -ArgumentList $siteUrl;
    pause
}


function DeployBalzorWasm60SiteToUbuntuHost ([string] $remoteFolder, [string] $projectPath, [string] $projectBuildPath, [string] $projectBuildZipPath, [string] $siteUrl)
{
    LogAndOutput -text  "Deploying Blazor on Net Core 6.0 site $siteUrl ";
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1
    LogAndOutput -text "Session opened successfully";

    # delete folder
    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "sudo rm -r -f  $($args[0])";} -ArgumentList $remoteFolder

    # check if folder deleted
    [string] $folderExists = Invoke-Command -Session $deploySession -ScriptBlock    { test-path $args[0]}  -ArgumentList $remoteFolder
    if ($folderExists -ne 'false')   {   LogAndExit -text "Unable to delete folder"  }

    LogAndOutput -text  "Remote folder deleted $remoteFolder";

    #  ---- deploy 
    #  -- build 
    DeleteAllFilesFromFolder -folderName $projectBuildPath
    "dotnet publish $projectPath -c Release -o $projectBuildPath --force " | cmd
    
    log "Publish completed, now gonna copy";

    #  -- create dir and copy it all to host
    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "mkdir $($args[0])";} -ArgumentList $remoteFolder
    LogAndOutput -text  "Remote dir $remoteFolder created";

    #copy files
    Copy-Item "$($projectBuildPath)\*" -ToSession $deploySession -Destination $remoteFolder -Recurse -Force ;

    # give rights to folder
    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "chmod 777 $($args[0])";} -ArgumentList $remoteFolder
    LogAndOutput -text  "Given rights to $remoteFolder on remote host";

    LogAndOutput -text  "Finished data copy";

    LogAndOutput -text  "Deploy completed successfully";
    Start-process -FilePath $operaExeFullPath -ArgumentList $siteUrl;
    
    pause

}


function DeployHtmlAboutmeSite([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        log "starting new process with args: $argList"
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }

    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1

    # delete folder
    Invoke-Command -Session $deploySession -ScriptBlock    {  Invoke-Expression "sudo rm -r -f  /var/www/www-root/data/www/aboutme.t109.tech";}
    

    # check if folder deleted
    [string] $folderExists = Invoke-Command -Session $deploySession -ScriptBlock  { test-path "/var/www/www-root/data/www/aboutme.t109.tech"}
    if ($folderExists -ne 'false')   {   throw;  }
    
    Copy-Item "C:\Develop\SiteData\aboutme.t109.tech" -ToSession $deploySession -Destination "/var/www/www-root/data/www/" -Recurse -Force ;

    Start-process -FilePath $operaExeFullPath -ArgumentList "https://aboutme.t109.tech";

    # copy files
    # robocopy "C:\Develop\SiteData\aboutme.t109.tech" "//$($script:deployHost01)/var/www/www-root/data/www/" /mir /xd;

    # CopyFilesToRemoteHostViaRoboCopy -folderSrc "C:\Develop\SiteData\aboutme.t109.tech" -hostTarget "$($script:deployHost01)" -folderTarget "/var/www/www-root/data/www/";
    # PerformSecondsCountDown -seconds 10
}

function CopyFilesToRemoteHostViaRoboCopy([string] $folderSrc, [string] $hostTarget, [string] $folderTarget )
{
    # this doesnt work
    [string] $srcPath = $folderSrc.Replace(":", "$");
    [string] $targetPath = "\\$($hostTarget)\$($folderTarget)".Replace(":", "$");
    
    $netTarget = new-object -ComObject WScript.Network;
    $netTarget.RemoveNetworkDrive('j:', $true);
    $netTarget.MapNetworkDrive('j:', "$targetPath", $false);

    log "Performing data robocopy from srcPath=$srcPath targetPath=$targetPath"

    # robocopy C:\Develop\SiteData\aboutme.t109.tech //31.31.201.152/var/www/www-root/data/www/ *.* /E
}

function DeploySampleMallAssortWebApi([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $remoteFolder = "/var/www/www-root/data/www/mallassortapi01.t109.tech";
    [string] $projectPath = "C:\Develop\SampleOnlineMall\SampleOnlineMall.AssortmentApi";
    [string] $projectBuildPath = "C:\Develop\SampleOnlineMall\SampleOnlineMall.AssortmentApi\bin\Release\net6.0\*";
    [string] $wwwRootFolder = "";
    [string] $executingFileFullPath = "/var/www/www-root/data/www/mallassortapi01.t109.tech/SampleOnlineMall.AssortmentApi.dll";
    [string] $serviceName = "mallassortapi01.service";
    [string] $siteUrl = "https://mallassortapi01.t109.tech";

    DeployAspNetCore60ApiSiteToUbuntuHost -remoteFolder $remoteFolder -projectPath $projectPath -projectBuildPath $projectBuildPath -wwwRootFolder $wwwRootFolder -serviceName $serviceName -executingFileFullPath $executingFileFullPath -siteUrl $siteUrl
    pause
}

$source = @"
public static byte[] SerializeBson<T>(T obj)
{
    using (MemoryStream ms = new MemoryStream())
    {
        using (BsonWriter writer = new BsonWriter(ms))
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(writer, obj);
        }

        return ms.ToArray();
    }
}
"@

function FeedAssortmentToMallAssortWebApi([string] $_transactionId)
{
    if($callType -eq 'plain')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    
    [string] $controllerUrl02 = "https://mallassortapi01.t109.tech/insertitem";
    [string] $imagePath = "C:\Develop\SampleOnlineMallAssortment";

    # Add-Type $source

    Add-Type -Path $dllFullPath

    $folders=Get-ChildItem $imagePath

    foreach($folder in $folders)
    {
        $itemJsonFileName = [System.io.path]::Combine($folder,"item.json");  
       
        if (-not [System.IO.File]::Exists($itemJsonFileName)) {continue;}

        $itemJsonContent= [System.IO.File]::ReadAllText($itemJsonFileName);
        
        $commItem = [SampleOnlineMall.Core.CommodityItemApiFeed] (ConvertFrom-Json($itemJsonContent));

        Log "-----------"
        Log $commItem.Id
        Log $commItem.Name

        # Pic 1
        $imagePath = [System.io.path]::Combine($folder,"1.jpg");
        if ([System.IO.File]::Exists($imagePath)) 
        {
            $bytesArr = [io.file]::ReadAllBytes($imagePath);
            $commItem.FirstPic = [Convert]::ToBase64String($bytesArr);
        }

        # Pic 2
        $imagePath = [System.io.path]::Combine($folder,"2.jpg");
        if ([System.IO.File]::Exists($imagePath)) 
        {
            $bytesArr = [io.file]::ReadAllBytes($imagePath);
            $commItem.SecondPic = [Convert]::ToBase64String($bytesArr);
        }

        # Pic 3
        $imagePath = [System.io.path]::Combine($folder,"3.jpg");
        if ([System.IO.File]::Exists($imagePath)) 
        {
            $bytesArr = [io.file]::ReadAllBytes($imagePath);
            $commItem.ThirdPic = [Convert]::ToBase64String($bytesArr);
        }
        
        $commItem.SupplierId = $marketplaceSuppliersGuid

        $jsonObj = ConvertTo-Json -InputObject $commItem -Depth 100 
        Log "Sending json obj"
        Log $jsonObj
        "Sending object $($commItem.Name)"
        Invoke-RestMethod -Method 'Post' -Uri $controllerUrl02 -ContentType "application/json; charset=utf-8" -Body $jsonObj   

    }
    pause
}

function GetSuppliersStatus ([string] $_transactionId)
{
    # Add-Type $source
    $controllerUrl="https://mallassortapi01.t109.tech/Suppliers";
    Invoke-RestMethod -Method 'Get' -Uri $controllerUrl -ContentType "application/json; charset=utf-8"
    pause
}
function FeedSuppliersToWebApi([string] $_transactionId)
{
    if($callType -eq 'plain0')
    {
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
    }
    [string] $controllerUrl02 = "https://mallassortapi01.t109.tech/suppliers/insertitem";
    Add-Type -Path $dllFullPath
    $folders=Get-ChildItem $imagePath
    $supplier = New-Object SampleOnlineMall.Core.Models.Supplier
    $supplier.Id = $marketplaceSuppliersGuid;
    $supplier.Name = 'Marketplace';
    $jsonObj = ConvertTo-Json -InputObject $supplier -Depth 100 
    Invoke-RestMethod -Method 'Post' -Uri $controllerUrl02 -ContentType "application/json; charset=utf-8" -Body $jsonObj
    pause
}
function DeleteAllSuppliers ([string] $_transactionId)
{
    # Add-Type $source
    $controllerUrl="https://mallassortapi01.t109.tech/Suppliers/deleteallitems";
    Invoke-RestMethod -Method 'Delete' -Uri $controllerUrl -ContentType "application/json; charset=utf-8"
}

function GetMessagesStatus ([string] $_transactionId)
{
    # Add-Type $source
    $controllerUrl="https://weblogger.t109.tech/";
    Invoke-RestMethod -Method 'Get' -Uri $controllerUrl -ContentType "application/json; charset=utf-8"
    pause
}
function SendWebApiTestMessage([string] $_transactionId)
{
    # Add-Type $source
    $controllerUrl="https://weblogger.t109.tech/insertitem/";
    Add-Type -Path $dllFullPath
    [SampleOnlineMall.Core.WebLoggerMessage] $msg = new-object SampleOnlineMall.Core.WebLoggerMessage
    $msg.Sender = "console"
    $msg.Message = "Test message"
    $jsonObj = ConvertTo-Json -InputObject $msg -Depth 100 
    Log "Sending json obj"
    Log $msg
    Invoke-RestMethod -Method 'Post' -Uri $controllerUrl -ContentType "application/json; charset=utf-8" -Body $jsonObj 
    pause  
}
function DeleteAllAssortmentItems ([string] $_transactionId)
{
    # Add-Type $source
    $controllerUrl="https://mallassortapi01.t109.tech/deleteallitems/";
    Invoke-RestMethod -Method 'Delete' -Uri $controllerUrl -ContentType "application/json; charset=utf-8"
    pause
}
function DeleteAllWebLoggerMessages ([string] $_transactionId)
{
    # Add-Type $source
    $controllerUrl="https://weblogger.t109.tech/deleteallitems/";
    Invoke-RestMethod -Method 'Delete' -Uri $controllerUrl -ContentType "application/json; charset=utf-8"
    pause
}

function GetAssortmentStatus ([string] $_transactionId)
{
    [string] $controllerUrl = "https://mallassortapi01.t109.tech/";
    Invoke-RestMethod -Method 'Get' -Uri $controllerUrl -ContentType "application/json"
    pause
}


function ViewPostgresDbsOnRemoteHost ([string] $_transactionId)
{
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1
    
    LogAndOutput -text "Session opened successfully";

    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -l"; }

}

function CreateAssortDbOnRemoteHost ([string] $_transactionId)
{
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1
    
    LogAndOutput -text "Session opened successfully";

    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c 'CREATE DATABASE Assortment'"; }

    pause
}

function DeleteAssortDbOnRemoteHost ([string] $_transactionId)
{
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1
    
    LogAndOutput -text "Session opened successfully";

    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c 'DROP DATABASE Assortment'"; }

    pause
}

function RemoveBinObjDirectoriesFromDirectoryRecursive([string] $dirName)
{
    get-childitem -Path $dirName -Include "bin" -Recurse -force | Remove-Item -Force -Recurse
    get-childitem -Path $dirName -Include "obj" -Recurse -force | Remove-Item -Force -Recurse
}

function RemoveBinObjFromMallBlazorFrontend()
{
    RemoveBinObjDirectoriesFromDirectoryRecursive -dirName "C:\Develop\SampleOnlineMall"
}


function CreateWebLoggerDbOnRemoteHost ([string] $_transactionId)
{
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1
    
    LogAndOutput -text "Session opened successfully";

    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c 'CREATE DATABASE weblogger'"; }

}

function DeleteWebLoggerDbOnRemoteHost ([string] $_transactionId)
{
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1

    LogAndOutput -text "Session opened successfully";

    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c 'DROP DATABASE weblogger'"; }
}

function CreateOpenSshSession()
{
    try 
    {
        $deploySession = New-PSSession -HostName $script:deployAddress01 -KeyFilePath "C:\Users\Admin\.ssh\id_rsa";       
    }
    catch 
    {
        log "Unable to crate session: $($_)"
        exit
    }
    $deploySession
}
function _FillClientsDb()
{
    # doesnt work
    LogAndOutput -text  "Creating session";
    
    # session
    $deploySession = CreateOpenSshSession | Select-Object -Last 1

    [string] $createTableQuery = "CREATE TABLE clients 
                                    (
                                        id UUID PRIMARY KEY UNIQUE NOT NULL, 
                                        username VARCHAR ( 50 ) UNIQUE NOT NULL,
                                        name VARCHAR ( 100 ) NULL,
                                        surname VARCHAR ( 100 ) NULL,
                                        age INT, 
                                        email VARCHAR ( 255 ) UNIQUE NOT NULL,
                                        created_on TIMESTAMP NOT NULL
                                    )";
    [string] $insertClient1Query = "INSERT INTO clients (id, username, name, surname, email) values ($(New-Guid), 'Clinent1', 'Name1', 'Surname1', 30, 'email.dot.com')"
    LogAndOutput -text "Session opened successfully";
    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c 'DROP DATABASE Crm'"; }
    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c 'CREATE DATABASE Crm'"; }
    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c '$createTableQuery'"; }
    Invoke-Command -Session $deploySession -ScriptBlock { Invoke-Expression "sudo -u postgres psql -c '$insertClient1Query'"; }
}
#18 - deploy


function RunTransaction ([string] $_transactionId)
{
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -PassThru;
        return;
}
function RunTransactionAndWait ([string] $_transactionId)
{
        # this is made to start transaction in a separate window
        [string] $argList = "-file $scriptFileFullPath -transactionId $_transactionId -callType 'external-from-self' ";
        Start-process -FilePath $pwshPath -ArgumentList $argList -Wait;
        "EOFunc"
        return;
}

function DropThanCreateAssortDbAndFeelAssortment ([string] $_transactionId)
{
    RunTransactionAndWait -_transactionId "753101";
    RunTransactionAndWait -_transactionId "750101";
    RunTransactionAndWait -_transactionId "200101";
    return;
}
function DeploySampleMallAssortWebApiWithAssortmentFill ([string] $_transactionId)
{
    RunTransactionAndWait -_transactionId "201801";
    "EOPoing"
    RunTransactionAndWait -_transactionId "203101";
    RunTransactionAndWait -_transactionId "200101";
    return;
}

function DeployAssortmentThanDropCreateDbThanFeedAssortment ([string] $_transactionId)
{
    RunTransactionAndWait -_transactionId "201801";
    RunTransactionAndWait -_transactionId "753101";
    RunTransactionAndWait -_transactionId "750101";
    RunTransactionAndWait -_transactionId "200101";

    return;
}
function DeployWebLoggerThanDelteAndCreateDb ([string] $_transactionId)
{
    RunTransactionAndWait -_transactionId "753102";
    RunTransactionAndWait -_transactionId "750102";
    RunTransactionAndWait -_transactionId "251801";
    return;
}



function ClearTheModel ([string] $_transactionId)
{
    RunTransactionAndWait -_transactionId "203101"; # delete assort items

    return;
}



#MENU
function ExecMenuItem([string] $menuItem) {

    Log "Executing menu item_$($menuItem)_in operation script"
    
    $ex = $menuItem;

    if ($ex -eq "00")     {    }

    #menubegin
    
    #old
    elseif ($ex -eq "100101") { DeployApi01 -_transactionId $ex }
    elseif ($ex -eq "100102") { DeployStore01 -_transactionId $ex }

    #10--html aboutme site
    elseif ($ex -eq "100110") { DeployHtmlAboutmeSite -_transactionId $ex}

    #20--MALL assortment
    elseif ($ex -eq "200000") { GetAssortmentStatus -_transactionId $ex}
    elseif ($ex -eq "200101") { FeedAssortmentToMallAssortWebApi -_transactionId $ex}
    elseif ($ex -eq "203101") { DeleteAllAssortmentItems -_transactionId $ex}
    elseif ($ex -eq "201801") { DeploySampleMallAssortWebApi -_transactionId $ex}

    elseif ($ex -eq "207401") { CompletelyClearThanFeedMallAssort -_transactionId $ex}
    elseif ($ex -eq "207402") { DropThanCreateAssortDbAndFeelAssortment -_transactionId $ex}
    elseif ($ex -eq "207403") { DeploySampleMallAssortWebApiWithAssortmentFill -_transactionId $ex}
    elseif ($ex -eq "207404") { DeployAssortmentThanDropCreateDbThanFeedAssortment -_transactionId $ex}

    #25--web logger
    elseif ($ex -eq "250000") { GetMessagesStatus -_transactionId $ex}
    elseif ($ex -eq "250101") { SendWebApiTestMessage -_transactionId $ex}
    elseif ($ex -eq "253101") { DeleteAllWebLoggerMessages -_transactionId $ex}
    elseif ($ex -eq "251801") { DeployWebLogger -_transactionId $ex}
    elseif ($ex -eq "257401") { DeployWebLoggerThanDelteAndCreateDb -_transactionId $ex}

    #26--web logger-2-Blazor

    elseif ($ex -eq "261801") { DeployWebLoggerBlazor -_transactionId $ex}

    #27--Suppliers
    elseif ($ex -eq "270000") { GetSuppliersStatus      -_transactionId $ex}
    elseif ($ex -eq "270101") { FeedSuppliersToWebApi   -_transactionId $ex}
    elseif ($ex -eq "273101") { DeleteAllSuppliers      -_transactionId $ex}

    #30--MALL blazor frontend
    elseif ($ex -eq "301801") { DeployMallBlazorFrontend  -_transactionId $ex}

    #75--database management
    elseif ($ex -eq "750000") { ViewPostgresDbsOnRemoteHost -_transactionId $ex }
    elseif ($ex -eq "750101") { CreateAssortDbOnRemoteHost  -_transactionId $ex }
    elseif ($ex -eq "753101") { DeleteAssortDbOnRemoteHost  -_transactionId $ex }

    elseif ($ex -eq "750102") { CreateWebLoggerDbOnRemoteHost -_transactionId $ex }
    elseif ($ex -eq "753102") { DeleteWebLoggerDbOnRemoteHost -_transactionId $ex }

    elseif ($ex -eq "300101") { FillClientsDb -_transactionId $ex}
    elseif ($ex -eq "900101") { RemoveBinObjFromMallBlazorFrontend }

    elseif ($ex -eq "99")     { $script:canExit = $true }

    else {
        "Wrong menu number, try again please";
    }
    #menuend
}

CheckLogsDirectory
CLearLogs
SetupCredentials

Log "Script started with params: transactionId=$transactionId, customWokingFolder=$customWokingFolder, callType=$callType"

if ($script:transactionId -ne '') {
    Log "Gonna exec menu item, script:transactionId= $($script:transactionId)";
    ExecMenuItem -menuItem $script:transactionId;
    exit
}

DO {
    PerformCls
    ShowMenu
    $ex = read-host 'Please enter menu point number'
    ExecMenuItem -menuItem $ex
}
WHILE ($script:canExit -eq $false)