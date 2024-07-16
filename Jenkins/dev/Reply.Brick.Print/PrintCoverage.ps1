param (
  [Parameter(Mandatory = $true)]
  [string]$Workspace
)

try {
	Get-ChildItem -Path $Workspace\CodeCoverage\Print\ | foreach { Remove-Item $_.fullname -recurse}

	dotnet test $Workspace\Reply.Brick.Print.Test\Reply.Brick.Print.Test.csproj --results-directory:$Workspace\CodeCoverage\Print\ --collect:"Code Coverage"  --settings:$Workspace\Jenkins\dev\Reply.Brick.Print\PrintConfiguration.runsettings 

	$coverage_output = ''
	$coverage = ''
	$pathCovarage = ''
	Get-ChildItem -Path $Workspace\CodeCoverage\Print\ | foreach { $pathCovarage = $_.fullname  }
	$coverage_output = $pathCovarage + '\Print.coveragexml'
	Get-ChildItem -Path $pathCovarage  | foreach {  $coverage = $_.fullname }

	cd $Workspace
	.\Jenkins\common\CodeCoverage\CodeCoverage.exe analyze  /output:$coverage_output  $coverage

	$pathCovarage = $Workspace + '\CodeCoverage\Print\'
	
	dotnet $Workspace\Jenkins\common\netcoreapp\ReportGenerator.dll "-reports:$coverage_output" "-targetdir:$pathCovarage" -reporttypes:Html
	
	cd $Workspace
	.\Jenkins\common\reportunit\ReportUnit.exe $Workspace\Reply.Brick.Print.Test\TestResults\TestResults.xml $Workspace\Reply.Brick.Print.Test\TestResults\Generated.html
	
	$xml = $Workspace + '\Reply.Brick.Print.Test\TestResults\TestResults.xml'
	$output = $Workspace + '\Reply.Brick.Print.Test\TestResults\junit-results.xml'
	$xslt = New-Object System.Xml.Xsl.XslCompiledTransform;
	$xslt.Load($Workspace + '\Jenkins\common\nunit3-junit.xslt');
	$xslt.Transform($xml, $output);
	
<#
	dotnet test $Workspace\Reply.Brick.Print.Tests\Reply.Brick.Print.Tests.csproj --logger:"trx;LogFileName=$Workspace\CodeCoverage\Print\unit_tests.xml"
	#>
<#
Copy-Item $coverage_output  -Destination  $pathCovarage
	$pathCobertura = $Workspace + '\CodeCoverage\Print\'
	
	.\Jenkins\common\opencovertocoberturaconverter\tools\OpenCoverToCoberturaConverter.exe  -input:$coverage_output -output:Cobertura.xml  -sources:$pathCobertura
	
#>

}
catch {
    exit 1
}