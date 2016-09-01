
#build tests
& $env:MSBUILD_PATH /p:configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal /v:m

#download nuget package
$password = convertto-securestring -String "$REPOX_QAPUBLICADMIN_PASSWORD" -AsPlainText -Force
$cred = new-object -typename System.Management.Automation.PSCredential -argumentlist $REPOX_QAPUBLICADMIN_USERNAME, $password
$ARTIFACTORY_SRC_REPO="sonarsource-nuget-qa"
$url = "$ARTIFACTORY_URL/$ARTIFACTORY_SRC_REPO/$ARTIFACT"
Invoke-WebRequest -Uri $url -Credential $cred

#unzip nuget package
$zipName=$ARTIFACT.Substring(0, $ARTIFACT.LastIndexOf('.'))+".zip"
mv $ARTIFACT $zipName
$shell_app=new-object -com shell.application
$currentdir=(Get-Item -Path ".\" -Verbose).FullName
$destination = $shell_app.NameSpace($currentdir)
$zip_file = $shell_app.NameSpace("$currentdir\$zipName")
$destination.CopyHere($zip_file.Items())

#move dlls to correct locations
mv analyzers\*.dll src\SonarLint.CSharp\bin\Release

#run tests

C:\PROGRA~2\MICROS~2.0\Common7\IDE\COMMON~1\MICROS~1\TESTWI~1\vstest.console.exe 

& $env:VSTEST_PATH .\src\Tests\SonarLint.SonarQube.Integration.UnitTest\bin\Release\SonarLint.SonarQube.Integration.UnitTest.dll
& $env:VSTEST_PATH .\src\Tests\SonarLint.UnitTest\bin\Release\SonarLint.UnitTest.dll
 
#run regression-test
.\its\regression-test.bat