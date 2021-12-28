Write-Output "##################################"
Write-Output "###      Building Plugins      ###"
Write-Output "##################################"


$output2 = $args[0]
#if ([String]::IsNullOrEmpty($output)) {
$output = '../FileFlows/deploy/Plugins';
#}

$year = (Get-Date).year
$copyright = "Copyright $year - John Andrews"


# build plugin
# build 0.0.1.0 so included one is always greater
dotnet.exe build ..\Plugin\Plugin.csproj /p:WarningLevel=1 --configuration Release  /p:AssemblyVersion=0.0.1.0 /p:Version=0.0.1.0 /p:CopyRight=$copyright --output ../../FileFlowsPlugins

Remove-Item ../../FileFlowsPlugins/FileFlows.Plugin.deps.json -ErrorAction SilentlyContinue

Push-Location ..\..\FileFlowsPlugins

Remove-Item Builds  -Recurse -ErrorAction SilentlyContinue

$revision = (git rev-list --count --first-parent HEAD) -join "`n"

$json = "[`n"

Get-ChildItem -Path .\ -Filter *.csproj -Recurse -File -Name | ForEach-Object {
    # update version number of builds
    (Get-Content $_) `
        -replace '(?<=(Version>([\d]+\.){3}))([\d]+)(?=<)', $revision |
    Out-File $_

        
    $name = [System.IO.Path]::GetFileNameWithoutExtension($_) 
    Write-Output "Building Plugin $name"
    $version = [Regex]::Match((Get-Content $_), "(?<=(Version>))([\d]+\.){3}[\d]+(?=<)").Value
    Write-Output "### Version: $version"
    $description = [Regex]::Match((Get-Content $_), "(?<=(Description>))[^<]+").Value
    Write-Output "### Description: $description"
    $Authors = [Regex]::Match((Get-Content $_), "(?<=(Authors>))[^<]+").Value
    Write-Output "### Authors: $Authors"
    $url = [Regex]::Match((Get-Content $_), "(?<=(PackageProjectUrl>))[^<]+").Value
    Write-Output "### Url: $url"
    

    # build an instance for FileFlow local code
    dotnet build $_ /p:WarningLevel=1 --configuration Release /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary --output:$output/$name/  
    Remove-Item $output/$name/FileFlows.Plugin.dll -ErrorAction SilentlyContinue
    Remove-Item $output/$name/FileFlows.Plugin.pdb -ErrorAction SilentlyContinue
    Remove-Item $output/$name/*.deps.json -ErrorAction SilentlyContinue
    Remove-Item $output/$name/ref -Recurse -ErrorAction SilentlyContinue

    Push-Location ../FileFlows/PluginInfoGenerator
    dotnet run ../deploy/Plugins/$name
    Pop-Location
    Move-Item $output/$name/*.plugininfo $output/$name/.plugininfo -Force

    if ((Test-Path -Path $output/$name/.plugininfo -PathType Leaf)) {

        # only actually create the plugin if plugins were found in it        
        $json += "`t{`n"
        $json += "`t`t""Name"": ""$name"",`n"
        $json += "`t`t""Version"": ""$version"",`n"
        $json += "`t`t""Authors"": ""$Authors"",`n"
        $json += "`t`t""Url"": ""$url"",`n"
        $json += "`t`t""Description"": ""$description"",`n"
        $json += "`t`t""Package"": ""$name.ffplugin""`n"
        $json += "`t},`n"
        

        Move-Item $output/$name/*.en.json $output/$name/en.json -Force

        # construct .ffplugin file
        $compress = @{
            Path             = "$output/$name/*"
            CompressionLevel = "Optimal"
            DestinationPath  = "$output/$name.zip"
        }
        Write-Output "Creating zip file $output/$name.zip"

        Compress-Archive @compress

        Write-Output "Creating plugin file $output/$name.ffplugin"
        Move-Item "$output/$name.zip" "$output/$name.ffplugin" -Force

        if ([String]::IsNullOrEmpty($output2) -eq $false) {
            Write-Output "Moving file to $output2"        
            Copy-Item "$output/$name.ffplugin" "$output2/" -Force
        }
    }

    Remove-Item $output/$name -Recurse -ErrorAction SilentlyContinue
}

$json = $json.Substring(0, $json.lastIndexOf(',')) + "`n"
$json += ']';

Set-Content -Path "$output/plugins.json" -Value $json

Pop-Location
