param (
  [Parameter(Mandatory = $true)]
  [string]$Workspace
)

try {
	Get-ChildItem -Path $Workspace\CodeCoverage\Execution\ | foreach { Remove-Item $_.fullname -recurse}

	dotnet test $Workspace\Reply.Brick.Execution.Tests\Reply.Brick.Execution.Tests.csproj --results-directory:$Workspace\CodeCoverage\Execution\ --collect:"Code Coverage"  --settings:$Workspace\Jenkins\dev\Reply.Brick.Execution\ExecutionConfiguration.runsettings 

	$coverage_output = ''
	$coverage = ''
	$pathCovarage = ''
	Get-ChildItem -Path $Workspace\CodeCoverage\Execution\ | foreach { $pathCovarage = $_.fullname  }
	$coverage_output = $pathCovarage + '\Execution.coveragexml'
	Get-ChildItem -Path $pathCovarage  | foreach {  $coverage = $_.fullname }

	cd $Workspace
	.\Jenkins\common\CodeCoverage\CodeCoverage.exe analyze  /output:$coverage_output  $coverage

	$pathCovarage = $Workspace + '\CodeCoverage\Execution\'
	
	dotnet $Workspace\Jenkins\common\netcoreapp\ReportGenerator.dll "-reports:$coverage_output" "-targetdir:$pathCovarage" -reporttypes:Html
	
	cd $Workspace
	.\Jenkins\common\reportunit\ReportUnit.exe $Workspace\Reply.Brick.Execution.Tests\TestResults\TestResults.xml $Workspace\Reply.Brick.Execution.Tests\TestResults\Generated.html
	
	
	$xml = $Workspace + '\Reply.Brick.Execution.Tests\TestResults\TestResults.xml'
	$output = $Workspace + '\Reply.Brick.Execution.Tests\TestResults\junit-results.xml'
	$xslt = New-Object System.Xml.Xsl.XslCompiledTransform;
	$xslt.Load($Workspace + '\Jenkins\common\nunit3-junit.xslt');
	$xslt.Transform($xml, $output);
	

}
catch {
    exit 1
}