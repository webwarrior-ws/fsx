#!/usr/bin/env fsx

open System
open System.IO
open System.Net
open System.Linq
open System.Diagnostics

#r "System.Configuration"
open System.Configuration

#load "../InfraLib/Misc.fs"
#load "../InfraLib/Process.fs"
#load "../InfraLib/Git.fs"

open FSX.Infrastructure
open Process

let ScriptsDir = __SOURCE_DIRECTORY__ |> DirectoryInfo
let RootDir = Path.Combine(ScriptsDir.FullName, "..") |> DirectoryInfo
let TestDir = Path.Combine(RootDir.FullName, "test") |> DirectoryInfo
let NugetDir = Path.Combine(RootDir.FullName, ".nuget") |> DirectoryInfo
let NugetExe = Path.Combine(NugetDir.FullName, "nuget.exe") |> FileInfo
let NugetPackages = Path.Combine(RootDir.FullName, "packages") |> DirectoryInfo
// please maintain this URL in sync with the make.fsx file
let NugetUrl = "https://dist.nuget.org/win-x86-commandline/v5.4.0/nuget.exe"

let GetFsxWindowsLauncher() =
    let programFiles =
        Environment.GetEnvironmentVariable "ProgramW6432" |> DirectoryInfo

    let fsxWinInstallationDir =
        Path.Combine(programFiles.FullName, "fsx") |> DirectoryInfo

    Path.Combine(fsxWinInstallationDir.FullName, "fsx.bat") |> FileInfo

let CreateCommandForTest(fsxFile: FileInfo, args: string) =
    if Misc.GuessPlatform() = Misc.Platform.Windows then
        // because Windows and shebang are not friends
        {
            Command = GetFsxWindowsLauncher().FullName
            Arguments = sprintf "%s %s" fsxFile.FullName args
        }
    else
        // because shebang works in Unix
        {
            Command = fsxFile.FullName
            Arguments = args
        }


let basicTest = Path.Combine(TestDir.FullName, "test.fsx") |> FileInfo

Process
    .Execute(CreateCommandForTest(basicTest, String.Empty), Echo.All)
    .UnwrapDefault()
|> ignore<string>

let nonExistentTest =
    Path.Combine(TestDir.FullName, "nonExistentFsx.fsx") |> FileInfo

let commandForNonExistentTest =
    if Misc.GuessPlatform() = Misc.Platform.Windows then
        GetFsxWindowsLauncher().FullName
    else
        // FIXME: extract PREFIX from build.config instead of assuming default
        "/usr/local/bin/fsx"

let proc =
    Process.Execute(
        {
            Command = commandForNonExistentTest
            Arguments = nonExistentTest.FullName
        },
        Echo.All
    )

// the reason to write the result of this to a file is:
// if error propagation is broken, then it would be broken as well for make.fsx
// when trying to call this very file (runTests.fsx) and wouldn't pick up an err
let errorPropagationResultFile =
    Path.Combine(TestDir.FullName, "errProp.txt") |> FileInfo

match proc.Result with
| Error _ -> File.WriteAllText(errorPropagationResultFile.FullName, "0")
| _ ->
    File.WriteAllText(errorPropagationResultFile.FullName, "1")
    failwith "Call to non-existent test should have failed (exitCode <> 0)"


match Misc.GuessPlatform() with
| Misc.Platform.Windows ->
    // not all tests run in Windows yet, WIP!
    Environment.Exit 0
| _ -> ()

let fsFileToBuild = Path.Combine(TestDir.FullName, "test.fs") |> FileInfo
let libToRef1 = Path.Combine(TestDir.FullName, "test1.dll") |> FileInfo

let fscCmd1 =
    {
        Command = "fsharpc"
        Arguments =
            sprintf
                "%s --target:library --out:%s"
                fsFileToBuild.FullName
                libToRef1.FullName
    }

Process.Execute(fscCmd1, Echo.All).UnwrapDefault() |> ignore<string>
let refLibTest = Path.Combine(TestDir.FullName, "testRefLib.fsx") |> FileInfo

Process
    .Execute(CreateCommandForTest(refLibTest, String.Empty), Echo.All)
    .UnwrapDefault()
|> ignore<string>


let subLibFolder =
    Directory.CreateDirectory(Path.Combine(TestDir.FullName, "lib"))

let libToRef2 = Path.Combine(subLibFolder.FullName, "test2.dll") |> FileInfo

let fscCmd2 =
    {
        Command = "fsharpc"
        Arguments =
            sprintf
                "%s --target:library --out:%s"
                fsFileToBuild.FullName
                libToRef2.FullName
    }

Process.Execute(fscCmd2, Echo.All).UnwrapDefault() |> ignore<string>

let refLibOutsideCurrentFolderTest =
    Path.Combine(TestDir.FullName, "testRefLibOutsideCurrentFolder.fsx")
    |> FileInfo

Process
    .Execute(
        CreateCommandForTest(refLibOutsideCurrentFolderTest, String.Empty),
        Echo.All
    )
    .UnwrapDefault()
|> ignore<string>

if not NugetExe.Exists then
    if not NugetDir.Exists then
        Directory.CreateDirectory(NugetDir.FullName) |> ignore

    use webClient = new WebClient()
    webClient.DownloadFile(NugetUrl, NugetExe.FullName)

if not NugetPackages.Exists then
    Directory.CreateDirectory(NugetPackages.FullName) |> ignore

let nugetCmd =
    {
        Command = "mono"
        Arguments =
            sprintf
                "%s install Microsoft.Build -Version 16.11.0 -OutputDirectory %s"
                NugetExe.FullName
                NugetPackages.FullName
    }

Process
    .Execute(nugetCmd, Echo.All)
    .UnwrapDefault()
|> ignore<string>

let refNugetLibTest =
    Path.Combine(TestDir.FullName, "testRefNugetLib.fsx") |> FileInfo

Process
    .Execute(CreateCommandForTest(refNugetLibTest, String.Empty), Echo.All)
    .UnwrapDefault()
|> ignore<string>


let cmdLineArgsTest =
    Path.Combine(TestDir.FullName, "testFsiCommandLineArgs.fsx") |> FileInfo

Process
    .Execute(CreateCommandForTest(cmdLineArgsTest, "one 2 three"), Echo.All)
    .UnwrapDefault()
|> ignore<string>

let tsvTest = Path.Combine(TestDir.FullName, "testTsv.fsx") |> FileInfo

Process
    .Execute(CreateCommandForTest(tsvTest, String.Empty), Echo.All)
    .UnwrapDefault()
|> ignore<string>

let processTest = Path.Combine(TestDir.FullName, "testProcess.fsx") |> FileInfo

Process
    .Execute(CreateCommandForTest(processTest, String.Empty), Echo.All)
    .UnwrapDefault()
|> ignore<string>

(* this is actually only really useful for when process spits both stdout & stderr
let processConcurrencyTest = Path.Combine(TestDir.FullName, "testProcessConcurrency.fsx") |> FileInfo
Process.Execute({ Command = processConcurrencyTest.FullName; Arguments = String.Empty }, Echo.All)
       .UnwrapDefault() |> ignore<string>
*)
