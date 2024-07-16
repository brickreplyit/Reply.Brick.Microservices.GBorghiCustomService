
try {
	Get-ChildItem F:\Build\SSDT\ | foreach { Remove-Item $_.fullname -recurse}
}
catch {
    exit 1
}