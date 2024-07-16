param (
  [Parameter(Mandatory = $true)]
  [string]$Workspace
)

try {
	Get-ChildItem -Path $Workspace\CodeCoverage\Reporting\ | foreach { Remove-Item $_.fullname -recurse}

	dotnet test $Workspace\Reply.Brick.Reporting.Tests\Reply.Brick.Reporting.Tests.csproj --results-directory:$Workspace\CodeCoverage\Reporting\ --collect:"Code Coverage"  --settings:$Workspace\Jenkins\dev\Reply.Brick.Reporting\ReportingConfiguration.runsettings 

	$coverage_output = ''
	$coverage = ''
	$pathCovarage = ''
	Get-ChildItem -Path $Workspace\CodeCoverage\Reporting\ | foreach { $pathCovarage = $_.fullname  }
	$coverage_output = $pathCovarage + '\Reporting.coveragexml'
	Get-ChildItem -Path $pathCovarage  | foreach {  $coverage = $_.fullname }

	cd $Workspace
	.\Jenkins\common\CodeCoverage\CodeCoverage.exe analyze  /output:$coverage_output  $coverage

	$pathCovarage = $Workspace + '\CodeCoverage\Reporting\'
	
	dotnet $Workspace\Jenkins\common\netcoreapp\ReportGenerator.dll "-reports:$coverage_output" "-targetdir:$pathCovarage" -reporttypes:Html
	
	cd $Workspace
	.\Jenkins\common\reportunit\ReportUnit.exe $Workspace\Reply.Brick.Reporting.Tests\TestResults\TestResults.xml $Workspace\Reply.Brick.Reporting.Tests\TestResults\Generated.html
	
	
	$xml = $Workspace + '\Reply.Brick.Reporting.Tests\TestResults\TestResults.xml'
	$output = $Workspace + '\Reply.Brick.Reporting.Tests\TestResults\junit-results.xml'
	$xslt = New-Object System.Xml.Xsl.XslCompiledTransform;
	$xslt.Load($Workspace + '\Jenkins\common\nunit3-junit.xslt');
	$xslt.Transform($xml, $output);
	

}
catch {
    exit 1
}