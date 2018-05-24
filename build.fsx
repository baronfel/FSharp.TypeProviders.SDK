// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#I "packages/FAKE/tools"

#r "packages/FAKE/tools/FakeLib.dll"

open System
open System.IO
open Fake

// --------------------------------------------------------------------------------------
// Information about the project to be used at NuGet
// --------------------------------------------------------------------------------------

let project = "FSharp.TypeProviders.SDK"
let authors = ["Tomas Petricek"; "Gustavo Guerra"; "Michael Newton"; "Don Syme" ]
let summary = "Helper code and examples for getting started with Type Providers"
let description = """
  The F# Type Provider SDK provides utilities for authoring type providers."""
let tags = "F# fsharp typeprovider"

let gitHome = "https://github.com/fsprojects"
let gitName = "FSharp.TypeProviders.SDK"

let config = "Release"

// Read release notes & version info from RELEASE_NOTES.md
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let release =
    File.ReadLines "RELEASE_NOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

let useMsBuildToolchain = environVar "USE_MSBUILD" <> null
let dotnetSdkVersion = "2.1.201"

let exec p args = 
    printfn "Executing %s %s" p args 
    Shell.Exec(p, args) |> function 0 -> () | d -> failwithf "%s %s exited with error %d" p args d

let pullRequest =
    match getBuildParamOrDefault "APPVEYOR_PULL_REQUEST_NUMBER" "" with
    | "" ->
        trace "Master build detected"
        None
    | a ->
        trace "Pull Request build detected"
        Some <| int a

let buildNumber =
    getBuildParamOrDefault "APPVEYOR_BUILD_VERSION" "0"

let version =
    match pullRequest with
    | None ->
        sprintf "%s.%s" release.AssemblyVersion buildNumber
    | Some num ->
        sprintf "%s-pull-%d-%s" release.AssemblyVersion num buildNumber
let releaseNotes = release.Notes |> String.concat "\n"
let srcDir = "src"

let sources =
    [srcDir @@ "ProvidedTypes.fsi"
     srcDir @@ "ProvidedTypes.fs"
     srcDir @@ "ProvidedTypesTesting.fs" ]

Target "Clean" (fun _ ->
    CleanDirs []
)

Target "Build" (fun _ ->
  if useMsBuildToolchain then
    DotNetCli.Restore  (fun p -> { p with Project = "src/FSharp.TypeProviders.SDK.fsproj" })
    DotNetCli.Restore  (fun p -> { p with Project = "tests/FSharp.TypeProviders.SDK.Tests.fsproj" })
    MSBuildRelease null "Build" ["src/FSharp.TypeProviders.SDK.fsproj"] |> Log "Build-Output: "
    MSBuildRelease null "Build" ["tests/FSharp.TypeProviders.SDK.Tests.fsproj"] |> Log "Build-Output: "
  else
    DotNetCli.Build  (fun p -> { p with Configuration = config; Project = "src/FSharp.TypeProviders.SDK.fsproj" })
    DotNetCli.Build  (fun p -> { p with Configuration = config; Project = "tests/FSharp.TypeProviders.SDK.Tests.fsproj" })
)

