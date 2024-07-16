param (
  [Parameter(Mandatory = $true)]
  [string]$Workspace
)

try {
	Get-ChildItem -Path $Workspace\CodeCoverage\ConfigurationManager\ | foreach { Remove-Item $_.fullname -recurse}

	dotnet test $Workspace\Reply.Brick.Configuration.Manager.Tests\Reply.Brick.Configuration.Manager.Tests.csproj --results-directory:$Workspace\CodeCoverage\ConfigurationManager\ --collect:"Code Coverage"  --settings:$Workspace\Jenkins\dev\Reply.Brick.Configuration.Manager\ConfigurationManagerConfiguration.runsettings 

	$coverage_output = ''
	$coverage = ''
	$pathCovarage = ''
	Get-ChildItem -Path $Workspace\CodeCoverage\ConfigurationManager\ | foreach { $pathCovarage = $_.fullname  }
	$coverage_output = $pathCovarage + '\ConfigurationManager.coveragexml'
	Get-ChildItem -Path $pathCovarage  | foreach {  $coverage = $_.fullname }

	cd $Workspace
	.\Jenkins\common\CodeCoverage\CodeCoverage.exe analyze  /output:$coverage_output  $coverage

	$pathCovarage = $Workspace + '\CodeCoverage\ConfigurationManager\'
	
	dotnet $Workspace\Jenkins\common\netcoreapp\ReportGenerator.dll "-reports:$coverage_output" "-targetdir:$pathCovarage" -reporttypes:Html
	
	cd $Workspace
	.\Jenkins\common\reportunit\ReportUnit.exe $Workspace\Reply.Brick.Configuration.Manager.Tests\TestResults\TestResults.xml $Workspace\Reply.Brick.Configuration.Manager.Tests\TestResults\Generated.html
	
	
	$xml = $Workspace + '\Reply.Brick.Configuration.Manager.Tests\TestResults\TestResults.xml'
	$output = $Workspace + '\Reply.Brick.Configuration.Manager.Tests\TestResults\junit-results.xml'
	$xslt = New-Object System.Xml.Xsl.XslCompiledTransform;
	$xslt.Load($Workspace + '\Jenkins\common\nunit3-junit.xslt');
	$xslt.Transform($xml, $output);
	
<#
	dotnet test $Workspace\Reply.Brick.Quality.Tests\Reply.Brick.Quality.Tests.csproj --logger:"trx;LogFileName=$Workspace\CodeCoverage\Quality\unit_tests.xml"
	#>
<#
Copy-Item $coverage_output  -Destination  $pathCovarage
	$pathCobertura = $Workspace + '\CodeCoverage\Quality\'
	
	.\Jenkins\common\opencovertocoberturaconverter\tools\OpenCoverToCoberturaConverter.exe  -input:$coverage_output -output:Cobertura.xml  -sources:$pathCobertura
	
#>

}
catch {
    exit 1
}