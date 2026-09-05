Set-Location $PSScriptRoot

$Project = Join-Path $PSScriptRoot "MeshCoreLauncher\MeshCoreLauncher.csproj"
$Config = "Release"
$Out = Join-Path $PSScriptRoot "publish"

$Rids = "win-x64", "win-x86", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"

if (Test-Path $Out) {
	Remove-Item -Recurse -Force $Out
}

foreach ($rid in $Rids) {
	foreach ($selfContained in "true", "false") {
		$kind = if ($selfContained -eq "true") { "self-contained" } else { "framework-dependent" }
		$dest = Join-Path $Out "$rid\$kind"

		Write-Host ""
		Write-Host "=== $rid  self-contained=$selfContained ==="
		dotnet publish $Project -c $Config -r $rid --self-contained $selfContained -o $dest
		if ($LASTEXITCODE -ne 0) {
			Write-Host ""
			Write-Host "BUILD FAILED."
			exit 1
		}
	}
}

Write-Host ""
Write-Host "All builds succeeded. Output in `"$Out`"."
