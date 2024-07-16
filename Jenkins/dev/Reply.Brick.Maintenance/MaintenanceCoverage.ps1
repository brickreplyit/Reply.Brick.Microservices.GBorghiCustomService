param (
  [Parameter(Mandatory = $true)]
  [string]$Workspace
)

try {
	Get-ChildItem -Path $Workspace\CodeCoverage\Maintenance\ | foreach { Remove-Item $_.fullname -recurse}

	dotnet test $Workspace\Reply.Brick.Maintenance.Test\Reply.Brick.Maintenance.Test.csproj --results-directory:$Workspace\CodeCoverage\Maintenance\ --collect:"Code Coverage"  --settings:$Workspace\Jenkins\dev\Reply.Brick.Maintenance\MaintenanceConfiguration.runsettings 

	$coverage_output = ''
	$coverage = ''
	$pathCovarage = ''
	Get-ChildItem -Path $Workspace\CodeCoverage\Maintenance\ | foreach { $pathCovarage = $_.fullname  }
	$coverage_output = $pathCovarage + '\Maintenance.coveragexml'
	Get-ChildItem -Path $pathCovarage  | foreach {  $coverage = $_.fullname }

	cd $Workspace
	.\Jenkins\common\CodeCoverage\CodeCoverage.exe analyze  /output:$coverage_output  $coverage

	$pathCovarage = $Workspace + '\CodeCoverage\Maintenance\'
	
	dotnet $Workspace\Jenkins\common\netcoreapp\ReportGenerator.dll "-reports:$coverage_output" "-targetdir:$pathCovarage" -reporttypes:Html
	
	cd $Workspace
	.\Jenkins\common\reportunit\ReportUnit.exe $Workspace\Reply.Brick.Maintenance.Test\TestResults\TestResults.xml $Workspace\Reply.Brick.Maintenance.Test\TestResults\Generated.html
	
	$xml = $Workspace + '\Reply.Brick.Maintenance.Test\TestResults\TestResults.xml'
	$output = $Workspace + '\Reply.Brick.Maintenance.Test\TestResults\junit-results.xml'
	$xslt = New-Object System.Xml.Xsl.XslCompiledTransform;
	$xslt.Load($Workspace + '\Jenkins\common\nunit3-junit.xslt');
	$xslt.Transform($xml, $output);
}
catch {
    exit 1
}