Target "Examples" (fun _ ->
  if useMsBuildToolchain then
    DotNetCli.Restore  (fun p -> { p with Project = "examples/BasicProvider.DesignTime/BasicProvider.DesignTime.fsproj" })
    DotNetCli.Restore  (fun p -> { p with Project = "examples/BasicProvider/BasicProvider.fsproj" })
    DotNetCli.Restore  (fun p -> { p with Project = "examples/ComboProvider/ComboProvider.fsproj" })
    DotNetCli.Restore  (fun p -> { p with Project = "examples/BasicProvider.Tests/BasicProvider.Tests.fsproj" })
    DotNetCli.Restore  (fun p -> { p with Project = "examples/ComboProvider.Tests/ComboProvider.Tests.fsproj" })
    MSBuildRelease null "Build" ["examples/BasicProvider.DesignTime/BasicProvider.DesignTime.fsproj"] |> Log "Build-Output: "
    MSBuildRelease null "Build" ["examples/BasicProvider/BasicProvider.fsproj"] |> Log "Build-Output: "
    MSBuildRelease null "Build" ["examples/ComboProvider/ComboProvider.fsproj"] |> Log "Build-Output: "
    MSBuildRelease null "Build" ["examples/BasicProvider.Tests/BasicProvider.Tests.fsproj"] |> Log "Build-Output: "
    MSBuildRelease null "Build" ["examples/ComboProvider.Tests/ComboProvider.Tests.fsproj"] |> Log "Build-Output: "
  else
    DotNetCli.Build  (fun p -> { p with Configuration = config; Project = "examples/BasicProvider.DesignTime/BasicProvider.DesignTime.fsproj" })
    DotNetCli.Build  (fun p -> { p with Configuration = config; Project = "examples/BasicProvider/BasicProvider.fsproj" })
    DotNetCli.Build  (fun p -> { p with Configuration = config; Project = "examples/ComboProvider/ComboProvider.fsproj" })
)
Target "RunTests" (fun _ ->
#if MONO
  // Run the netcoreapp2.0 tests with "dotnet test"
    DotNetCli.Test  (fun p -> { p with Configuration = config; Project = "tests/FSharp.TypeProviders.SDK.Tests.fsproj"; AdditionalArgs=["-f"; "netcoreapp2.0"] })

  // We don't use "dotnet test" for Mono testing the test runner doesn't know how to run with Mono
  // This is a bit of a hack to find the output test DLL and run xunit using Mono directly
    let dir = "tests/bin/" + config + "/net461"
    let file = 
        (Array.append [| dir |] (System.IO.Directory.GetDirectories(dir)))
        |> Array.pick (fun subdir -> 
            let file = subdir + "/FSharp.TypeProviders.SDK.Tests.dll"
            if System.IO.File.Exists file then Some file else None)

    exec "packages/xunit.runner.console/tools/net452/xunit.console.exe" (file + " -parallel none")
            
    // This can also be used on Mono to give console output:
    // msbuild tests/FSharp.TypeProviders.SDK.Tests.fsproj /p:Configuration=Debug && mono packages/xunit.runner.console/tools/net452/xunit.console.exe tests/bin/Debug/net461/FSharp.TypeProviders.SDK.Tests.dll -parallel none
#else
    DotNetCli.Test  (fun p -> { p with Configuration = config; Project = "tests/FSharp.TypeProviders.SDK.Tests.fsproj" })
    DotNetCli.Test  (fun p -> { p with Configuration = config; Project = "examples/BasicProvider.Tests/BasicProvider.Tests.fsproj" })
    DotNetCli.Test  (fun p -> { p with Configuration = config; Project = "examples/ComboProvider.Tests/ComboProvider.Tests.fsproj" })

    // This can also be used to give console output:
    // dotnet build tests\FSharp.TypeProviders.SDK.Tests.fsproj -c Debug -f net461 && packages\xunit.runner.console\tools\net452\xunit.console.exe tests\bin\Debug\net461\FSharp.TypeProviders.SDK.Tests.dll -parallel none

#endif
)

Target "Pack" (fun _ ->
    DotNetCli.Pack  (fun p -> { p with Configuration = config; Project = "src/FSharp.TypeProviders.SDK.fsproj" })
    DotNetCli.Pack   (fun p -> { p with Configuration = config; Project = "examples/BasicProvider/BasicProvider.fsproj" })
    DotNetCli.Pack   (fun p -> { p with Configuration = config; Project = "examples/ComboProvider/ComboProvider.fsproj" })
)

"Clean" ==> "Pack"
"Build" ==> "Examples" ==> "Pack"
"Build" ==> "RunTests" ==> "Pack"

RunTargetOrDefault "RunTests"
