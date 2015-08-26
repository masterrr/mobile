#r @"packages/FAKE.4.1.4/tools/FakeLib.dll"
#load ".build-helpers.fsx"
open Fake
open System
open System.IO
open System.Linq
open BuildHelpers
open Fake.XamarinHelper
open Fake.FileUtils

Target "core-build" (fun () ->
    RestorePackages "Phoebe/packages.config"
    RestorePackages "Tests/packages.config"
    MSBuild "Phoebe/bin/Desktop/Debug" "Build" [ ("Configuration", "Debug"); ("Platform", "Any CPU") ] [ "Phoebe/Phoebe.Desktop.csproj" ] |> ignore
    MSBuild "Tests/bin/Debug" "Build" [ ("Configuration", "Debug"); ("Platform", "Any CPU") ] [ "Tests/Tests.csproj" ] |> ignore
)

Target "core-tests" (fun () ->
    RestorePackages "Tests/packages.config"
    RunNUnitTests "Tests/bin/Debug/Tests.dll" "Tests/bin/Debug/TestResult.xml"
)

Target "android-build" (fun () ->
    // Clear previous compilation

    // Copy config. file from path or use default.
    let buildParamsFile = getBuildParam "buildParamsFile"
    if (System.String.Empty <> buildParamsFile)
      then cp buildParamsFile "Phoebe/Build.cs"

    RestorePackages "Joey/packages.config"
    MSBuild "Joey/bin/Release" "Build" [ ("Configuration", "Release") ] [ "Joey/Joey.csproj" ] |> ignore
)

Target "android-package" (fun () ->
    // Android build parameters
    let keyStorePath = getBuildParamOrDefault "keyStorePath" "toggl.keystore"
    let keyStorePassword = getBuildParamOrDefault "keyStorePassword" ""
    let keyStoreAlias = getBuildParamOrDefault "keyStoreAlias" "toggl"

    AndroidPackage (fun defaults ->
        {defaults with
            ProjectPath = "Joey/Joey.csproj"
            Configuration = "Release"
            OutputPath = "Joey/bin/Release"
        })
    |> AndroidSignAndAlign (fun defaults ->
        {defaults with
            KeystorePath = keyStorePath
            KeystorePassword = keyStorePassword
            KeystoreAlias = keyStoreAlias
            // If zipalign tool is not added to system path
            // you should uncomment this line and configure
            // the correct path.
            // ZipalignPath = "/Users/xxx/Library/Developers/Xamarin/android-sdk-macosx/build-tools/23.0.0/zipalign"
        })
    |> fun file -> TeamCityHelper.PublishArtifact file.FullName
)

"core-build"
  ==> "core-tests"

"android-build"
  ==> "android-package"

RunTarget()
