@ECHO OFF
SETLOCAL
CD /d %~dp0

SET AWISE_AIEXTENSIONSFORGEMINI_RECORD_SNAPSHOT_TESTS=1

dotnet test tests\SnapshotTests\SnapshotTests.csproj
